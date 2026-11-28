using DeskVault.Application.Documents.Commands.ImportDocument;
using DeskVault.Application.Documents.Commands.RecoverDocumentArtifacts;
using DeskVault.Application.Documents.Commands.RemoveDocument;
using DeskVault.Application.Documents.Queries.ReconcileDocumentArtifacts;
using DeskVault.Application.Interfaces;
using DeskVault.Domain.Documents;
using DeskVault.Domain.Workspaces;
using System.Security.Cryptography;

namespace DeskVault.Integration.Tests;

public sealed class DocumentArtifactRecoveryIntegrationTests
{

    [Fact]
    public async Task RecoverAsync_WhenImportIsStillPublishingDocument_WaitsForImportAndPreservesArtifact()
    {
        // Arrange
        string rootDirectory =
            Path.Combine(
                Path.GetTempPath(),
                "DeskVaultIntegrationTests",
                Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(rootDirectory);

        string databasePath =
            Path.Combine(
                rootDirectory,
                "DeskVault.db");

        byte[] encryptionKey =
            RandomNumberGenerator.GetBytes(32);

        string sourceFilePath =
            Path.Combine(
                rootDirectory,
                "concurrent-import-recovery.txt");

        var artifactStored =
            new TaskCompletionSource<StoredArtifact>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var releaseStorage =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var recoveryAcquireRequested =
            new TaskCompletionSource<Guid>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        Task<ImportDocumentResult>? importTask = null;

        Task<RecoverDocumentArtifactsResult>? recoveryTask = null;

        try
        {
            await File.WriteAllTextAsync(
                sourceFilePath,
                "Document artifact import and recovery coordination test.",
                TestContext.Current.CancellationToken);

            // These are separate harnesses and coordinator instances,
            // but both resolve coordination resources for the same vault.
            await using var importHarness =
                new DocumentPipelineTestHarness(
                    rootDirectory,
                    databasePath,
                    encryptionKey,
                    importStorageDecorator:
                        storage =>
                            new BlockingImportStorageService(
                                storage,
                                artifactStored,
                                releaseStorage));

            await using var recoveryHarness =
                new DocumentPipelineTestHarness(
                    rootDirectory,
                    databasePath,
                    encryptionKey,
                    artifactCoordinatorDecorator:
                        coordinator =>
                            new SignalingDocumentArtifactCoordinator(
                                coordinator,
                                recoveryAcquireRequested));

            try
            {
                // Act: pause the import after the real encrypted artifact
                // has been stored but before StoreAsync returns to the handler.
                importTask =
                    importHarness.ImportHandler.HandleAsync(
                        new ImportDocumentCommand(
                            sourceFilePath,
                            "Concurrent Import Recovery Test"),
                        TestContext.Current.CancellationToken);

                StoredArtifact storedArtifact =
                    await artifactStored.Task.WaitAsync(
                        TimeSpan.FromSeconds(15),
                        TestContext.Current.CancellationToken);

                Assert.True(
                    File.Exists(storedArtifact.StoredFilePath));

                // At this point storage has completed, but metadata publication
                // has not started because the import handler is still awaiting StoreAsync.
                Assert.Null(
                    await importHarness.GetDocumentAsync(
                        storedArtifact.DocumentId));

                // Start recovery through a second, independently constructed
                // coordinator instance for the same vault.
                recoveryTask =
                    recoveryHarness.RecoveryHandler.HandleAsync(
                        new RecoverDocumentArtifactsCommand(),
                        TestContext.Current.CancellationToken);

                Guid requestedDocumentId =
                    await recoveryAcquireRequested.Task.WaitAsync(
                        TimeSpan.FromSeconds(15),
                        TestContext.Current.CancellationToken);

                Assert.Equal(
                    storedArtifact.DocumentId,
                    requestedDocumentId);

                // Recovery has reached real lease acquisition. It must remain
                // blocked until the import finishes its protected lifecycle.
                Assert.False(
                    recoveryTask.IsCompleted);

                releaseStorage.TrySetResult(true);

                // Assert: import publishes metadata and completes successfully.
                ImportDocumentResult importResult =
                    await importTask;

                Assert.Equal(
                    ImportDocumentResultStatus.Success,
                    importResult.Status);

                Assert.Equal(
                    storedArtifact.DocumentId,
                    importResult.DocumentId);

                Document? persistedDocument =
                    await importHarness.GetDocumentAsync(
                        storedArtifact.DocumentId);

                Assert.NotNull(persistedDocument);

                Assert.True(
                    File.Exists(
                        storedArtifact.StoredFilePath));

                // Recovery acquires the lease only after import releases it.
                // Its authoritative metadata recheck must invalidate the stale
                // orphan finding and preserve the newly published document.
                RecoverDocumentArtifactsResult recoveryResult =
                    await recoveryTask;

                Assert.Equal(
                    RecoverDocumentArtifactsResultStatus.PreservedForRecovery,
                    recoveryResult.Status);

                DocumentArtifactReconciliationResult finding =
                    Assert.Single(
                        recoveryResult.Findings);

                Assert.Equal(
                    storedArtifact.DocumentId,
                    finding.DocumentId);

                Assert.Equal(
                    DocumentArtifactReconciliationStatus.OrphanedArtifact,
                    finding.Status);

                Assert.Equal(
                    DocumentArtifactReconciliationRecoveryAction.PreserveForRecovery,
                    finding.RecoveryAction);

                Assert.True(
                    File.Exists(
                        storedArtifact.StoredFilePath));

                Assert.NotNull(
                    await recoveryHarness.GetDocumentAsync(
                        storedArtifact.DocumentId));
            }
            finally
            {
                // Always release the storage barrier so a failed assertion
                // cannot leave an import operation waiting during disposal.
                releaseStorage.TrySetResult(true);

                if (importTask is not null)
                {
                    try
                    {
                        await importTask.WaitAsync(
                            TimeSpan.FromSeconds(15),
                            TestContext.Current.CancellationToken);
                    }
                    catch (Exception)
                    {
                        // Preserve the primary test failure while observing
                        // the operation during cleanup.
                    }
                }

                if (recoveryTask is not null)
                {
                    try
                    {
                        await recoveryTask.WaitAsync(
                            TimeSpan.FromSeconds(15),
                            TestContext.Current.CancellationToken);
                    }
                    catch (Exception)
                    {
                        // Preserve the primary test failure while observing
                        // the operation during cleanup.
                    }
                }
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                encryptionKey);

            if (Directory.Exists(rootDirectory))
            {
                Directory.Delete(
                    rootDirectory,
                    recursive: true);
            }
        }
    }

    [Fact]
    public async Task RecoverAsync_WhenImportCompensationFails_WaitsForCompensationAndRemovesRemainingOrphan()
    {
        // Arrange
        string rootDirectory =
            Path.Combine(
                Path.GetTempPath(),
                "DeskVaultIntegrationTests",
                Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(rootDirectory);

        string databasePath =
            Path.Combine(
                rootDirectory,
                "DeskVault.db");

        byte[] encryptionKey =
            RandomNumberGenerator.GetBytes(32);

        var persistenceException =
            new InvalidOperationException(
                "Injected metadata persistence failure.");

        var artifactStored =
            new TaskCompletionSource<StoredArtifact>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var compensationStarted =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var releaseCompensation =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var recoveryAcquireRequested =
            new TaskCompletionSource<Guid>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        Task<ImportDocumentResult>? importTask = null;

        Task<RecoverDocumentArtifactsResult>? recoveryTask = null;

        try
        {
            string sourceFilePath =
                Path.Combine(
                    rootDirectory,
                    "failed-compensation-import.txt");

            await File.WriteAllTextAsync(
                sourceFilePath,
                "Document artifact failed compensation recovery test.",
                TestContext.Current.CancellationToken);

            await using var importHarness =
                new DocumentPipelineTestHarness(
                    rootDirectory,
                    databasePath,
                    encryptionKey,
                    importStorageDecorator:
                        storage =>
                            new FailingCompensationStorageService(
                                storage,
                                artifactStored,
                                compensationStarted,
                                releaseCompensation),
                    importRepositoryDecorator:
                        repository =>
                            new FailingImportRepository(
                                repository,
                                persistenceException));

            await using var recoveryHarness =
                new DocumentPipelineTestHarness(
                    rootDirectory,
                    databasePath,
                    encryptionKey,
                    artifactCoordinatorDecorator:
                        coordinator =>
                            new SignalingDocumentArtifactCoordinator(
                                coordinator,
                                recoveryAcquireRequested));

            try
            {
                // Start import. Metadata publication will fail, and its
                // compensation attempt will pause before returning.
                importTask =
                    importHarness.ImportHandler.HandleAsync(
                        new ImportDocumentCommand(
                            sourceFilePath,
                            "Failed Compensation Import"),
                        TestContext.Current.CancellationToken);

                StoredArtifact storedArtifact =
                    await artifactStored.Task.WaitAsync(
                        TimeSpan.FromSeconds(15),
                        TestContext.Current.CancellationToken);

                await compensationStarted.Task.WaitAsync(
                    TimeSpan.FromSeconds(15),
                    TestContext.Current.CancellationToken);

                Assert.True(
                    File.Exists(
                        storedArtifact.StoredFilePath));

                Assert.Null(
                    await importHarness.GetDocumentAsync(
                        storedArtifact.DocumentId));

                // Recovery must not delete the artifact while compensation
                // is still executing under the import lease.
                recoveryTask =
                    recoveryHarness.RecoveryHandler.HandleAsync(
                        new RecoverDocumentArtifactsCommand(),
                        TestContext.Current.CancellationToken);

                Guid requestedDocumentId =
                    await recoveryAcquireRequested.Task.WaitAsync(
                        TimeSpan.FromSeconds(15),
                        TestContext.Current.CancellationToken);

                Assert.Equal(
                    storedArtifact.DocumentId,
                    requestedDocumentId);

                Assert.False(
                    recoveryTask.IsCompleted);

                // Allow the compensation attempt to fail. The import handler
                // must catch that cleanup failure and release its lease.
                releaseCompensation.TrySetResult(true);

                InvalidOperationException actualException =
                    await Assert.ThrowsAsync<InvalidOperationException>(
                        () => importTask);

                Assert.Same(
                    persistenceException,
                    actualException);

                Assert.True(
                    File.Exists(
                        storedArtifact.StoredFilePath));

                // Recovery now acquires the released lease, revalidates
                // that metadata is absent, and removes the remaining orphan.
                RecoverDocumentArtifactsResult recoveryResult =
                    await recoveryTask;

                Assert.Equal(
                    RecoverDocumentArtifactsResultStatus.Recovered,
                    recoveryResult.Status);

                DocumentArtifactReconciliationResult finding =
                    Assert.Single(
                        recoveryResult.Findings);

                Assert.Equal(
                    storedArtifact.DocumentId,
                    finding.DocumentId);

                Assert.Equal(
                    DocumentArtifactReconciliationStatus.OrphanedArtifact,
                    finding.Status);

                Assert.Equal(
                    DocumentArtifactReconciliationRecoveryAction.CleanupOrphanedArtifact,
                    finding.RecoveryAction);

                Assert.False(
                    File.Exists(
                        storedArtifact.StoredFilePath));

                Assert.Null(
                    await recoveryHarness.GetDocumentAsync(
                        storedArtifact.DocumentId));
            }
            finally
            {
                // Release the test barrier even when an assertion fails.
                releaseCompensation.TrySetResult(true);

                if (importTask is not null)
                {
                    try
                    {
                        await importTask.WaitAsync(
                            TimeSpan.FromSeconds(15),
                            TestContext.Current.CancellationToken);
                    }
                    catch (Exception)
                    {
                        // Observe the operation without hiding the primary test failure.
                    }
                }

                if (recoveryTask is not null)
                {
                    try
                    {
                        await recoveryTask.WaitAsync(
                            TimeSpan.FromSeconds(15),
                            TestContext.Current.CancellationToken);
                    }
                    catch (Exception)
                    {
                        // Observe the operation without hiding the primary test failure.
                    }
                }
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                encryptionKey);

            if (Directory.Exists(rootDirectory))
            {
                Directory.Delete(
                    rootDirectory,
                    recursive: true);
            }
        }
    }

    [Fact]
    public async Task RecoverAsync_WhenRemovalHoldsDocumentLease_WaitsAndRevalidatesPersistedDocument()
    {
        // Arrange
        string rootDirectory =
            Path.Combine(
                Path.GetTempPath(),
                "DeskVaultIntegrationTests",
                Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(rootDirectory);

        string databasePath =
            Path.Combine(
                rootDirectory,
                "DeskVault.db");

        byte[] encryptionKey =
            RandomNumberGenerator.GetBytes(32);

        var removalDeleteStarted =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var releaseFirstRemovalDelete =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var recoveryAcquireRequested =
            new TaskCompletionSource<Guid>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        Task<RemoveDocumentResult>? removalTask = null;

        Task<RecoverDocumentArtifactsResult>? recoveryTask = null;

        try
        {
            string sourceFilePath =
                Path.Combine(
                    rootDirectory,
                    "removal-recovery-race.txt");

            await File.WriteAllTextAsync(
                sourceFilePath,
                "Document removal and artifact recovery coordination test.",
                TestContext.Current.CancellationToken);

            await using var importHarness =
                new DocumentPipelineTestHarness(
                    rootDirectory,
                    databasePath,
                    encryptionKey);

            ImportDocumentResult importResult =
                await importHarness.ImportHandler.HandleAsync(
                    new ImportDocumentCommand(
                        sourceFilePath,
                        "Removal Recovery Race Test"),
                    TestContext.Current.CancellationToken);

            Assert.Equal(
                ImportDocumentResultStatus.Success,
                importResult.Status);

            Assert.NotNull(
                importResult.DocumentId);

            Guid documentId =
                importResult.DocumentId.Value;

            Document? importedDocument =
                await importHarness.GetDocumentAsync(
                    documentId);

            Assert.NotNull(importedDocument);

            string artifactPath =
                importedDocument.StoredFilePath;

            Assert.True(
                File.Exists(artifactPath));

            // Retain a workspace membership to verify removal's retry contract.
            await using var removalHarness =
                new DocumentPipelineTestHarness(
                    rootDirectory,
                    databasePath,
                    encryptionKey,
                    removeStorageDecorator:
                        storage =>
                            new BlockingFailOnceRemovalStorageService(
                                storage,
                                removalDeleteStarted,
                                releaseFirstRemovalDelete));

            Workspace workspace =
                Workspace.CreatePersistent(
                    Guid.NewGuid(),
                    "Removal Recovery Workspace");

            workspace.AddDocument(documentId);

            await removalHarness.PersistWorkspaceAsync(workspace);

            // Reconciliation deliberately receives a stale view. Recovery
            // itself still uses the authoritative repository for revalidation.
            await using var recoveryHarness =
                new DocumentPipelineTestHarness(
                    rootDirectory,
                    databasePath,
                    encryptionKey,
                    artifactCoordinatorDecorator:
                        coordinator =>
                            new SignalingDocumentArtifactCoordinator(
                                coordinator,
                                recoveryAcquireRequested),
                    reconciliationRepositoryDecorator:
                        repository =>
                            new StaleReconciliationDocumentRepository(
                                repository));

            // Act: removal acquires the real lease and pauses before deleting
            // the artifact. Metadata and the workspace membership still exist.
            removalTask =
                removalHarness.RemoveHandler.HandleAsync(
                    new RemoveDocumentCommand(documentId),
                    TestContext.Current.CancellationToken);

            await removalDeleteStarted.Task.WaitAsync(
                TimeSpan.FromSeconds(15),
                TestContext.Current.CancellationToken);

            Assert.False(
                removalTask.IsCompleted);

            Assert.True(
                File.Exists(artifactPath));

            // Recovery sees a deliberately stale orphan finding and attempts
            // to acquire the same document lease held by removal.
            recoveryTask =
                recoveryHarness.RecoveryHandler.HandleAsync(
                    new RecoverDocumentArtifactsCommand(),
                    TestContext.Current.CancellationToken);

            Guid requestedDocumentId =
                await recoveryAcquireRequested.Task.WaitAsync(
                    TimeSpan.FromSeconds(15),
                    TestContext.Current.CancellationToken);

            Assert.Equal(
                documentId,
                requestedDocumentId);

            Assert.False(
                recoveryTask.IsCompleted);

            // The first removal attempt now fails. The handler must release
            // its lease without removing metadata or workspace membership.
            releaseFirstRemovalDelete.TrySetResult(true);

            RemoveDocumentResult removalResult =
                await removalTask;

            Assert.Equal(
                RemoveDocumentResultStatus.StorageDeletionFailed,
                removalResult.Status);

            Assert.NotNull(
                await removalHarness.GetDocumentAsync(documentId));

            Workspace? workspaceAfterFailure =
                await removalHarness.GetWorkspaceAsync(workspace.Id);

            Assert.NotNull(workspaceAfterFailure);

            Assert.Single(workspaceAfterFailure.Memberships);

            Assert.True(
                File.Exists(artifactPath));

            // Recovery must acquire the released lease and re-read metadata.
            // It must preserve the artifact despite its stale orphan finding.
            RecoverDocumentArtifactsResult recoveryResult =
                await recoveryTask;

            Assert.Equal(
                RecoverDocumentArtifactsResultStatus.PreservedForRecovery,
                recoveryResult.Status);

            DocumentArtifactReconciliationResult finding =
                Assert.Single(recoveryResult.Findings);

            Assert.Equal(
                documentId,
                finding.DocumentId);

            Assert.Equal(
                DocumentArtifactReconciliationStatus.OrphanedArtifact,
                finding.Status);

            Assert.Equal(
                DocumentArtifactReconciliationRecoveryAction.PreserveForRecovery,
                finding.RecoveryAction);

            Assert.True(
                File.Exists(artifactPath));

            Assert.NotNull(
                await recoveryHarness.GetDocumentAsync(documentId));

            // Retry removal using the same handler. Its second deletion attempt
            // delegates to real storage and completes the original lifecycle.
            RemoveDocumentResult retryResult =
                await removalHarness.RemoveHandler.HandleAsync(
                    new RemoveDocumentCommand(documentId),
                    TestContext.Current.CancellationToken);

            Assert.Equal(
                RemoveDocumentResultStatus.Success,
                retryResult.Status);

            Assert.False(
                File.Exists(artifactPath));

            Assert.Null(
                await removalHarness.GetDocumentAsync(documentId));

            Workspace? workspaceAfterRetry =
                await removalHarness.GetWorkspaceAsync(workspace.Id);

            Assert.NotNull(workspaceAfterRetry);

            Assert.Empty(
                workspaceAfterRetry.Memberships);
        }
        finally
        {
            // Always unblock a paused deletion before disposing the harnesses.
            releaseFirstRemovalDelete.TrySetResult(true);

            if (removalTask is not null)
            {
                try
                {
                    await removalTask.WaitAsync(
                        TimeSpan.FromSeconds(15),
                        TestContext.Current.CancellationToken);
                }
                catch (Exception)
                {
                    // Preserve the primary test failure while observing cleanup.
                }
            }

            if (recoveryTask is not null)
            {
                try
                {
                    await recoveryTask.WaitAsync(
                        TimeSpan.FromSeconds(15),
                        TestContext.Current.CancellationToken);
                }
                catch (Exception)
                {
                    // Preserve the primary test failure while observing cleanup.
                }
            }

            CryptographicOperations.ZeroMemory(
                encryptionKey);

            if (Directory.Exists(rootDirectory))
            {
                Directory.Delete(
                    rootDirectory,
                    recursive: true);
            }
        }
    }

    [Fact]
    public async Task RecoverAsync_WhenImportCompensationSucceeds_WaitsAndObservesArtifactAlreadyRemoved()
    {
        // Arrange
        string rootDirectory =
            Path.Combine(
                Path.GetTempPath(),
                "DeskVaultIntegrationTests",
                Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(rootDirectory);

        string databasePath =
            Path.Combine(
                rootDirectory,
                "DeskVault.db");

        byte[] encryptionKey =
            RandomNumberGenerator.GetBytes(32);

        var persistenceException =
            new InvalidOperationException(
                "Injected metadata persistence failure.");

        var artifactStored =
            new TaskCompletionSource<StoredArtifact>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var compensationStarted =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var releaseCompensation =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var recoveryAcquireRequested =
            new TaskCompletionSource<Guid>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        Task<ImportDocumentResult>? importTask = null;

        Task<RecoverDocumentArtifactsResult>? recoveryTask = null;

        try
        {
            string sourceFilePath =
                Path.Combine(
                    rootDirectory,
                    "successful-compensation-import.txt");

            await File.WriteAllTextAsync(
                sourceFilePath,
                "Document artifact successful compensation recovery test.",
                TestContext.Current.CancellationToken);

            await using var importHarness =
                new DocumentPipelineTestHarness(
                    rootDirectory,
                    databasePath,
                    encryptionKey,
                    importStorageDecorator:
                        storage =>
                            new BlockingSuccessfulCompensationStorageService(
                                storage,
                                artifactStored,
                                compensationStarted,
                                releaseCompensation),
                    importRepositoryDecorator:
                        repository =>
                            new FailingImportRepository(
                                repository,
                                persistenceException));

            await using var recoveryHarness =
                new DocumentPipelineTestHarness(
                    rootDirectory,
                    databasePath,
                    encryptionKey,
                    artifactCoordinatorDecorator:
                        coordinator =>
                            new SignalingDocumentArtifactCoordinator(
                                coordinator,
                                recoveryAcquireRequested));

            try
            {
                // Start import. Metadata publication fails, and compensation
                // pauses while the import still owns its coordination lease.
                importTask =
                    importHarness.ImportHandler.HandleAsync(
                        new ImportDocumentCommand(
                            sourceFilePath,
                            "Successful Compensation Import"),
                        TestContext.Current.CancellationToken);

                StoredArtifact storedArtifact =
                    await artifactStored.Task.WaitAsync(
                        TimeSpan.FromSeconds(15),
                        TestContext.Current.CancellationToken);

                await compensationStarted.Task.WaitAsync(
                    TimeSpan.FromSeconds(15),
                    TestContext.Current.CancellationToken);

                Assert.True(
                    File.Exists(
                        storedArtifact.StoredFilePath));

                Assert.Null(
                    await importHarness.GetDocumentAsync(
                        storedArtifact.DocumentId));

                // Recovery observes the artifact while compensation is
                // paused and must wait for the import lease to be released.
                recoveryTask =
                    recoveryHarness.RecoveryHandler.HandleAsync(
                        new RecoverDocumentArtifactsCommand(),
                        TestContext.Current.CancellationToken);

                Guid requestedDocumentId =
                    await recoveryAcquireRequested.Task.WaitAsync(
                        TimeSpan.FromSeconds(15),
                        TestContext.Current.CancellationToken);

                Assert.Equal(
                    storedArtifact.DocumentId,
                    requestedDocumentId);

                Assert.False(
                    recoveryTask.IsCompleted);

                // Allow successful compensation to delete the canonical artifact.
                releaseCompensation.TrySetResult(true);

                InvalidOperationException actualException =
                    await Assert.ThrowsAsync<InvalidOperationException>(
                        () => importTask);

                Assert.Same(
                    persistenceException,
                    actualException);

                Assert.False(
                    File.Exists(
                        storedArtifact.StoredFilePath));

                // Recovery acquires the released lease and must revalidate
                // artifact existence instead of applying its stale finding.
                RecoverDocumentArtifactsResult recoveryResult =
                    await recoveryTask;

                Assert.Equal(
                    RecoverDocumentArtifactsResultStatus.NoActionRequired,
                    recoveryResult.Status);

                DocumentArtifactReconciliationResult finding =
                    Assert.Single(
                        recoveryResult.Findings);

                Assert.Equal(
                    storedArtifact.DocumentId,
                    finding.DocumentId);

                Assert.Equal(
                    DocumentArtifactReconciliationStatus.OrphanedArtifact,
                    finding.Status);

                Assert.Equal(
                    DocumentArtifactReconciliationRecoveryAction.None,
                    finding.RecoveryAction);

                Assert.False(
                    File.Exists(
                        storedArtifact.StoredFilePath));

                Assert.Null(
                    await recoveryHarness.GetDocumentAsync(
                        storedArtifact.DocumentId));
            }
            finally
            {
                // Ensure a failed assertion cannot leave compensation blocked.
                releaseCompensation.TrySetResult(true);

                if (importTask is not null)
                {
                    try
                    {
                        await importTask.WaitAsync(
                            TimeSpan.FromSeconds(15),
                            TestContext.Current.CancellationToken);
                    }
                    catch (Exception)
                    {
                        // Observe the operation without hiding the primary test failure.
                    }
                }

                if (recoveryTask is not null)
                {
                    try
                    {
                        await recoveryTask.WaitAsync(
                            TimeSpan.FromSeconds(15),
                            TestContext.Current.CancellationToken);
                    }
                    catch (Exception)
                    {
                        // Observe the operation without hiding the primary test failure.
                    }
                }
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                encryptionKey);

            if (Directory.Exists(rootDirectory))
            {
                Directory.Delete(
                    rootDirectory,
                    recursive: true);
            }
        }
    }

    [Fact]
    public async Task RecoverAsync_WhenCanonicalOrphanExists_RemovesOrphanPreservesValidDocumentAndConvergesOnRetry()
    {
        await using var environment =
            await TestEnvironment.CreateAsync();

        string validSourceFilePath =
            Path.Combine(
                environment.RootDirectory,
                "valid-document.txt");

        await File.WriteAllTextAsync(validSourceFilePath, "Valid persisted document content.", TestContext.Current.CancellationToken);

        ImportDocumentResult importResult =
            await environment.Harness.ImportHandler.HandleAsync(new ImportDocumentCommand(
                    validSourceFilePath,
                    "Valid Persisted Document"), TestContext.Current.CancellationToken);

        Assert.Equal(
            ImportDocumentResultStatus.Success,
            importResult.Status);

        Assert.NotNull(
            importResult.DocumentId);

        Guid validDocumentId =
            importResult.DocumentId.Value;

        Document? validDocumentBeforeRecovery =
            await environment.Harness.GetDocumentAsync(
                validDocumentId);

        Assert.NotNull(
            validDocumentBeforeRecovery);

        string validArtifactPath =
            Path.GetFullPath(
                validDocumentBeforeRecovery.StoredFilePath);

        Assert.True(
            File.Exists(
                validArtifactPath));

        Guid orphanDocumentId =
            Guid.NewGuid();

        string orphanSourceFilePath =
            Path.Combine(
                environment.RootDirectory,
                "orphan-document.txt");

        await File.WriteAllTextAsync(orphanSourceFilePath, "Orphaned encrypted artifact.", TestContext.Current.CancellationToken);

        await environment.Harness.StoreArtifactAsync(orphanSourceFilePath, orphanDocumentId, TestContext.Current.CancellationToken);

        string orphanArtifactPath =
            Path.Combine(
                environment.Harness.DataPaths.DocumentsDirectory,
                $"{orphanDocumentId}.dvault");

        Assert.True(
            File.Exists(
                orphanArtifactPath));

        // Act
        RecoverDocumentArtifactsResult firstResult =
            await environment.Harness.RecoveryHandler.HandleAsync(new RecoverDocumentArtifactsCommand(), TestContext.Current.CancellationToken);

        RecoverDocumentArtifactsResult retryResult =
            await environment.Harness.RecoveryHandler.HandleAsync(new RecoverDocumentArtifactsCommand(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            RecoverDocumentArtifactsResultStatus.Recovered,
            firstResult.Status);

        DocumentArtifactReconciliationResult recoveredFinding =
            Assert.Single(
                firstResult.Findings,
                finding =>
                    finding.Status ==
                    DocumentArtifactReconciliationStatus.OrphanedArtifact);

        Assert.Equal(
            orphanDocumentId,
            recoveredFinding.DocumentId);

        Assert.Equal(
            Path.GetFullPath(
                orphanArtifactPath),
            recoveredFinding.ArtifactPath);

        Assert.Equal(
            DocumentArtifactReconciliationRecoveryAction.CleanupOrphanedArtifact,
            recoveredFinding.RecoveryAction);

        DocumentArtifactReconciliationResult validFinding =
            Assert.Single(
                firstResult.Findings,
                finding =>
                    finding.Status ==
                    DocumentArtifactReconciliationStatus.Matched);

        Assert.Equal(
            validDocumentId,
            validFinding.DocumentId);

        Assert.Equal(
            DocumentArtifactReconciliationRecoveryAction.None,
            validFinding.RecoveryAction);

        Assert.Equal(
            RecoverDocumentArtifactsResultStatus.NoActionRequired,
            retryResult.Status);

        DocumentArtifactReconciliationResult retryFinding =
            Assert.Single(
                retryResult.Findings);

        Assert.Equal(
            DocumentArtifactReconciliationStatus.Matched,
            retryFinding.Status);

        Assert.Equal(
            validDocumentId,
            retryFinding.DocumentId);

        Assert.Equal(
            DocumentArtifactReconciliationRecoveryAction.None,
            retryFinding.RecoveryAction);

        Assert.False(
            File.Exists(
                orphanArtifactPath));

        Assert.True(
            File.Exists(
                validArtifactPath));

        Document? validDocumentAfterRecovery =
            await environment.Harness.GetDocumentAsync(
                validDocumentId);

        Assert.NotNull(
            validDocumentAfterRecovery);

        Assert.Equal(
            validDocumentBeforeRecovery.Id,
            validDocumentAfterRecovery.Id);

        Assert.Equal(
            validDocumentBeforeRecovery.Sha256Hash,
            validDocumentAfterRecovery.Sha256Hash);

        Assert.Equal(
            validDocumentBeforeRecovery.StoredFilePath,
            validDocumentAfterRecovery.StoredFilePath);
    }

    /// <summary>
    /// Represents the canonical artifact created by the blocked import.
    /// </summary>
    private sealed record StoredArtifact(
        Guid DocumentId,
        string StoredFilePath);

    /// <summary>
    /// Blocks an import after the real storage operation has created its artifact.
    /// </summary>
    private sealed class BlockingImportStorageService : IStorageService
    {
        private readonly IStorageService _inner;

        private readonly TaskCompletionSource<StoredArtifact> _artifactStored;

        private readonly TaskCompletionSource<bool> _releaseStorage;

        public BlockingImportStorageService(
            IStorageService inner,
            TaskCompletionSource<StoredArtifact> artifactStored,
            TaskCompletionSource<bool> releaseStorage)
        {
            _inner = inner;
            _artifactStored = artifactStored;
            _releaseStorage = releaseStorage;
        }

        public async Task<string> StoreAsync(
            string sourceFilePath,
            Guid documentId,
            CancellationToken cancellationToken = default,
            string? expectedSha256Hash = null)
        {
            string storedFilePath =
                await _inner.StoreAsync(
                    sourceFilePath,
                    documentId,
                    cancellationToken,
                    expectedSha256Hash);

            // The artifact now exists, but the import handler has not yet
            // continued to metadata publication.
            _artifactStored.TrySetResult(
                new StoredArtifact(
                    documentId,
                    storedFilePath));

            await _releaseStorage.Task.WaitAsync(
                cancellationToken);

            return storedFilePath;
        }

        public Task DeleteAsync(
            Guid documentId,
            CancellationToken cancellationToken = default)
        {
            return _inner.DeleteAsync(
                documentId,
                cancellationToken);
        }

        public bool IsOwnedArtifactPath(
            Guid documentId,
            string storedFilePath)
        {
            return _inner.IsOwnedArtifactPath(
                documentId,
                storedFilePath);
        }
    }

    /// <summary>
    /// Signals when recovery requests a lease, then delegates to the real coordinator.
    /// </summary>
    private sealed class SignalingDocumentArtifactCoordinator
        : IDocumentArtifactCoordinator
    {
        private readonly IDocumentArtifactCoordinator _inner;

        private readonly TaskCompletionSource<Guid> _acquisitionRequested;

        public SignalingDocumentArtifactCoordinator(
            IDocumentArtifactCoordinator inner,
            TaskCompletionSource<Guid> acquisitionRequested)
        {
            _inner = inner;
            _acquisitionRequested = acquisitionRequested;
        }

        public Task<IAsyncDisposable> AcquireAsync(
            Guid documentId,
            CancellationToken cancellationToken = default)
        {
            // Signal before delegating so the test can verify that recovery
            // is waiting on the actual filesystem lease.
            _acquisitionRequested.TrySetResult(documentId);

            return _inner.AcquireAsync(
                documentId,
                cancellationToken);
        }
    }

    /// <summary>
    /// Allows the real artifact storage operation to complete, then
    /// deterministically fails the import compensation deletion attempt.
    /// </summary>
    private sealed class FailingCompensationStorageService : IStorageService
    {
        private readonly IStorageService _inner;

        private readonly TaskCompletionSource<StoredArtifact> _artifactStored;

        private readonly TaskCompletionSource<bool> _compensationStarted;

        private readonly TaskCompletionSource<bool> _releaseCompensation;

        public FailingCompensationStorageService(
            IStorageService inner,
            TaskCompletionSource<StoredArtifact> artifactStored,
            TaskCompletionSource<bool> compensationStarted,
            TaskCompletionSource<bool> releaseCompensation)
        {
            _inner = inner;
            _artifactStored = artifactStored;
            _compensationStarted = compensationStarted;
            _releaseCompensation = releaseCompensation;
        }

        public async Task<string> StoreAsync(
            string sourceFilePath,
            Guid documentId,
            CancellationToken cancellationToken = default,
            string? expectedSha256Hash = null)
        {
            string storedFilePath =
                await _inner.StoreAsync(
                    sourceFilePath,
                    documentId,
                    cancellationToken,
                    expectedSha256Hash);

            _artifactStored.TrySetResult(
                new StoredArtifact(
                    documentId,
                    storedFilePath));

            return storedFilePath;
        }

        public async Task DeleteAsync(
            Guid documentId,
            CancellationToken cancellationToken = default)
        {
            // The import handler still owns its coordination lease here.
            _compensationStarted.TrySetResult(true);

            await _releaseCompensation.Task;

            // Simulate a failed compensation after the deterministic pause.
            throw new IOException(
                "Injected import compensation failure.");
        }

        public bool IsOwnedArtifactPath(
            Guid documentId,
            string storedFilePath)
        {
            return _inner.IsOwnedArtifactPath(
                documentId,
                storedFilePath);
        }
    }

    /// <summary>
    /// Injects a deterministic metadata-publication failure while preserving
    /// all other repository behavior.
    /// </summary>
    private sealed class FailingImportRepository : IDocumentRepository
    {
        private readonly IDocumentRepository _inner;

        private readonly InvalidOperationException _persistenceException;

        public FailingImportRepository(
            IDocumentRepository inner,
            InvalidOperationException persistenceException)
        {
            _inner = inner;
            _persistenceException = persistenceException;
        }

        public Task<bool> ExistsByHashAsync(
            string sha256Hash,
            CancellationToken cancellationToken = default)
        {
            return _inner.ExistsByHashAsync(
                sha256Hash,
                cancellationToken);
        }

        public Task AddAsync(
            Document document,
            CancellationToken cancellationToken = default)
        {
            return Task.FromException(
                _persistenceException);
        }

        public Task<Document?> GetByIdAsync(
            Guid documentId,
            CancellationToken cancellationToken = default)
        {
            return _inner.GetByIdAsync(
                documentId,
                cancellationToken);
        }

        public Task<IReadOnlyList<Document>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            return _inner.GetAllAsync(
                cancellationToken);
        }

        public Task<IReadOnlyList<Document>> GetPageAsync(
            DocumentPageCursor? cursor,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            return _inner.GetPageAsync(
                cursor,
                pageSize,
                cancellationToken);
        }

        public Task DeleteAsync(
            Guid documentId,
            CancellationToken cancellationToken = default)
        {
            return _inner.DeleteAsync(
                documentId,
                cancellationToken);
        }

        public Task UpdateAsync(
            Document document,
            CancellationToken cancellationToken = default)
        {
            return _inner.UpdateAsync(
                document,
                cancellationToken);
        }
    }

    /// <summary>
    /// Blocks the first removal deletion attempt and then fails it,
    /// while allowing a subsequent retry to use the real storage service.
    /// </summary>
    private sealed class BlockingFailOnceRemovalStorageService : IStorageService
    {
        private readonly IStorageService _inner;

        private readonly TaskCompletionSource<bool> _deleteStarted;

        private readonly TaskCompletionSource<bool> _releaseFirstDelete;

        private int _deleteCount;

        public BlockingFailOnceRemovalStorageService(
            IStorageService inner,
            TaskCompletionSource<bool> deleteStarted,
            TaskCompletionSource<bool> releaseFirstDelete)
        {
            _inner = inner;
            _deleteStarted = deleteStarted;
            _releaseFirstDelete = releaseFirstDelete;
        }

        public Task<string> StoreAsync(
            string sourceFilePath,
            Guid documentId,
            CancellationToken cancellationToken = default,
            string? expectedSha256Hash = null)
        {
            return _inner.StoreAsync(
                sourceFilePath,
                documentId,
                cancellationToken,
                expectedSha256Hash);
        }

        public async Task DeleteAsync(
            Guid documentId,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _deleteCount) == 1)
            {
                // The removal handler owns the document lease at this point.
                _deleteStarted.TrySetResult(true);

                await _releaseFirstDelete.Task.WaitAsync(
                    cancellationToken);

                throw new IOException(
                    "Injected first removal artifact deletion failure.");
            }

            // Subsequent attempts use the real filesystem deletion.
            await _inner.DeleteAsync(
                documentId,
                cancellationToken);
        }

        public bool IsOwnedArtifactPath(
            Guid documentId,
            string storedFilePath)
        {
            return _inner.IsOwnedArtifactPath(
                documentId,
                storedFilePath);
        }
    }

    /// <summary>
    /// Supplies a deliberately stale read-only reconciliation view while
    /// leaving the recovery handler's authoritative repository unchanged.
    /// </summary>
    private sealed class StaleReconciliationDocumentRepository
        : IDocumentRepository
    {
        private readonly IDocumentRepository _inner;

        public StaleReconciliationDocumentRepository(
            IDocumentRepository inner)
        {
            _inner = inner;
        }

        public Task<bool> ExistsByHashAsync(
            string sha256Hash,
            CancellationToken cancellationToken = default)
        {
            return _inner.ExistsByHashAsync(
                sha256Hash,
                cancellationToken);
        }

        public Task AddAsync(
            Document document,
            CancellationToken cancellationToken = default)
        {
            return _inner.AddAsync(
                document,
                cancellationToken);
        }

        public Task<Document?> GetByIdAsync(
            Guid documentId,
            CancellationToken cancellationToken = default)
        {
            // Simulate a stale reconciliation observation.
            return Task.FromResult<Document?>(null);
        }

        public Task<IReadOnlyList<Document>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            return _inner.GetAllAsync(
                cancellationToken);
        }

        public Task<IReadOnlyList<Document>> GetPageAsync(
            DocumentPageCursor? cursor,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            // Reconciliation sees no persisted document in its initial scan.
            return Task.FromResult<IReadOnlyList<Document>>(
                []);
        }

        public Task DeleteAsync(
            Guid documentId,
            CancellationToken cancellationToken = default)
        {
            return _inner.DeleteAsync(
                documentId,
                cancellationToken);
        }

        public Task UpdateAsync(
            Document document,
            CancellationToken cancellationToken = default)
        {
            return _inner.UpdateAsync(
                document,
                cancellationToken);
        }
    }

    /// <summary>
    /// Pauses the import compensation attempt before delegating deletion
    /// to the real storage service.
    /// </summary>
    private sealed class BlockingSuccessfulCompensationStorageService : IStorageService
    {
        private readonly IStorageService _inner;

        private readonly TaskCompletionSource<StoredArtifact> _artifactStored;

        private readonly TaskCompletionSource<bool> _compensationStarted;

        private readonly TaskCompletionSource<bool> _releaseCompensation;

        public BlockingSuccessfulCompensationStorageService(
            IStorageService inner,
            TaskCompletionSource<StoredArtifact> artifactStored,
            TaskCompletionSource<bool> compensationStarted,
            TaskCompletionSource<bool> releaseCompensation)
        {
            _inner = inner;
            _artifactStored = artifactStored;
            _compensationStarted = compensationStarted;
            _releaseCompensation = releaseCompensation;
        }

        public async Task<string> StoreAsync(
            string sourceFilePath,
            Guid documentId,
            CancellationToken cancellationToken = default,
            string? expectedSha256Hash = null)
        {
            string storedFilePath =
                await _inner.StoreAsync(
                    sourceFilePath,
                    documentId,
                    cancellationToken,
                    expectedSha256Hash);

            _artifactStored.TrySetResult(
                new StoredArtifact(
                    documentId,
                    storedFilePath));

            return storedFilePath;
        }

        public async Task DeleteAsync(
            Guid documentId,
            CancellationToken cancellationToken = default)
        {
            // The import handler still owns its lease during compensation.
            _compensationStarted.TrySetResult(true);

            await _releaseCompensation.Task.WaitAsync(
                cancellationToken);

            // Let the real storage service complete compensation.
            await _inner.DeleteAsync(
                documentId,
                cancellationToken);
        }

        public bool IsOwnedArtifactPath(
            Guid documentId,
            string storedFilePath)
        {
            return _inner.IsOwnedArtifactPath(
                documentId,
                storedFilePath);
        }
    }

    private sealed class TestEnvironment : IAsyncDisposable
    {
        private TestEnvironment(
            string rootDirectory,
            DocumentPipelineTestHarness harness)
        {
            RootDirectory =
                rootDirectory;

            Harness =
                harness;
        }

        public string RootDirectory { get; }

        public DocumentPipelineTestHarness Harness { get; }

        public static Task<TestEnvironment> CreateAsync()
        {
            string rootDirectory =
                Path.Combine(
                    Path.GetTempPath(),
                    "DeskVaultIntegrationTests",
                    Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(
                rootDirectory);

            string databasePath =
                Path.Combine(
                    rootDirectory,
                    "DeskVault.db");

            byte[] encryptionKey =
                RandomNumberGenerator.GetBytes(32);

            var harness =
                new DocumentPipelineTestHarness(
                    rootDirectory,
                    databasePath,
                    encryptionKey);

            return Task.FromResult(
                new TestEnvironment(
                    rootDirectory,
                    harness));
        }

        public async ValueTask DisposeAsync()
        {
            await Harness.DisposeAsync();

            if (Directory.Exists(
                RootDirectory))
            {
                Directory.Delete(
                    RootDirectory,
                    recursive: true);
            }
        }
    }
}
