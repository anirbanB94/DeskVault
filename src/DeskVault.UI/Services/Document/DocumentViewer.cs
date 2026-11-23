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
            activeArtifact.MarkPreparationFailed();

            lock (_lifecycleSync)
            {
                _unregisteredCleanupArtifacts[activeArtifact] = 0;
                TryCleanupAndRemove(activeArtifact);
            }

            throw new InvalidOperationException(
                "The temporary document artifact identity is already active.");
        }

        IExternalDocumentLaunch? launch = null;
        bool launchOwnershipTransferred = false;
        bool externalLaunchStarted = false;

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
            // to viewer disposal. Disposal cannot observe a preparing artifact and
            // then race past a launch that has already been admitted.
            lock (_lifecycleSync)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);

                launch = _externalDocumentLauncher.Launch(
                    artifact.FilePath);

                externalLaunchStarted = true;

                // Until the completion contract is successfully inspected, retain the
                // artifact conservatively as an unobservable external lifecycle.
                activeArtifact.SetExternalLifecycleObservable(false);

                completion = launch.Completion;

                activeArtifact.SetExternalLifecycleObservable(
                    completion is not null);
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

            if (!externalLaunchStarted)
            {
                // Copy or launch creation failed before an external launch was established.
                // Mark the completed preparation as eligible for owner-disposal retry.
                activeArtifact.MarkPreparationFailed();

                TryCleanupAndRemove(activeArtifact);
            }
            else
            {
                // An external launch was established, but its lifecycle could not be
                // transferred to the observer. Do not risk deleting content that may
                // still be consumed by the external application.
                _logger.LogWarning(
                    "External document lifecycle could not be established. ArtifactId: {ArtifactId}; Artifact retained until viewer disposal.",
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
                activeArtifact.SetExternalLifecycleObservable(false);

                _logger.LogWarning(
                    "External document lifecycle observation failed. ArtifactId: {ArtifactId}; FailureCategory: {FailureCategory}",
                    activeArtifact.Artifact.Id,
                    exception.GetType().Name);

                return;
            }

            activeArtifact.MarkExternalLifecycleCompleted();

            TryCleanupAndRemove(activeArtifact);
        }
        finally
        {
            DisposeLaunch(launch);
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
    /// Attempts cleanup and removes ownership only after deletion succeeds.
    /// </summary>
    /// <param name="activeArtifact">The artifact whose cleanup is being attempted.</param>
    private void TryCleanupAndRemove(
        ActiveArtifact activeArtifact)
    {
        TemporaryDocumentArtifactCleanupResult result =
            activeArtifact.Artifact.TryCleanup();

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
    /// Cleans artifacts whose external lifecycle permits owner disposal cleanup.
    /// </summary>
    /// <remarks>
    /// Preparing artifacts remain owned by their in-flight operation, which checks the
    /// disposed state before launch admission. Observable external lifecycles retain
    /// ownership until their completion task finishes.
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

                if (!activeArtifact.CanBeCleanedOnViewerDisposal)
                {
                    continue;
                }

                TryCleanupAndRemove(activeArtifact);
            }

            // Retry artifacts that could not enter the primary GUID registry.
            foreach (ActiveArtifact activeArtifact in _unregisteredCleanupArtifacts.Keys)
            {
                if (!activeArtifact.CanBeCleanedOnViewerDisposal)
                {
                    continue;
                }

                TryCleanupAndRemove(activeArtifact);
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
        /// <remarks>
        /// Preparation failures and successfully completed external lifecycles
        /// permit cleanup. An unobservable or active external lifecycle does not.
        /// </remarks>
        public bool CanBeCleanedOnViewerDisposal
        {
            get
            {
                int state = Volatile.Read(ref _externalLifecycleState);

                return state is ExternalLifecycleCompleted
                    or PreparationFailed;
            }
        }

        public void SetExternalLifecycleObservable(bool isObservable)
        {
            Volatile.Write(
                ref _externalLifecycleState,
                isObservable
                    ? ObservableExternalLifecycle
                    : UnobservableExternalLifecycle);
        }

        public void MarkExternalLifecycleCompleted()
        {
            Volatile.Write(
                ref _externalLifecycleState,
                ExternalLifecycleCompleted);
        }

        /// <summary>
        /// Marks materialization as failed before an external consumer was established.
        /// </summary>
        public void MarkPreparationFailed()
        {
            Volatile.Write(
                ref _externalLifecycleState,
                PreparationFailed);
        }
    }
}
