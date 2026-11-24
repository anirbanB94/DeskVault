using DeskVault.UI.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Concurrent;

namespace DeskVault.UI.Services.Document;

/// <summary>
/// Materializes documents for external viewing and owns their temporary artifact lifecycles.
/// </summary>
/// <remarks>
/// Temporary artifacts are created through <see cref="TemporaryDocumentArtifactStore"/>.
/// The caller retains ownership of the source stream. An artifact remains registered until
/// cleanup succeeds or its external viewing lifecycle is still active.
/// </remarks>
public sealed class DocumentViewer :
    IDocumentViewer,
    IDisposable
{
    private readonly IExternalDocumentLauncher _externalDocumentLauncher;
    private readonly TemporaryDocumentArtifactStore _artifactStore;
    private readonly ILogger<DocumentViewer> _logger;

    private readonly ConcurrentDictionary<Guid, ActiveArtifact> _activeArtifacts =
        new();

    // Identity-collision artifacts cannot replace an existing GUID registration.
    // Keep each rejected artifact separately owned when immediate cleanup fails.
    private readonly ConcurrentDictionary<ActiveArtifact, byte> _unregisteredCleanupArtifacts =
        new();

    private readonly Lock _lifecycleSync = new();

    private bool _disposed;

    /// <summary>
    /// Initializes a viewer using the production temporary artifact store.
    /// </summary>
    /// <param name="externalDocumentLauncher">The external viewer launch boundary.</param>
    public DocumentViewer(
        IExternalDocumentLauncher externalDocumentLauncher)
        : this(
            externalDocumentLauncher,
            new TemporaryDocumentArtifactStore(),
            NullLogger<DocumentViewer>.Instance)
    {
    }

    /// <summary>
    /// Initializes a viewer with explicit artifact storage and diagnostic services.
    /// </summary>
    /// <param name="externalDocumentLauncher">The external viewer launch boundary.</param>
    /// <param name="artifactStore">The protected temporary artifact store.</param>
    /// <param name="logger">The logger used for safe lifecycle diagnostics.</param>
    internal DocumentViewer(
        IExternalDocumentLauncher externalDocumentLauncher,
        TemporaryDocumentArtifactStore artifactStore,
        ILogger<DocumentViewer> logger)
    {
        ArgumentNullException.ThrowIfNull(externalDocumentLauncher);
        ArgumentNullException.ThrowIfNull(artifactStore);
        ArgumentNullException.ThrowIfNull(logger);

        _externalDocumentLauncher = externalDocumentLauncher;
        _artifactStore = artifactStore;
        _logger = logger;
    }

    /// <summary>
    /// Copies a caller-owned document stream to protected temporary storage and launches it.
    /// </summary>
    /// <param name="documentStream">The caller-owned source document stream.</param>
    /// <param name="fileName">The original filename, used only to determine the extension.</param>
    /// <param name="cancellationToken">The cancellation token for materialization.</param>
    public async Task OpenAsync(
        Stream documentStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        lock (_lifecycleSync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }

        ArgumentNullException.ThrowIfNull(documentStream);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        cancellationToken.ThrowIfCancellationRequested();

        TemporaryDocumentArtifact artifact =
            _artifactStore.CreateArtifact(fileName);

        var activeArtifact = new ActiveArtifact(artifact);

        if (!_activeArtifacts.TryAdd(
            artifact.Id,
            activeArtifact))
        {
            // Preserve the existing GUID owner while retaining this distinct artifact
            // for retry if its immediate cleanup attempt fails.
            lock (_lifecycleSync)
            {
                _unregisteredCleanupArtifacts[activeArtifact] = 0;
            }

            MarkPreparationFailedAndCleanup(activeArtifact);

            throw new InvalidOperationException(
                "The temporary document artifact identity is already active.");
        }

        IExternalDocumentLaunch? launch = null;
        bool launchHandleReturned = false;
        bool launchOwnershipTransferred = false;

        try
        {
            // Disposal may have raced with artifact creation and registration.
            lock (_lifecycleSync)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
            }

            await artifact.CopyFromAsync(
                documentStream,
                cancellationToken).ConfigureAwait(false);

            Task? completion;

            // Launch admission and lifecycle classification are atomic with respect
            // to viewer disposal. The in-progress state prevents disposal from
            // releasing the ownership lease before Launch() returns an outcome.
            lock (_lifecycleSync)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);

                activeArtifact.MarkExternalLaunchInvocationInProgress();

                launch = _externalDocumentLauncher.Launch(
                    artifact.FilePath);

                // Set this immediately after Launch returns and before inspecting
                // the returned handle's completion contract.
                launchHandleReturned = true;

                completion = launch.Completion;

                activeArtifact.SetExternalLifecycleObservable(
                    completion is not null);

                // A re-entrant Dispose() may have occurred inside the launcher.
                // Once an unobservable outcome is established, release the lease
                // if the viewer has already been disposed.
                if (completion is null && _disposed)
                {
                    ReleaseOwnershipForRecovery(activeArtifact);
                }
            }

            _ = ObserveExternalLaunchAsync(
                activeArtifact,
                launch,
                completion);

            launchOwnershipTransferred = true;
        }
        catch
        {
            if (launch is not null && !launchOwnershipTransferred)
            {
                DisposeLaunch(launch);
            }

            if (!launchHandleReturned)
            {
                // Copy failed or Launch() failed before returning a handle.
                // The in-progress state prevented disposal from releasing the
                // lease, so cleanup eligibility can now be persisted safely.
                lock (_lifecycleSync)
                {
                    MarkPreparationFailedAndCleanup(activeArtifact);
                }
            }
            else
            {
                // A launch handle was returned. A failure while inspecting its
                // completion contract or establishing observation is ambiguous:
                // retain the artifact for recovery rather than deleting it.
                MarkExternalLifecycleUnobservable(activeArtifact);

                _logger.LogWarning(
                    "External document launch handle did not establish a trustworthy lifecycle observation. ArtifactId: {ArtifactId}; Artifact retained for recovery.",
                    activeArtifact.Artifact.Id);
            }

            throw;
        }
    }

    /// <summary>
    /// Observes a trustworthy external-consumer lifecycle when one is available.
    /// </summary>
    /// <param name="activeArtifact">The artifact associated with the launch.</param>
    /// <param name="launch">The external launch handle.</param>
    /// <param name="completion">The captured external lifecycle task, if available.</param>
    /// <remarks>
    /// An absent, faulted, or cancelled completion signal is treated as unobservable.
    /// In those cases the artifact remains retained for the separate recovery lifecycle.
    /// </remarks>
    private async Task ObserveExternalLaunchAsync(
        ActiveArtifact activeArtifact,
        IExternalDocumentLaunch launch,
        Task? completion)
    {
        try
        {
            if (completion is null)
            {
                return;
            }

            try
            {
                await completion.ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                // A failed observation does not establish that the consumer
                // has finished reading the temporary document.
                MarkExternalLifecycleUnobservable(activeArtifact);

                _logger.LogWarning(
                    "External document lifecycle observation failed. ArtifactId: {ArtifactId}; FailureCategory: {FailureCategory}",
                    activeArtifact.Artifact.Id,
                    exception.GetType().Name);

                return;
            }

            try
            {
                // Persist cleanup eligibility before attempting physical deletion.
                activeArtifact.MarkExternalLifecycleCompleted();
            }
            catch (Exception exception)
            {
                // If the durable transition cannot be committed, preserve the
                // external-consumer state and let recovery handle the artifact.
                MarkExternalLifecycleUnobservable(activeArtifact);

                _logger.LogWarning(
                    "External document completion could not be persisted. ArtifactId: {ArtifactId}; FailureCategory: {FailureCategory}",
                    activeArtifact.Artifact.Id,
                    exception.GetType().Name);

                return;
            }

            TryCleanupAndRemove(activeArtifact);
        }
        finally
        {
            DisposeLaunch(launch);
        }
    }

    /// <summary>
    /// Marks an external lifecycle as unobservable and releases ownership for recovery
    /// when the viewer has already been disposed.
    /// </summary>
    /// <param name="activeArtifact">The artifact whose external lifecycle is unobservable.</param>
    private void MarkExternalLifecycleUnobservable(
        ActiveArtifact activeArtifact)
    {
        lock (_lifecycleSync)
        {
            activeArtifact.SetExternalLifecycleObservable(false);

            if (_disposed)
            {
                ReleaseOwnershipForRecovery(activeArtifact);
            }
        }
    }

    /// <summary>
    /// Disposes the launch handle and records failures without exposing document paths.
    /// </summary>
    /// <param name="launch">The external launch handle to dispose.</param>
    private void DisposeLaunch(IExternalDocumentLaunch launch)
    {
        try
        {
            launch.Dispose();
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                "External document launch handle disposal failed. FailureCategory: {FailureCategory}",
                exception.GetType().Name);
        }
    }

    /// <summary>
    /// Marks a preparation or pre-invocation failure as cleanup-eligible and attempts cleanup.
    /// </summary>
    /// <param name="activeArtifact">The artifact whose preparation did not establish an external consumer.</param>
    private void MarkPreparationFailedAndCleanup(
        ActiveArtifact activeArtifact)
    {
        try
        {
            activeArtifact.MarkPreparationFailed();
        }
        catch (Exception exception)
        {
            // Preserve the original operation failure. If the durable transition
            // cannot be written, leave the artifact and metadata for recovery.
            _logger.LogWarning(
                "Temporary document artifact cleanup eligibility could not be persisted. ArtifactId: {ArtifactId}; FailureCategory: {FailureCategory}",
                activeArtifact.Artifact.Id,
                exception.GetType().Name);

            ReleaseOwnershipForRecovery(activeArtifact);

            return;
        }

        TryCleanupAndRemove(activeArtifact);
    }

    /// <summary>
    /// Attempts cleanup and removes ownership only after deletion succeeds.
    /// </summary>
    /// <param name="activeArtifact">The artifact whose cleanup is being attempted.</param>
    private void TryCleanupAndRemove(
        ActiveArtifact activeArtifact)
    {
        TemporaryDocumentArtifactCleanupResult result;

        try
        {
            result = activeArtifact.Artifact.TryCleanup();
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                "Temporary document artifact cleanup could not be completed. ArtifactId: {ArtifactId}; FailureCategory: {FailureCategory}",
                activeArtifact.Artifact.Id,
                exception.GetType().Name);

            return;
        }

        if (!result.Succeeded)
        {
            // Retain the registration so a later owner cleanup attempt can retry.
            _logger.LogWarning(
                "Temporary document artifact cleanup failed. ArtifactId: {ArtifactId}; FailureCategory: {FailureCategory}",
                activeArtifact.Artifact.Id,
                result.FailureCategory);

            return;
        }

        // Remove only the exact owner registration that was successfully cleaned.
        ((ICollection<KeyValuePair<Guid, ActiveArtifact>>)_activeArtifacts).Remove(
            new KeyValuePair<Guid, ActiveArtifact>(
                activeArtifact.Artifact.Id,
                activeArtifact));

        _unregisteredCleanupArtifacts.TryRemove(activeArtifact, out _);
    }

    /// <summary>
    /// Releases the process lease without deleting an artifact whose external
    /// consumer may still need it.
    /// </summary>
    /// <param name="activeArtifact">The artifact to leave for startup recovery.</param>
    private void ReleaseOwnershipForRecovery(
        ActiveArtifact activeArtifact)
    {
        lock (_lifecycleSync)
        {
            try
            {
                activeArtifact.ReleaseOwnershipLease();
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    "Temporary document artifact ownership lease release failed. ArtifactId: {ArtifactId}; FailureCategory: {FailureCategory}",
                    activeArtifact.Artifact.Id,
                    exception.GetType().Name);

                return;
            }

            // The durable record and artifact remain on disk. Remove only this
            // viewer's in-memory references after the lease has been released.
            ((ICollection<KeyValuePair<Guid, ActiveArtifact>>)_activeArtifacts).Remove(
                new KeyValuePair<Guid, ActiveArtifact>(
                    activeArtifact.Artifact.Id,
                    activeArtifact));

            _unregisteredCleanupArtifacts.TryRemove(activeArtifact, out _);
        }
    }

    /// <summary>
    /// Cleans artifacts whose external lifecycle permits owner disposal cleanup.
    /// </summary>
    /// <remarks>
    /// Preparing artifacts remain owned by their in-flight operation, which checks the
    /// disposed state before launch admission. Observable external lifecycles retain
    /// ownership until their completion task finishes.
    ///
    /// Unobservable external lifecycles retain their files and durable ownership records,
    /// but release the process lease so startup recovery can inspect them.
    ///
    /// Repeated disposal calls retry cleanup for artifacts whose prior deletion failed.
    /// </remarks>
    public void Dispose()
    {
        lock (_lifecycleSync)
        {
            _disposed = true;

            foreach (KeyValuePair<Guid, ActiveArtifact> entry in _activeArtifacts)
            {
                ActiveArtifact activeArtifact = entry.Value;

                if (activeArtifact.CanBeCleanedOnViewerDisposal)
                {
                    TryCleanupAndRemove(activeArtifact);
                }
                else if (activeArtifact.MustBePreservedForRecovery)
                {
                    ReleaseOwnershipForRecovery(activeArtifact);
                }
            }

            // Retry artifacts that could not enter the primary GUID registry.
            foreach (ActiveArtifact activeArtifact in _unregisteredCleanupArtifacts.Keys)
            {
                if (activeArtifact.CanBeCleanedOnViewerDisposal)
                {
                    TryCleanupAndRemove(activeArtifact);
                }
                else if (activeArtifact.MustBePreservedForRecovery)
                {
                    ReleaseOwnershipForRecovery(activeArtifact);
                }
            }
        }
    }

    /// <summary>
    /// Tracks one artifact's preparation and external-consumer lifecycle state.
    /// </summary>
    private sealed class ActiveArtifact
    {
        private const int Preparing = 0;
        private const int ObservableExternalLifecycle = 1;
        private const int UnobservableExternalLifecycle = 2;
        private const int ExternalLifecycleCompleted = 3;
        private const int PreparationFailed = 4;
        private const int LaunchInvocationInProgress = 5;

        private int _externalLifecycleState = Preparing;

        public ActiveArtifact(TemporaryDocumentArtifact artifact)
        {
            ArgumentNullException.ThrowIfNull(artifact);

            Artifact = artifact;
        }

        public TemporaryDocumentArtifact Artifact { get; }

        /// <summary>
        /// Gets whether viewer disposal may safely attempt artifact cleanup.
        /// </summary>
        public bool CanBeCleanedOnViewerDisposal
        {
            get
            {
                int state = Volatile.Read(ref _externalLifecycleState);

                return state is ExternalLifecycleCompleted
                    or PreparationFailed;
            }
        }

        /// <summary>
        /// Gets whether the artifact must be retained for startup recovery
        /// when this viewer is disposed.
        /// </summary>
        public bool MustBePreservedForRecovery =>
            Volatile.Read(ref _externalLifecycleState) ==
            UnobservableExternalLifecycle;

        /// <summary>
        /// Persists the conservative external-consumer state before launch admission.
        /// </summary>
        public void MarkExternalConsumerMayNeedArtifact()
        {
            Artifact.MarkExternalConsumerMayNeedArtifact();

            Volatile.Write(
                ref _externalLifecycleState,
                UnobservableExternalLifecycle);
        }

        public void SetExternalLifecycleObservable(bool isObservable)
        {
            Volatile.Write(
                ref _externalLifecycleState,
                isObservable
                    ? ObservableExternalLifecycle
                    : UnobservableExternalLifecycle);
        }

        /// <summary>
        /// Persists cleanup eligibility after confirmed external lifecycle completion.
        /// </summary>
        public void MarkExternalLifecycleCompleted()
        {
            Artifact.MarkCleanupEligible();

            Volatile.Write(
                ref _externalLifecycleState,
                ExternalLifecycleCompleted);
        }

        /// <summary>
        /// Persists cleanup eligibility when preparation or pre-invocation admission fails.
        /// </summary>
        public void MarkPreparationFailed()
        {
            Artifact.MarkCleanupEligible();

            Volatile.Write(
                ref _externalLifecycleState,
                PreparationFailed);
        }

        /// <summary>
        /// Releases the process lease while preserving the artifact and its durable record.
        /// </summary>
        public void ReleaseOwnershipLease()
        {
            Artifact.ReleaseOwnershipLease();
        }

        /// <summary>
        /// Persists the external-consumer state and marks the synchronous
        /// launcher invocation as unresolved until it returns an outcome.
        /// </summary>
        public void MarkExternalLaunchInvocationInProgress()
        {
            Artifact.MarkExternalConsumerMayNeedArtifact();

            Volatile.Write(
                ref _externalLifecycleState,
                LaunchInvocationInProgress);
        }
    }
}
