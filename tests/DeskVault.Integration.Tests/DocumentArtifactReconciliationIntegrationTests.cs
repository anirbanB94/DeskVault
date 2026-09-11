using DeskVault.Application.Documents.Commands.ImportDocument;
using DeskVault.Application.Documents.Commands.RemoveDocument;
using DeskVault.Application.Documents.Queries.ReconcileDocumentArtifacts;
using DeskVault.Domain.Documents;
using System.Security.Cryptography;

namespace DeskVault.Integration.Tests;

public sealed class DocumentArtifactReconciliationIntegrationTests
{
    [Fact]
    public async Task ReconcileAsync_WhenDocumentAndArtifactExist_ReturnsMatched()
    {
        await using var environment =
            await TestEnvironment.CreateAsync();

        string sourceFilePath =
            Path.Combine(
                environment.RootDirectory,
                "integration-test.txt");

        await File.WriteAllTextAsync(
            sourceFilePath,
            "DeskVault reconciliation integration test.");

        ImportDocumentResult importResult =
            await environment.Harness.ImportHandler.HandleAsync(
                new ImportDocumentCommand(
                    sourceFilePath,
                    "Reconciliation Integration Test Document"));

        Assert.Equal(
            ImportDocumentResultStatus.Success,
            importResult.Status);

        Assert.NotNull(
            importResult.DocumentId);

        // Act
        ReconcileDocumentArtifactsResult result =
            await environment.Harness.ReconciliationHandler.HandleAsync(
                new ReconcileDocumentArtifactsQuery());

        // Assert
        DocumentArtifactReconciliationResult finding =
            Assert.Single(
                result.Findings);

        Assert.Equal(
            DocumentArtifactReconciliationStatus.Matched,
            finding.Status);

        Assert.Equal(
            importResult.DocumentId,
            finding.DocumentId);

        Assert.True(
            File.Exists(
                finding.ArtifactPath));
    }

    [Fact]
    public async Task ReconcileAsync_WhenArtifactIsMissing_ReturnsMissingArtifact()
    {
        await using var environment =
            await TestEnvironment.CreateAsync();

        string sourceFilePath =
            Path.Combine(
                environment.RootDirectory,
                "integration-test.txt");

        await File.WriteAllTextAsync(
            sourceFilePath,
            "DeskVault reconciliation integration test.");

        ImportDocumentResult importResult =
            await environment.Harness.ImportHandler.HandleAsync(
                new ImportDocumentCommand(
                    sourceFilePath,
                    "Reconciliation Integration Test Document"));

        Assert.Equal(
            ImportDocumentResultStatus.Success,
            importResult.Status);

        Assert.NotNull(
            importResult.DocumentId);

        string artifactPath =
            Path.Combine(
                environment.Harness.DataPaths.DocumentsDirectory,
                $"{importResult.DocumentId}.dvault");

        File.Delete(
            artifactPath);

        // Act
        ReconcileDocumentArtifactsResult result =
            await environment.Harness.ReconciliationHandler.HandleAsync(
                new ReconcileDocumentArtifactsQuery());

        // Assert
        DocumentArtifactReconciliationResult finding =
            Assert.Single(
                result.Findings);

        Assert.Equal(
            DocumentArtifactReconciliationStatus.MissingArtifact,
            finding.Status);

        Assert.Equal(
            importResult.DocumentId,
            finding.DocumentId);

        Assert.Equal(
            Path.GetFullPath(
                artifactPath),
            finding.ArtifactPath);

        Assert.False(
            File.Exists(
                artifactPath));
    }

    [Fact]
    public async Task ReconcileAsync_WhenArtifactHasNoDocumentRecord_ReturnsOrphanedArtifact()
    {
        await using var environment =
            await TestEnvironment.CreateAsync();

        string sourceFilePath =
            Path.Combine(
                environment.RootDirectory,
                "integration-test.txt");

        await File.WriteAllTextAsync(
            sourceFilePath,
            "DeskVault reconciliation integration test.");

        ImportDocumentResult importResult =
            await environment.Harness.ImportHandler.HandleAsync(
                new ImportDocumentCommand(
                    sourceFilePath,
                    "Reconciliation Integration Test Document"));

        Assert.Equal(
            ImportDocumentResultStatus.Success,
            importResult.Status);

        Assert.NotNull(
            importResult.DocumentId);

        Guid documentId =
            importResult.DocumentId.Value;

        RemoveDocumentResult removeResult =
            await environment.Harness.RemoveHandler.HandleAsync(
                new RemoveDocumentCommand(
                    documentId));

        Assert.Equal(
            RemoveDocumentResultStatus.Success,
            removeResult.Status);

        string orphanSourceFilePath =
            Path.Combine(
                environment.RootDirectory,
                "orphan.txt");

        await File.WriteAllTextAsync(
            orphanSourceFilePath,
            "Orphaned encrypted artifact.");

        await environment.Harness.StoreArtifactAsync(
            orphanSourceFilePath,
            documentId);

        string artifactPath =
            Path.Combine(
                environment.Harness.DataPaths.DocumentsDirectory,
                $"{documentId}.dvault");

        // Act
        ReconcileDocumentArtifactsResult result =
            await environment.Harness.ReconciliationHandler.HandleAsync(
                new ReconcileDocumentArtifactsQuery());

        // Assert
        DocumentArtifactReconciliationResult finding =
            Assert.Single(
                result.Findings);

        Assert.Equal(
            DocumentArtifactReconciliationStatus.OrphanedArtifact,
            finding.Status);

        Assert.Equal(
            documentId,
            finding.DocumentId);

        Assert.Equal(
            Path.GetFullPath(
                artifactPath),
            finding.ArtifactPath);

        Assert.True(
            File.Exists(
                finding.ArtifactPath));
    }

    [Fact]
    public async Task ReconcileAsync_WhenArtifactIsCorrupted_ReturnsUnreadableArtifactWithoutMutatingDocument()
    {
        await using var environment =
            await TestEnvironment.CreateAsync();

        string sourceFilePath =
            Path.Combine(
                environment.RootDirectory,
                "integration-test.txt");

        await File.WriteAllTextAsync(
            sourceFilePath,
            "DeskVault reconciliation integration test.");

        ImportDocumentResult importResult =
            await environment.Harness.ImportHandler.HandleAsync(
                new ImportDocumentCommand(
                    sourceFilePath,
                    "Reconciliation Integration Test Document"));

        Assert.Equal(
            ImportDocumentResultStatus.Success,
            importResult.Status);

        Assert.NotNull(
            importResult.DocumentId);

        Guid documentId =
            importResult.DocumentId.Value;

        string artifactPath =
            Path.Combine(
                environment.Harness.DataPaths.DocumentsDirectory,
                $"{documentId}.dvault");

        byte[] artifactBytes =
            await File.ReadAllBytesAsync(
                artifactPath);

        Assert.NotEmpty(
            artifactBytes);

        artifactBytes[^1] ^= 0xFF;

        await File.WriteAllBytesAsync(
            artifactPath,
            artifactBytes);

        // Act
        ReconcileDocumentArtifactsResult result =
            await environment.Harness.ReconciliationHandler.HandleAsync(
                new ReconcileDocumentArtifactsQuery());

        // Assert
        DocumentArtifactReconciliationResult finding =
            Assert.Single(
                result.Findings);

        Assert.Equal(
            DocumentArtifactReconciliationStatus.UnreadableArtifact,
            finding.Status);

        Assert.Equal(
            documentId,
            finding.DocumentId);

        Assert.Equal(
            Path.GetFullPath(
                artifactPath),
            finding.ArtifactPath);

        Assert.True(
            File.Exists(
                artifactPath));

        Assert.NotNull(
            await environment.Harness.GetDocumentAsync(
                documentId));
    }

    [Fact]
    public async Task ReconcileAsync_WhenStoredPathDoesNotMatchDocumentIdentity_ReturnsPathMismatch()
    {
        await using var environment =
            await TestEnvironment.CreateAsync();

        Guid documentId =
            Guid.NewGuid();

        Guid differentArtifactId =
            Guid.NewGuid();

        string mismatchedArtifactPath =
            Path.Combine(
                environment.Harness.DataPaths.DocumentsDirectory,
                $"{differentArtifactId}.dvault");

        Document document =
            Document.Create(
                documentId,
                "document.txt",
                "Path Mismatch Test Document",
                "sha256-path-mismatch-test-hash",
                mismatchedArtifactPath);

        await environment.Harness.PersistDocumentAsync(
            document);

        string sourceFilePath =
            Path.Combine(
                environment.RootDirectory,
                "mismatched-artifact-source.txt");

        await File.WriteAllTextAsync(
            sourceFilePath,
            "Mismatched artifact.");

        await environment.Harness.StoreArtifactAsync(
            sourceFilePath,
            differentArtifactId);

        // Act
        ReconcileDocumentArtifactsResult result =
            await environment.Harness.ReconciliationHandler.HandleAsync(
                new ReconcileDocumentArtifactsQuery());

        // Assert
        DocumentArtifactReconciliationResult finding =
            Assert.Single(
                result.Findings);

        Assert.Equal(
            DocumentArtifactReconciliationStatus.PathMismatch,
            finding.Status);

        Assert.Equal(
            documentId,
            finding.DocumentId);

        Assert.Equal(
            Path.GetFullPath(
                mismatchedArtifactPath),
            finding.ArtifactPath);

        Assert.True(
            File.Exists(
                finding.ArtifactPath));
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
