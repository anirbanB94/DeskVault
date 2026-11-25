using DeskVault.Application.Configurations;
using DeskVault.Application.Documents.Commands.ImportDocument;
using DeskVault.Application.Documents.Commands.RemoveDocument;
using DeskVault.Application.Documents.Queries.GetDocument;
using DeskVault.Application.Documents.Queries.OpenDocument;
using DeskVault.Application.Interfaces;
using DeskVault.Application.Security.Sessions;
using DeskVault.Domain.Documents;
using DeskVault.UI.Hosting;
using DeskVault.UI.Presenters;
using DeskVault.UI.Services.Document;
using DeskVault.UI.Services.Interfaces;
using DeskVault.UI.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;

namespace DeskVault.UI.Tests;

/// <summary>
/// Verifies cross-boundary document-content protection across presentation,
/// external viewing, session revocation and temporary-artifact recovery.
/// </summary>
public sealed class DocumentContentProtectionLifecycleIntegrationTests
{
    /// <summary>
    /// Sets the maximum duration for deterministic asynchronous test synchronization.
    /// </summary>
    private static readonly TimeSpan OperationTimeout =
        TimeSpan.FromSeconds(5);

    /// <summary>
    /// Verifies that an external artifact remains available when consumer
    /// completion is unobservable, while lock and recovery clear in-app
    /// content and preserve the canonical encrypted document.
    /// </summary>
    [Fact]
    public async Task UnobservableExternalHandoff_WhenSessionLocks_PreservesRequiredArtifactAndCanonicalContentWhileRecoveryCleansOnlyEligibleArtifacts()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        using var temporaryRoot = new TemporaryRoot();

        var artifactStore =
            new TemporaryDocumentArtifactStore(
                temporaryRoot.DirectoryPath,
                Guid.NewGuid);

        var launcher =
            new RecordingExternalDocumentLauncher();

        await using ProductionSecuritySessionTestHost testHost =
            ProductionSecuritySessionTestHost.Create(
                temporaryDocumentArtifactStore: artifactStore,
                externalDocumentLauncher: launcher);

        const string documentContent =
            """
            DeskVault integrated document-content protection regression.

            This content must remain protected by the in-app session.
            The canonical encrypted document must remain unchanged.
            """;

        ImportedDocument importedDocument =
            await InitializeHostAndImportDocumentAsync(
                testHost,
                documentContent,
                cancellationToken);

        IServiceProvider services =
            testHost.Services;

        var uiDispatch =
            new TaskCompletionSource<Action>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        string? presentedContent = null;

        Mock<IDocumentView> view =
            CreateDocumentView(
                uiDispatch,
                async (stream, _, token) =>
                {
                    presentedContent =
                        await ReadContentAsync(stream, token);
                },
                () => presentedContent = null);

        var trackingReader =
            new TrackingDocumentReader(
                services.GetRequiredService<IDocumentReader>());

        OpenDocumentHandler openDocumentHandler =
            CreateTrackingOpenDocumentHandler(
                services,
                trackingReader);

        var productionViewer =
            Assert.IsType<DocumentViewer>(
                services.GetRequiredService<IDocumentViewer>());

        var signallingViewer =
            new SignallingDocumentViewer(productionViewer);

        using var presenter =
            CreatePresenter(
                services,
                view,
                signallingViewer,
                openDocumentHandler);

        TemporaryDocumentArtifactRecoveryService recoveryService =
            services.GetRequiredService<
                TemporaryDocumentArtifactRecoveryService>();

        IApplicationSecuritySessionLifecycleService lifecycleService =
            services.GetRequiredService<
                IApplicationSecuritySessionLifecycleService>();

        DeskVaultStartupCoordinator startupCoordinator =
            services.GetRequiredService<DeskVaultStartupCoordinator>();

        // Act: open the imported document through the real encrypted reader
        // and the production document-workspace presenter.
        await presenter.OpenAsync(
            importedDocument.DocumentId,
            cancellationToken);

        // Assert: the in-app presentation received the decrypted content.
        Assert.Equal(
            documentContent,
            presentedContent);

        TrackingReadStream openedDocumentStream =
            Assert.IsType<TrackingReadStream>(
                trackingReader.LastOpenedStream);

        Assert.Equal(
            0,
            openedDocumentStream.DisposeCount);

        // Act: request external viewing. The controlled launcher returns
        // immediately with no trustworthy external-consumer completion task.
        view.Raise(
            documentView => documentView.OpenExternallyRequested += null,
            EventArgs.Empty);

        await signallingViewer.OpenCompleted.WaitAsync(
            OperationTimeout,
            cancellationToken);

        // Assert: launch admission completed, but consumer completion remains
        // unobservable. The temporary artifact must therefore remain available.
        string externalArtifactPath =
            Assert.IsType<string>(launcher.LaunchedFilePath);

        string externalOwnershipPath =
            $"{externalArtifactPath}.owner.json";

        Assert.Equal(
            documentContent,
            await File.ReadAllTextAsync(
                externalArtifactPath,
                cancellationToken));

        Assert.True(File.Exists(externalOwnershipPath));

        // Arrange an independent stale artifact whose owner has explicitly
        // released its lease and persisted cleanup eligibility.
        TemporaryDocumentArtifact staleArtifact =
            await CreateCleanupEligibleArtifactAsync(
                artifactStore,
                cancellationToken);

        string staleArtifactPath = staleArtifact.FilePath;
        string staleOwnershipPath = staleArtifact.OwnershipMetadataPath;

        // This unrelated file is outside the DeskVault-owned namespace.
        string unrelatedTemporaryFilePath =
            Path.Combine(
                Path.GetTempPath(),
                $"DeskVault.Unrelated.{Guid.NewGuid():N}.tmp");

        const string unrelatedTemporaryContent =
            "This unrelated operating-system temporary file must remain untouched.";

        try
        {
            await File.WriteAllTextAsync(
                unrelatedTemporaryFilePath,
                unrelatedTemporaryContent,
                cancellationToken);

            // Act: run production recovery while the external artifact's
            // ownership lease is still held by DocumentViewer.
            TemporaryDocumentArtifactRecoveryResult activeRecoveryResult =
                recoveryService.Recover();

            // Assert: only the explicitly stale DeskVault-owned artifact
            // is cleaned. The active external artifact and unrelated file remain.
            Assert.Equal(
                2,
                activeRecoveryResult.CandidateCount);

            Assert.Equal(
                1,
                activeRecoveryResult.CleanedCount);

            Assert.Equal(
                1,
                activeRecoveryResult.PreservedCount);

            Assert.Contains(
                activeRecoveryResult.Findings,
                finding =>
                    finding.Disposition ==
                    TemporaryDocumentArtifactRecoveryDisposition.PreservedActiveLease);

            Assert.Contains(
                activeRecoveryResult.Findings,
                finding =>
                    finding.ArtifactId == staleArtifact.Id &&
                    finding.Disposition ==
                    TemporaryDocumentArtifactRecoveryDisposition.Cleaned);

            Assert.False(File.Exists(staleArtifactPath));
            Assert.False(File.Exists(staleOwnershipPath));

            Assert.True(File.Exists(externalArtifactPath));
            Assert.True(File.Exists(externalOwnershipPath));

            Assert.Equal(
                unrelatedTemporaryContent,
                await File.ReadAllTextAsync(
                    unrelatedTemporaryFilePath,
                    cancellationToken));

            // Act: revoke protected access through the production session
            // lifecycle and execute the UI-dispatched presentation cleanup.
            ApplicationSecuritySessionSnapshot lockedSnapshot =
                lifecycleService.Lock();

            Assert.Equal(
                ApplicationSecuritySessionState.Locked,
                lockedSnapshot.State);

            Action clearPresentation =
                await uiDispatch.Task.WaitAsync(
                    OperationTimeout,
                    cancellationToken);

            clearPresentation();

            // The presenter must release its decrypted source stream.
            await openedDocumentStream.Disposed.WaitAsync(
                OperationTimeout,
                cancellationToken);

            // Assert: previously presented content is gone, while the
            // independent external artifact remains available.
            Assert.Null(presentedContent);

            Assert.Equal(
                1,
                openedDocumentStream.DisposeCount);

            Assert.True(File.Exists(externalArtifactPath));
            Assert.True(File.Exists(externalOwnershipPath));

            Assert.Equal(
                documentContent,
                await File.ReadAllTextAsync(
                    externalArtifactPath,
                    cancellationToken));

            // Act: dispose the viewer. An unobservable external consumer
            // must leave its durable artifact for conservative recovery.
            productionViewer.Dispose();
            productionViewer.Dispose();

            TemporaryDocumentArtifactRecoveryResult externalRecoveryResult =
                recoveryService.Recover();

            // Assert: the durable external-consumer state prevents deletion
            // even after the process ownership lease has been released.
            Assert.Equal(
                1,
                externalRecoveryResult.CandidateCount);

            Assert.Equal(
                1,
                externalRecoveryResult.PreservedCount);

            Assert.Equal(
                TemporaryDocumentArtifactRecoveryDisposition.PreservedExternalConsumer,
                Assert.Single(externalRecoveryResult.Findings).Disposition);

            Assert.True(File.Exists(externalArtifactPath));
            Assert.True(File.Exists(externalOwnershipPath));

            // Repeated recovery must remain conservative and idempotent.
            TemporaryDocumentArtifactRecoveryResult repeatedRecoveryResult =
                recoveryService.Recover();

            Assert.Equal(
                1,
                repeatedRecoveryResult.PreservedCount);

            Assert.True(File.Exists(externalArtifactPath));

            // Act: freshly authenticate after the lock.
            bool reauthenticated =
                await startupCoordinator.AuthenticateAsync(
                    cancellationToken);

            // Assert: authentication alone must not restore the old
            // presentation or its decrypted source stream.
            Assert.True(reauthenticated);
            Assert.Null(presentedContent);

            // The canonical encrypted artifact must be byte-for-byte unchanged
            // throughout viewing, external materialization, lock and recovery.
            byte[] canonicalArtifactAfterLifecycle =
                await File.ReadAllBytesAsync(
                    importedDocument.CanonicalArtifactPath,
                    cancellationToken);

            Assert.Equal(
                importedDocument.CanonicalArtifactBytes,
                canonicalArtifactAfterLifecycle);

            Assert.True(
                File.Exists(importedDocument.CanonicalArtifactPath));
        }
        finally
        {
            if (File.Exists(unrelatedTemporaryFilePath))
            {
                File.Delete(unrelatedTemporaryFilePath);
            }
        }
    }

    /// <summary>
    /// Verifies that an external launch failure cleans its temporary artifact
    /// and that subsequent session revocation clears the decrypted presentation.
    /// </summary>
    [Fact]
    public async Task ExternalLaunchFailure_CleansTemporaryArtifactAndSessionRevocationClearsDecryptedPresentation()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        using var temporaryRoot = new TemporaryRoot();

        var artifactStore =
            new TemporaryDocumentArtifactStore(
                temporaryRoot.DirectoryPath,
                Guid.NewGuid);

        var launcher =
            new RecordingExternalDocumentLauncher
            {
                LaunchException = new IOException(
                    "Simulated external launch failure.")
            };

        await using ProductionSecuritySessionTestHost testHost =
            ProductionSecuritySessionTestHost.Create(
                temporaryDocumentArtifactStore: artifactStore,
                externalDocumentLauncher: launcher);

        const string documentContent =
            """
            DeskVault external-launch failure regression.
            The failed launch must not leave an unmanaged plaintext artifact.
            """;

        ImportedDocument importedDocument =
            await InitializeHostAndImportDocumentAsync(
                testHost,
                documentContent,
                cancellationToken);

        IServiceProvider services =
            testHost.Services;

        var uiDispatch =
            new TaskCompletionSource<Action>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var errorShown =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        string? presentedContent = null;

        Mock<IDocumentView> view =
            CreateDocumentView(
                uiDispatch,
                async (stream, _, token) =>
                {
                    presentedContent =
                        await ReadContentAsync(stream, token);
                },
                () => presentedContent = null,
                () => errorShown.TrySetResult());

        var trackingReader =
            new TrackingDocumentReader(
                services.GetRequiredService<IDocumentReader>());

        OpenDocumentHandler openDocumentHandler =
            CreateTrackingOpenDocumentHandler(
                services,
                trackingReader);

        var productionViewer =
            Assert.IsType<DocumentViewer>(
                services.GetRequiredService<IDocumentViewer>());

        var signallingViewer =
            new SignallingDocumentViewer(productionViewer);

        using var presenter =
            CreatePresenter(
                services,
                view,
                signallingViewer,
                openDocumentHandler);

        IApplicationSecuritySessionLifecycleService lifecycleService =
            services.GetRequiredService<
                IApplicationSecuritySessionLifecycleService>();

        TemporaryDocumentArtifactRecoveryService recoveryService =
            services.GetRequiredService<
                TemporaryDocumentArtifactRecoveryService>();

        // Act: open and present the real decrypted document.
        await presenter.OpenAsync(
            importedDocument.DocumentId,
            cancellationToken);

        Assert.Equal(
            documentContent,
            presentedContent);

        TrackingReadStream openedDocumentStream =
            Assert.IsType<TrackingReadStream>(
                trackingReader.LastOpenedStream);

        // Act: request external viewing. The launcher fails before returning
        // a launch handle, so no external consumer was admitted.
        view.Raise(
            documentView => documentView.OpenExternallyRequested += null,
            EventArgs.Empty);

        await signallingViewer.OpenCompleted.WaitAsync(
            OperationTimeout,
            cancellationToken);

        await errorShown.Task.WaitAsync(
            OperationTimeout,
            cancellationToken);

        // Assert: the launcher was invoked but failed before handoff.
        Assert.Equal(1, launcher.LaunchCount);

        string failedArtifactPath =
            Assert.IsType<string>(launcher.LastLaunchedFilePath);

        Assert.False(File.Exists(failedArtifactPath));
        Assert.False(File.Exists($"{failedArtifactPath}.owner.json"));

        Assert.Empty(
            Directory.GetFiles(
                artifactStore.DirectoryPath,
                "*",
                SearchOption.AllDirectories));

        // The source stream remains owned by the active in-app presentation
        // until that presentation is invalidated.
        Assert.Equal(
            0,
            openedDocumentStream.DisposeCount);

        // Act: lock the session and dispatch presentation cleanup.
        lifecycleService.Lock();

        Action clearPresentation =
            await uiDispatch.Task.WaitAsync(
                OperationTimeout,
                cancellationToken);

        clearPresentation();

        await openedDocumentStream.Disposed.WaitAsync(
            OperationTimeout,
            cancellationToken);

        // Assert: in-app content and source stream are released.
        Assert.Null(presentedContent);

        Assert.Equal(
            1,
            openedDocumentStream.DisposeCount);

        // Recovery is safe to repeat after pre-handoff failure cleanup.
        TemporaryDocumentArtifactRecoveryResult firstRecovery =
            recoveryService.Recover();

        TemporaryDocumentArtifactRecoveryResult secondRecovery =
            recoveryService.Recover();

        Assert.Equal(0, firstRecovery.CandidateCount);
        Assert.Equal(0, secondRecovery.CandidateCount);

        // The original encrypted document remains unchanged.
        byte[] canonicalArtifactAfterFailure =
            await File.ReadAllBytesAsync(
                importedDocument.CanonicalArtifactPath,
                cancellationToken);

        Assert.Equal(
            importedDocument.CanonicalArtifactBytes,
            canonicalArtifactAfterFailure);

        Assert.True(
            File.Exists(importedDocument.CanonicalArtifactPath));

        productionViewer.Dispose();
        productionViewer.Dispose();
    }

    /// <summary>
    /// Verifies that cancellation during external materialization cleans
    /// temporary content and defers source-stream disposal until the
    /// outstanding external-viewing borrow has completed.
    /// </summary>
    [Fact]
    public async Task ExternalMaterializationCancellation_DefersSourceDisposalUntilBorrowCompletesAndLeavesNoTemporaryArtifact()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        using var temporaryRoot = new TemporaryRoot();

        var artifactStore =
            new TemporaryDocumentArtifactStore(
                temporaryRoot.DirectoryPath,
                Guid.NewGuid);

        var launcher =
            new RecordingExternalDocumentLauncher();

        await using ProductionSecuritySessionTestHost testHost =
            ProductionSecuritySessionTestHost.Create(
                temporaryDocumentArtifactStore: artifactStore,
                externalDocumentLauncher: launcher);

        const string canonicalContent =
            """
            DeskVault cancellation integration regression.
            The canonical encrypted file must not be changed by cancellation.
            """;

        ImportedDocument importedDocument =
            await InitializeHostAndImportDocumentAsync(
                testHost,
                canonicalContent,
                cancellationToken);

        IServiceProvider services =
            testHost.Services;

        var uiDispatch =
            new TaskCompletionSource<Action>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var blockingSourceStream =
            new BlockingSourceStream(
                Encoding.UTF8.GetBytes(
                    "Controlled plaintext stream used for cancellation regression."));

        string? presentedContent = null;

        Mock<IDocumentView> view =
            CreateDocumentView(
                uiDispatch,
                async (stream, _, token) =>
                {
                    presentedContent =
                        await ReadContentAsync(stream, token);

                    // The initial in-app presentation has consumed the stream.
                    // The next read belongs to external materialization and
                    // is held until the operation's cancellation token fires.
                    blockingSourceStream.BlockNextRead();
                },
                () => presentedContent = null);

        var productionViewer =
            Assert.IsType<DocumentViewer>(
                services.GetRequiredService<IDocumentViewer>());

        var signallingViewer =
            new SignallingDocumentViewer(productionViewer);

        using var presenter =
            CreatePresenter(
                services,
                view,
                signallingViewer,
                services.GetRequiredService<OpenDocumentHandler>());

        IApplicationSecuritySessionLifecycleService lifecycleService =
            services.GetRequiredService<
                IApplicationSecuritySessionLifecycleService>();

        TemporaryDocumentArtifactRecoveryService recoveryService =
            services.GetRequiredService<
                TemporaryDocumentArtifactRecoveryService>();

        // Act: present the controlled stream for an existing imported document.
        // The presenter owns the supplied stream after this call.
        await presenter.OpenAsync(
            importedDocument.DocumentId,
            blockingSourceStream,
            "cancellation-source.txt",
            cancellationToken);

        Assert.NotNull(presentedContent);

        // Start external materialization and wait until its source read is blocked.
        view.Raise(
            documentView => documentView.OpenExternallyRequested += null,
            EventArgs.Empty);

        await blockingSourceStream.ReadStarted.WaitAsync(
            OperationTimeout,
            cancellationToken);

        // Act: revoke access. The presenter cancels the outstanding external
        // operation, but must defer source-stream disposal while it is borrowed.
        ApplicationSecuritySessionSnapshot lockedSnapshot =
            lifecycleService.Lock();

        Assert.Equal(
            ApplicationSecuritySessionState.Locked,
            lockedSnapshot.State);

        Action clearPresentation =
            await uiDispatch.Task.WaitAsync(
                OperationTimeout,
                cancellationToken);

        clearPresentation();

        // The controlled read completes only through cancellation. Waiting
        // for source disposal proves the viewer borrow has completed and the
        // presenter has released its deferred ownership.
        await blockingSourceStream.Disposed.WaitAsync(
            OperationTimeout,
            cancellationToken);

        await signallingViewer.OpenCompleted.WaitAsync(
            OperationTimeout,
            cancellationToken);

        // Assert: the original in-app content is cleared and the owned source
        // stream is disposed exactly once after the outstanding borrow ends.
        Assert.Null(presentedContent);

        Assert.Equal(
            1,
            blockingSourceStream.DisposeCount);

        Assert.Equal(
            0,
            launcher.LaunchCount);

        // Cancellation occurred before launch admission. No artifact or
        // ownership metadata may remain in the dedicated namespace.
        Assert.Empty(
            Directory.GetFiles(
                artifactStore.DirectoryPath,
                "*",
                SearchOption.AllDirectories));

        TemporaryDocumentArtifactRecoveryResult recoveryResult =
            recoveryService.Recover();

        Assert.Equal(0, recoveryResult.CandidateCount);
        Assert.Equal(0, recoveryResult.CleanedCount);
        Assert.Equal(0, recoveryResult.PreservedCount);
        Assert.Equal(0, recoveryResult.FailedCount);

        // The canonical encrypted file is independent of the cancelled view.
        byte[] canonicalArtifactAfterCancellation =
            await File.ReadAllBytesAsync(
                importedDocument.CanonicalArtifactPath,
                cancellationToken);

        Assert.Equal(
            importedDocument.CanonicalArtifactBytes,
            canonicalArtifactAfterCancellation);

        Assert.True(
            File.Exists(importedDocument.CanonicalArtifactPath));

        productionViewer.Dispose();
        productionViewer.Dispose();
    }

    /// <summary>
    /// Starts and authenticates the production test host, initializes its encrypted vault,
    /// and imports a document through the production document-import pipeline.
    /// </summary>
    private static async Task<ImportedDocument> InitializeHostAndImportDocumentAsync(
        ProductionSecuritySessionTestHost testHost,
        string documentContent,
        CancellationToken cancellationToken)
    {
        await testHost.StartAsync(cancellationToken);

        IServiceProvider services =
            testHost.Services;

        DeskVaultStartupCoordinator startupCoordinator =
            services.GetRequiredService<DeskVaultStartupCoordinator>();

        bool authenticated =
            await startupCoordinator.AuthenticateAsync(
                cancellationToken);

        Assert.True(authenticated);

        await startupCoordinator.InitializeDatabaseAsync(
            cancellationToken);

        string sourceFilePath =
            Path.Combine(
                testHost.DataPaths.RootDirectory,
                $"content-protection-{Guid.NewGuid():N}.txt");

        await File.WriteAllTextAsync(
            sourceFilePath,
            documentContent,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken);

        ImportDocumentHandler importDocumentHandler =
            services.GetRequiredService<ImportDocumentHandler>();

        ImportDocumentResult importResult =
            await importDocumentHandler.HandleAsync(
                new ImportDocumentCommand(
                    sourceFilePath,
                    "Document Content Protection Integration Test"),
                cancellationToken);

        Assert.Equal(
            ImportDocumentResultStatus.Success,
            importResult.Status);

        Assert.NotNull(importResult.DocumentId);

        Guid documentId =
            importResult.DocumentId.Value;

        IDocumentRepository documentRepository =
            services.GetRequiredService<IDocumentRepository>();

        Document document =
            await documentRepository.GetByIdAsync(
                documentId,
                cancellationToken) ??
            throw new InvalidOperationException(
                "The imported document metadata could not be retrieved.");

        Assert.False(
            string.IsNullOrWhiteSpace(document.StoredFilePath));

        string canonicalArtifactPath =
            document.StoredFilePath;

        Assert.EndsWith(
            ".dvault",
            canonicalArtifactPath,
            StringComparison.OrdinalIgnoreCase);

        byte[] canonicalArtifactBytes =
            await File.ReadAllBytesAsync(
                canonicalArtifactPath,
                cancellationToken);

        Assert.NotEmpty(canonicalArtifactBytes);

        return new ImportedDocument(
            documentId,
            canonicalArtifactPath,
            canonicalArtifactBytes);
    }

    /// <summary>
    /// Creates an open-document handler using the production repository and processing
    /// options with the supplied tracking document reader.
    /// </summary>
    private static OpenDocumentHandler CreateTrackingOpenDocumentHandler(
        IServiceProvider services,
        TrackingDocumentReader trackingReader)
    {
        return new OpenDocumentHandler(
            services.GetRequiredService<IDocumentRepository>(),
            trackingReader,
            services.GetRequiredService<DocumentProcessingOptions>(),
            NullLogger<OpenDocumentHandler>.Instance);
    }

    /// <summary>
    /// Creates a document workspace presenter using production application handlers and
    /// the supplied document view and external document viewer.
    /// </summary>
    private static DocumentWorkspacePresenter CreatePresenter(
        IServiceProvider services,
        Mock<IDocumentView> view,
        IDocumentViewer documentViewer,
        OpenDocumentHandler openDocumentHandler)
    {
        return new DocumentWorkspacePresenter(
            view.Object,
            documentViewer,
            services.GetRequiredService<GetDocumentHandler>(),
            openDocumentHandler,
            services.GetRequiredService<RemoveDocumentHandler>(),
            NullLogger<DocumentWorkspacePresenter>.Instance,
            services.GetRequiredService<IApplicationSecuritySession>());
    }

    /// <summary>
    /// Creates a deterministic document-view mock for presentation, UI dispatch, cleanup
    /// and optional error-display operations.
    /// </summary>
    private static Mock<IDocumentView> CreateDocumentView(
        TaskCompletionSource<Action> uiDispatch,
        Func<Stream, string, CancellationToken, Task> showDocument,
        Action clearPresentation,
        Action? errorShown = null)
    {
        var view =
            new Mock<IDocumentView>();

        view
            .Setup(x => x.ShowDocumentAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(
                (
                    Stream stream,
                    string fileName,
                    CancellationToken cancellationToken) =>
                        showDocument(
                            stream,
                            fileName,
                            cancellationToken));

        view
            .Setup(x => x.PostToUi(
                It.IsAny<Action>()))
            .Callback<Action>(
                action => uiDispatch.TrySetResult(action));

        view
            .Setup(x => x.ClearPresentation())
            .Callback(clearPresentation);

        if (errorShown is not null)
        {
            view
                .Setup(x => x.ShowError(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<long>()))
                .Callback<string, string, long>(
                    (_, _, _) => errorShown());
        }

        return view;
    }

    /// <summary>
    /// Reads the complete stream and decodes it as UTF-8 without disposing the source stream.
    /// </summary>
    private static async Task<string> ReadContentAsync(
        Stream source,
        CancellationToken cancellationToken)
    {
        using var destination =
            new MemoryStream();

        await source.CopyToAsync(
            destination,
            cancellationToken);

        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetString(
            destination.ToArray());
    }

    /// <summary>
    /// Creates a temporary artifact, writes test content, marks it cleanup-eligible and
    /// releases its ownership lease so recovery can reconcile it.
    /// </summary>
    private static async Task<TemporaryDocumentArtifact>
        CreateCleanupEligibleArtifactAsync(
            TemporaryDocumentArtifactStore store,
            CancellationToken cancellationToken)
    {
        TemporaryDocumentArtifact artifact =
            store.CreateArtifact("stale-document.txt");

        using var source =
            new MemoryStream(
                Encoding.UTF8.GetBytes(
                    "An abandoned DeskVault temporary artifact."));

        await artifact.CopyFromAsync(
            source,
            cancellationToken);

        artifact.MarkCleanupEligible();
        artifact.ReleaseOwnershipLease();

        return artifact;
    }

    /// <summary>
    /// Captures the imported document identity and the canonical encrypted artifact snapshot.
    /// </summary>
    /// <param name="DocumentId">The identifier assigned to the imported document.</param>
    /// <param name="CanonicalArtifactPath">The path of the canonical encrypted document artifact.</param>
    /// <param name="CanonicalArtifactBytes">The original bytes of the canonical encrypted artifact.</param>
    private sealed record ImportedDocument(
        Guid DocumentId,
        string CanonicalArtifactPath,
        byte[] CanonicalArtifactBytes);

    /// <summary>
    /// Wraps the production document reader and records disposal of the
    /// actual decrypted stream returned by its encrypted-storage path.
    /// </summary>
    private sealed class TrackingDocumentReader : IDocumentReader
    {
        private readonly IDocumentReader _inner;

        /// <summary>
        /// Initializes a tracking reader that delegates to the supplied production reader.
        /// </summary>
        public TrackingDocumentReader(IDocumentReader inner)
        {
            ArgumentNullException.ThrowIfNull(inner);

            _inner = inner;
        }

        /// <summary>
        /// Gets the most recently opened decrypted stream wrapped for disposal tracking.
        /// </summary>
        public TrackingReadStream? LastOpenedStream { get; private set; }

        /// <summary>
        /// Opens document content through the production reader and wraps the returned
        /// decrypted stream to observe its disposal.
        /// </summary>
        public async Task<Stream> OpenReadAsync(
            Guid documentId,
            string expectedSha256Hash,
            long maximumPlaintextBytes,
            CancellationToken cancellationToken = default)
        {
            Stream source =
                await _inner.OpenReadAsync(
                    documentId,
                    expectedSha256Hash,
                    maximumPlaintextBytes,
                    cancellationToken);

            var trackedStream =
                new TrackingReadStream(source);

            LastOpenedStream = trackedStream;

            return trackedStream;
        }

        /// <summary>
        /// Delegates document-content hash validation to the production reader.
        /// </summary>
        public Task ValidateContentHashAsync(
            Guid documentId,
            string expectedSha256Hash,
            long maximumPlaintextBytes,
            CancellationToken cancellationToken = default)
        {
            return _inner.ValidateContentHashAsync(
                documentId,
                expectedSha256Hash,
                maximumPlaintextBytes,
                cancellationToken);
        }
    }

    private sealed class TrackingReadStream : Stream
    {
        private readonly Stream _inner;

        private readonly TaskCompletionSource _disposed =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private int _disposeCount;

        /// <summary>
        /// Initializes a tracking wrapper around a decrypted document stream.
        /// </summary>
        public TrackingReadStream(Stream inner)
        {
            ArgumentNullException.ThrowIfNull(inner);

            _inner = inner;
        }

        /// <summary>
        /// Gets the number of times disposal of the wrapped source stream has been initiated.
        /// </summary>
        public int DisposeCount =>
            Volatile.Read(ref _disposeCount);

        /// <summary>
        /// Gets a task that completes when the wrapped source stream has been disposed.
        /// </summary>
        public Task Disposed =>
            _disposed.Task;

        /// <inheritdoc />
        public override bool CanRead =>
            _inner.CanRead;

        /// <inheritdoc />
        public override bool CanSeek =>
            _inner.CanSeek;

        /// <inheritdoc />
        public override bool CanWrite =>
            _inner.CanWrite;

        /// <inheritdoc />
        public override long Length =>
            _inner.Length;

        /// <inheritdoc />
        public override long Position
        {
            get => _inner.Position;
            set => _inner.Position = value;
        }

        /// <inheritdoc />
        public override void Flush() =>
            _inner.Flush();

        /// <inheritdoc />
        public override int Read(
            byte[] buffer,
            int offset,
            int count) =>
            _inner.Read(buffer, offset, count);

        /// <inheritdoc />
        public override int Read(Span<byte> buffer) =>
            _inner.Read(buffer);

        /// <inheritdoc />
        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken) =>
            _inner.ReadAsync(
                buffer,
                offset,
                count,
                cancellationToken);

        /// <inheritdoc />
        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) =>
            _inner.ReadAsync(
                buffer,
                cancellationToken);

        /// <inheritdoc />
        public override long Seek(
            long offset,
            SeekOrigin origin) =>
            _inner.Seek(offset, origin);

        /// <inheritdoc />
        public override void SetLength(long value) =>
            _inner.SetLength(value);

        /// <inheritdoc />
        public override void Write(
            byte[] buffer,
            int offset,
            int count) =>
            _inner.Write(buffer, offset, count);

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing &&
                Interlocked.Exchange(ref _disposeCount, 1) == 0)
            {
                try
                {
                    _inner.Dispose();
                }
                finally
                {
                    _disposed.TrySetResult();
                }
            }

            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Provides a deterministic external-launch boundary. A successful launch
    /// returns a handle with no consumer-completion signal; a configured failure
    /// is thrown before a handle is returned.
    /// </summary>
    private sealed class RecordingExternalDocumentLauncher :
        IExternalDocumentLauncher
    {
        /// <summary>
        /// Gets the number of external launch requests received.
        /// </summary>
        public int LaunchCount { get; private set; }

        /// <summary>
        /// Gets the path supplied to the most recent successful external launch.
        /// </summary>
        public string? LaunchedFilePath { get; private set; }

        /// <summary>
        /// Gets the path supplied to the most recent external launch attempt, including failures.
        /// </summary>
        public string? LastLaunchedFilePath { get; private set; }

        /// <summary>
        /// Gets or initializes the exception thrown when an external launch is attempted.
        /// </summary>
        public Exception? LaunchException { get; init; }

        /// <summary>
        /// Records the launch request and either throws the configured failure or returns
        /// a handle whose external-consumer completion cannot be observed.
        /// </summary>
        public IExternalDocumentLaunch Launch(string filePath)
        {
            LaunchCount++;

            LastLaunchedFilePath = filePath;

            if (LaunchException is not null)
            {
                throw LaunchException;
            }

            LaunchedFilePath = filePath;

            return new UnobservableExternalDocumentLaunch();
        }
    }

    /// <summary>
    /// Represents a successful external launch for which consumer completion is unobservable.
    /// </summary>
    private sealed class UnobservableExternalDocumentLaunch :
        IExternalDocumentLaunch
    {
        /// <summary>
        /// Gets the external-consumer completion task, which is unavailable for this launch.
        /// </summary>
        public Task? Completion => null;

        /// <summary>
        /// Releases the test launch handle without terminating or making assumptions about
        /// the external consumer.
        /// </summary>
        public void Dispose()
        {
        }
    }

    /// <summary>
    /// Signals when the presenter's external-viewing call has returned from
    /// the real DocumentViewer, allowing tests to synchronize without delays.
    /// </summary>
    private sealed class SignallingDocumentViewer : IDocumentViewer
    {
        private readonly IDocumentViewer _inner;

        private readonly TaskCompletionSource _openCompleted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Initializes a wrapper around the production viewer that exposes deterministic
        /// operation-completion signalling to the tests.
        /// </summary>
        public SignallingDocumentViewer(IDocumentViewer inner)
        {
            ArgumentNullException.ThrowIfNull(inner);

            _inner = inner;
        }

        /// <summary>
        /// Gets a task that completes when the wrapped external-viewing operation returns.
        /// </summary>
        public Task OpenCompleted =>
            _openCompleted.Task;

        /// <summary>
        /// Delegates external materialization to the production viewer and signals when it
        /// returns, including failure and cancellation outcomes.
        /// </summary>
        public async Task OpenAsync(
            Stream documentStream,
            string fileName,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await _inner.OpenAsync(
                    documentStream,
                    fileName,
                    cancellationToken);
            }
            finally
            {
                _openCompleted.TrySetResult();
            }
        }
    }

    /// <summary>
    /// Blocks one selected read until the caller's cancellation token fires.
    /// Initial in-app rendering remains deterministic and completes normally.
    /// </summary>
    private sealed class BlockingSourceStream : Stream
    {
        private readonly MemoryStream _inner;

        private readonly TaskCompletionSource _readStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource _disposed =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private int _blockNextRead;
        private int _disposeCount;

        /// <summary>
        /// Initializes a read-only source stream over the supplied document content.
        /// </summary>
        public BlockingSourceStream(byte[] content)
        {
            ArgumentNullException.ThrowIfNull(content);

            _inner = new MemoryStream(content, writable: false);
        }

        /// <summary>
        /// Gets a task that completes when the controlled read begins.
        /// </summary>
        public Task ReadStarted =>
            _readStarted.Task;

        /// <summary>
        /// Gets a task that completes when the controlled source stream is disposed.
        /// </summary>
        public Task Disposed =>
            _disposed.Task;

        /// <summary>
        /// Gets the number of times disposal of the controlled source stream has been initiated.
        /// </summary>
        public int DisposeCount =>
            Volatile.Read(ref _disposeCount);

        /// <summary>
        /// Configures the next asynchronous read to remain pending until its cancellation token
        /// is signalled.
        /// </summary>
        public void BlockNextRead()
        {
            Volatile.Write(
                ref _blockNextRead,
                1);
        }

        /// <inheritdoc />
        public override bool CanRead =>
            _inner.CanRead;

        /// <inheritdoc />
        public override bool CanSeek =>
            _inner.CanSeek;

        /// <inheritdoc />
        public override bool CanWrite => false;

        /// <inheritdoc />
        public override long Length =>
            _inner.Length;

        /// <inheritdoc />
        public override long Position
        {
            get => _inner.Position;
            set => _inner.Position = value;
        }

        /// <inheritdoc />
        public override void Flush()
        {
        }

        /// <inheritdoc />
        public override int Read(
            byte[] buffer,
            int offset,
            int count) =>
            _inner.Read(buffer, offset, count);

        /// <inheritdoc />
        public override int Read(Span<byte> buffer) =>
            _inner.Read(buffer);

        /// <inheritdoc />
        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken) =>
            ReadAsync(
                buffer.AsMemory(offset, count),
                cancellationToken).AsTask();

        /// <inheritdoc />
        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(
                    ref _blockNextRead,
                    0) == 1)
            {
                _readStarted.TrySetResult();

                return WaitForCancellationAsync(
                    cancellationToken);
            }

            return _inner.ReadAsync(
                buffer,
                cancellationToken);
        }

        /// <summary>
        /// Keeps an asynchronous read pending until cancellation is requested.
        /// </summary>
        private static async ValueTask<int> WaitForCancellationAsync(
            CancellationToken cancellationToken)
        {
            await Task.Delay(
                Timeout.InfiniteTimeSpan,
                cancellationToken);

            return 0;
        }

        /// <inheritdoc />
        public override long Seek(
            long offset,
            SeekOrigin origin) =>
            _inner.Seek(offset, origin);

        /// <inheritdoc />
        public override void SetLength(long value) =>
            throw new NotSupportedException();

        /// <inheritdoc />
        public override void Write(
            byte[] buffer,
            int offset,
            int count) =>
            throw new NotSupportedException();

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing &&
                Interlocked.Exchange(ref _disposeCount, 1) == 0)
            {
                try
                {
                    _inner.Dispose();
                }
                finally
                {
                    _disposed.TrySetResult();
                }
            }

            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Owns an isolated filesystem root and normalizes restrictive production
    /// ACLs only during test teardown.
    /// </summary>
    private sealed class TemporaryRoot : IDisposable
    {
        /// <summary>
        /// Creates an isolated temporary directory for lifecycle integration-test artifacts.
        /// </summary>
        public TemporaryRoot()
        {
            DirectoryPath =
                Path.Combine(
                    Path.GetTempPath(),
                    $"DeskVault.ContentProtectionTests.{Guid.NewGuid():N}");

            Directory.CreateDirectory(DirectoryPath);
        }

        /// <summary>
        /// Gets the path of the isolated temporary test directory.
        /// </summary>
        public string DirectoryPath { get; }

        /// <summary>
        /// Restores cleanup permissions on the isolated test filesystem and recursively removes
        /// the temporary directory.
        /// </summary>
        public void Dispose()
        {
            if (!Directory.Exists(DirectoryPath))
            {
                return;
            }

            string[] filePaths =
            [
                .. Directory.GetFiles(
                    DirectoryPath,
                    "*",
                    SearchOption.AllDirectories)
            ];

            string[] directoryPaths =
            [
                .. Directory.GetDirectories(
                    DirectoryPath,
                    "*",
                    SearchOption.AllDirectories)
                    .OrderByDescending(path => path.Length)
            ];

            foreach (string filePath in filePaths)
            {
                GrantFixtureFileCleanupAccess(filePath);
            }

            foreach (string directoryPath in directoryPaths)
            {
                GrantFixtureDirectoryCleanupAccess(directoryPath);
            }

            GrantFixtureDirectoryCleanupAccess(DirectoryPath);

            Directory.Delete(
                DirectoryPath,
                recursive: true);
        }

        /// <summary>
        /// Grants the current test process cleanup permissions on an isolated fixture file
        /// without changing production ACL policy.
        /// </summary>
        private static void GrantFixtureFileCleanupAccess(
            string filePath)
        {
            using WindowsIdentity identity =
                WindowsIdentity.GetCurrent();

            SecurityIdentifier currentUserSid =
                identity.User ??
                throw new InvalidOperationException(
                    "The current Windows user identity is unavailable.");

            var security =
                new FileSecurity();

            security.SetAccessRuleProtection(
                isProtected: true,
                preserveInheritance: false);

            security.AddAccessRule(
                new FileSystemAccessRule(
                    currentUserSid,
                    FileSystemRights.FullControl,
                    AccessControlType.Allow));

            FileSystemAclExtensions.SetAccessControl(
                new FileInfo(filePath),
                security);
        }

        /// <summary>
        /// Grants the current test process cleanup permissions on an isolated fixture directory
        /// without changing production ACL policy.
        /// </summary>
        private static void GrantFixtureDirectoryCleanupAccess(
            string directoryPath)
        {
            using WindowsIdentity identity =
                WindowsIdentity.GetCurrent();

            SecurityIdentifier currentUserSid =
                identity.User ??
                throw new InvalidOperationException(
                    "The current Windows user identity is unavailable.");

            var security =
                new DirectorySecurity();

            security.SetAccessRuleProtection(
                isProtected: true,
                preserveInheritance: false);

            security.AddAccessRule(
                new FileSystemAccessRule(
                    currentUserSid,
                    FileSystemRights.FullControl,
                    AccessControlType.Allow));

            FileSystemAclExtensions.SetAccessControl(
                new DirectoryInfo(directoryPath),
                security);
        }
    }
}
