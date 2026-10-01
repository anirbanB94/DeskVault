using DeskVault.Application.Documents.Commands.ImportDocument;
using DeskVault.Application.Documents.Commands.RecoverDocumentArtifacts;
using DeskVault.Application.Documents.Queries.ReconcileDocumentArtifacts;
using DeskVault.Domain.Documents;
using System.Security.Cryptography;

namespace DeskVault.Integration.Tests;

public sealed class DocumentArtifactRecoveryIntegrationTests
{
    [Fact]
    public async Task RecoverAsync_WhenCanonicalOrphanExists_RemovesOrphanPreservesValidDocumentAndConvergesOnRetry()
    {
        await using var environment =
            await TestEnvironment.CreateAsync();

        string validSourceFilePath =
            Path.Combine(
                environment.RootDirectory,
                "valid-document.txt");

        await File.WriteAllTextAsync(
            validSourceFilePath,
            "Valid persisted document content.");

        ImportDocumentResult importResult =
            await environment.Harness.ImportHandler.HandleAsync(
                new ImportDocumentCommand(
                    validSourceFilePath,
                    "Valid Persisted Document"));

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

        await File.WriteAllTextAsync(
            orphanSourceFilePath,
            "Orphaned encrypted artifact.");

        await environment.Harness.StoreArtifactAsync(
            orphanSourceFilePath,
            orphanDocumentId);

        string orphanArtifactPath =
            Path.Combine(
                environment.Harness.DataPaths.DocumentsDirectory,
                $"{orphanDocumentId}.dvault");

        Assert.True(
            File.Exists(
                orphanArtifactPath));

        // Act
        RecoverDocumentArtifactsResult firstResult =
            await environment.Harness.RecoveryHandler.HandleAsync(
                new RecoverDocumentArtifactsCommand());

        RecoverDocumentArtifactsResult retryResult =
            await environment.Harness.RecoveryHandler.HandleAsync(
                new RecoverDocumentArtifactsCommand());

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
