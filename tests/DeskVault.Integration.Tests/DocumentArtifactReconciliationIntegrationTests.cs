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
    public async Task ReconcileAsync_WhenReconciliationIsRepeated_PreservesMatchedDocumentAndArtifact()
    {
        await using var environment =
            await TestEnvironment.CreateAsync();

        string sourceFilePath =
            Path.Combine(
                environment.RootDirectory,
                "repeated-reconciliation.txt");

        await File.WriteAllTextAsync(
            sourceFilePath,
            "DeskVault repeated reconciliation integration test.");

        ImportDocumentResult importResult =
            await environment.Harness.ImportHandler.HandleAsync(
                new ImportDocumentCommand(
                    sourceFilePath,
                    "Repeated Reconciliation Test Document"));

        Assert.Equal(
            ImportDocumentResultStatus.Success,
            importResult.Status);

        Assert.NotNull(
            importResult.DocumentId);

        Guid documentId =
            importResult.DocumentId.Value;

        Document? documentBeforeReconciliation =
            await environment.Harness.GetDocumentAsync(
                documentId);

        Assert.NotNull(
            documentBeforeReconciliation);

        string artifactPath =
            Path.GetFullPath(
                documentBeforeReconciliation.StoredFilePath);

        byte[] artifactBeforeReconciliation =
            await File.ReadAllBytesAsync(
                artifactPath);

        Assert.NotEmpty(
            artifactBeforeReconciliation);

        // Act
        ReconcileDocumentArtifactsResult firstResult =
            await environment.Harness.ReconciliationHandler.HandleAsync(
                new ReconcileDocumentArtifactsQuery());

        ReconcileDocumentArtifactsResult secondResult =
            await environment.Harness.ReconciliationHandler.HandleAsync(
                new ReconcileDocumentArtifactsQuery());

        // Assert
        DocumentArtifactReconciliationResult firstFinding =
            Assert.Single(
                firstResult.Findings);

        Assert.Equal(
            DocumentArtifactReconciliationStatus.Matched,
            firstFinding.Status);

        Assert.Equal(
            documentId,
            firstFinding.DocumentId);

        Assert.Equal(
            Path.GetFullPath(
                artifactPath),
            firstFinding.ArtifactPath);

        Assert.Equal(
            DocumentArtifactReconciliationRecoveryAction.None,
            firstFinding.RecoveryAction);

        DocumentArtifactReconciliationResult secondFinding =
            Assert.Single(
                secondResult.Findings);

        Assert.Equal(
            DocumentArtifactReconciliationStatus.Matched,
            secondFinding.Status);

        Assert.Equal(
            documentId,
            secondFinding.DocumentId);

        Assert.Equal(
            Path.GetFullPath(
                artifactPath),
            secondFinding.ArtifactPath);

        Assert.Equal(
            DocumentArtifactReconciliationRecoveryAction.None,
            secondFinding.RecoveryAction);

        Assert.True(
            File.Exists(
                artifactPath));

        byte[] artifactAfterReconciliation =
            await File.ReadAllBytesAsync(
                artifactPath);

        Assert.Equal(
            artifactBeforeReconciliation,
            artifactAfterReconciliation);

        Document? documentAfterReconciliation =
            await environment.Harness.GetDocumentAsync(
                documentId);

        Assert.NotNull(
            documentAfterReconciliation);

        Assert.Equal(
            documentBeforeReconciliation.Id,
            documentAfterReconciliation.Id);

        Assert.Equal(
            documentBeforeReconciliation.Sha256Hash,
            documentAfterReconciliation.Sha256Hash);

        Assert.Equal(
            documentBeforeReconciliation.StoredFilePath,
            documentAfterReconciliation.StoredFilePath);

        Assert.Equal(
            documentBeforeReconciliation.DisplayName,
            documentAfterReconciliation.DisplayName);
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
    public async Task ReconcileAsync_WhenOneArtifactIsUnreadable_DoesNotModifyUnrelatedValidDocument()
    {
        await using var environment =
            await TestEnvironment.CreateAsync();

        string validSourceFilePath =
            Path.Combine(
                environment.RootDirectory,
                "valid-document.txt");

        await File.WriteAllTextAsync(
            validSourceFilePath,
            "Valid document content.");

        ImportDocumentResult validImportResult =
            await environment.Harness.ImportHandler.HandleAsync(
                new ImportDocumentCommand(
                    validSourceFilePath,
                    "Valid Reconciliation Document"));

        Assert.Equal(
            ImportDocumentResultStatus.Success,
            validImportResult.Status);

        Assert.NotNull(
            validImportResult.DocumentId);

        Guid validDocumentId =
            validImportResult.DocumentId.Value;

        Document? validDocumentBeforeReconciliation =
            await environment.Harness.GetDocumentAsync(
                validDocumentId);

        Assert.NotNull(
            validDocumentBeforeReconciliation);

        string validArtifactPath =
            Path.GetFullPath(
                validDocumentBeforeReconciliation.StoredFilePath);

        Assert.True(
            File.Exists(
                validArtifactPath));

        string unreadableSourceFilePath =
            Path.Combine(
                environment.RootDirectory,
                "unreadable-document.txt");

        await File.WriteAllTextAsync(
            unreadableSourceFilePath,
            "Unreadable document content.");

        ImportDocumentResult unreadableImportResult =
            await environment.Harness.ImportHandler.HandleAsync(
                new ImportDocumentCommand(
                    unreadableSourceFilePath,
                    "Unreadable Reconciliation Document"));

        Assert.Equal(
            ImportDocumentResultStatus.Success,
            unreadableImportResult.Status);

        Assert.NotNull(
            unreadableImportResult.DocumentId);

        Guid unreadableDocumentId =
            unreadableImportResult.DocumentId.Value;

        Document? unreadableDocumentBeforeReconciliation =
            await environment.Harness.GetDocumentAsync(
                unreadableDocumentId);

        Assert.NotNull(
            unreadableDocumentBeforeReconciliation);

        string unreadableArtifactPath =
            Path.GetFullPath(
                unreadableDocumentBeforeReconciliation.StoredFilePath);

        byte[] unreadableArtifactBytes =
            await File.ReadAllBytesAsync(
                unreadableArtifactPath);

        Assert.NotEmpty(
            unreadableArtifactBytes);

        unreadableArtifactBytes[^1] ^= 0xFF;

        await File.WriteAllBytesAsync(
            unreadableArtifactPath,
            unreadableArtifactBytes);

        // Act
        ReconcileDocumentArtifactsResult result =
            await environment.Harness.ReconciliationHandler.HandleAsync(
                new ReconcileDocumentArtifactsQuery());

        // Assert
        DocumentArtifactReconciliationResult unreadableFinding =
            Assert.Single(
                result.Findings,
                finding =>
                    finding.DocumentId ==
                    unreadableDocumentId);

        Assert.Equal(
            DocumentArtifactReconciliationStatus.UnreadableArtifact,
            unreadableFinding.Status);

        Assert.Equal(
            DocumentArtifactReconciliationRecoveryAction.PreserveForRecovery,
            unreadableFinding.RecoveryAction);

        DocumentArtifactReconciliationResult validFinding =
            Assert.Single(
                result.Findings,
                finding =>
                    finding.DocumentId ==
                    validDocumentId);

        Assert.Equal(
            DocumentArtifactReconciliationStatus.Matched,
            validFinding.Status);

        Assert.Equal(
            DocumentArtifactReconciliationRecoveryAction.None,
            validFinding.RecoveryAction);

        Assert.True(
            File.Exists(
                unreadableArtifactPath));

        Assert.True(
            File.Exists(
                validArtifactPath));

        Document? validDocumentAfterReconciliation =
            await environment.Harness.GetDocumentAsync(
                validDocumentId);

        Assert.NotNull(
            validDocumentAfterReconciliation);

        Assert.Equal(
            validDocumentBeforeReconciliation.Id,
            validDocumentAfterReconciliation.Id);

        Assert.Equal(
            validDocumentBeforeReconciliation.Sha256Hash,
            validDocumentAfterReconciliation.Sha256Hash);

        Assert.Equal(
            validDocumentBeforeReconciliation.StoredFilePath,
            validDocumentAfterReconciliation.StoredFilePath);

        Document? unreadableDocumentAfterReconciliation =
            await environment.Harness.GetDocumentAsync(
                unreadableDocumentId);

        Assert.NotNull(
            unreadableDocumentAfterReconciliation);

        Assert.Equal(
            unreadableDocumentBeforeReconciliation.Id,
            unreadableDocumentAfterReconciliation.Id);

        Assert.Equal(
            unreadableDocumentBeforeReconciliation.Sha256Hash,
            unreadableDocumentAfterReconciliation.Sha256Hash);

        Assert.Equal(
            unreadableDocumentBeforeReconciliation.StoredFilePath,
            unreadableDocumentAfterReconciliation.StoredFilePath);
    }

    [Fact]
    public async Task ReconcileAsync_WhenArtifactIsReadableButContainsDifferentContent_ReturnsContentMismatchWithoutMutatingDocument()
    {
        await using var environment =
            await TestEnvironment.CreateAsync();

        string originalSourceFilePath =
            Path.Combine(
                environment.RootDirectory,
                "original-content.txt");

        await File.WriteAllTextAsync(
            originalSourceFilePath,
            "Original document content.");

        ImportDocumentResult importResult =
            await environment.Harness.ImportHandler.HandleAsync(
                new ImportDocumentCommand(
                    originalSourceFilePath,
                    "Content Mismatch Integration Test Document"));

        Assert.Equal(
            ImportDocumentResultStatus.Success,
            importResult.Status);

        Assert.NotNull(
            importResult.DocumentId);

        Guid documentId =
            importResult.DocumentId.Value;

        Document? persistedDocumentBeforeReconciliation =
            await environment.Harness.GetDocumentAsync(
                documentId);

        Assert.NotNull(
            persistedDocumentBeforeReconciliation);

        string persistedHash =
            persistedDocumentBeforeReconciliation.Sha256Hash;

        string replacementSourceFilePath =
            Path.Combine(
                environment.RootDirectory,
                "replacement-content.txt");

        await File.WriteAllTextAsync(
            replacementSourceFilePath,
            "Different readable document content.");

        string artifactPath =
            Path.Combine(
                environment.Harness.DataPaths.DocumentsDirectory,
                $"{documentId}.dvault");

        Assert.True(
            File.Exists(
                artifactPath));

        File.Delete(
            artifactPath);

        Assert.False(
            File.Exists(
                artifactPath));

        await environment.Harness.StoreArtifactAsync(
            replacementSourceFilePath,
            documentId);

        Assert.True(
            File.Exists(
                artifactPath));

        Assert.True(
            File.Exists(
                artifactPath));

        // Act
        ReconcileDocumentArtifactsResult result =
            await environment.Harness.ReconciliationHandler.HandleAsync(
                new ReconcileDocumentArtifactsQuery());

        // Assert
        DocumentArtifactReconciliationResult finding =
            Assert.Single(
                result.Findings);

        Assert.Equal(
            DocumentArtifactReconciliationStatus.ContentMismatch,
            finding.Status);

        Assert.Equal(
            documentId,
            finding.DocumentId);

        Assert.Equal(
            Path.GetFullPath(
                artifactPath),
            finding.ArtifactPath);

        Assert.Equal(
            DocumentArtifactReconciliationRecoveryAction.PreserveForRecovery,
            finding.RecoveryAction);

        Assert.True(
            File.Exists(
                artifactPath));

        Document? persistedDocumentAfterReconciliation =
            await environment.Harness.GetDocumentAsync(
                documentId);

        Assert.NotNull(
            persistedDocumentAfterReconciliation);

        Assert.Equal(
            persistedHash,
            persistedDocumentAfterReconciliation.Sha256Hash);

        Assert.Equal(
            persistedDocumentBeforeReconciliation.DisplayName,
            persistedDocumentAfterReconciliation.DisplayName);
    }

    [Fact]
    public async Task ReconcileAsync_WhenStoredPathDoesNotMatchDocumentIdentity_ReportsPathMismatchAndOrphanedArtifact()
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
        Assert.Equal(
            2,
            result.Findings.Count);

        DocumentArtifactReconciliationResult pathMismatchFinding =
            Assert.Single(
                result.Findings,
                finding =>
                    finding.Status ==
                    DocumentArtifactReconciliationStatus.PathMismatch);

        Assert.Equal(
            documentId,
            pathMismatchFinding.DocumentId);

        Assert.Equal(
            Path.GetFullPath(
                mismatchedArtifactPath),
            pathMismatchFinding.ArtifactPath);

        DocumentArtifactReconciliationResult orphanedArtifactFinding =
            Assert.Single(
                result.Findings,
                finding =>
                    finding.Status ==
                    DocumentArtifactReconciliationStatus.OrphanedArtifact);

        Assert.Equal(
            differentArtifactId,
            orphanedArtifactFinding.DocumentId);

        Assert.Equal(
            Path.GetFullPath(
                mismatchedArtifactPath),
            orphanedArtifactFinding.ArtifactPath);

        Assert.Equal(
            DocumentArtifactReconciliationRecoveryAction.CleanupOrphanedArtifact,
            orphanedArtifactFinding.RecoveryAction);

        Assert.True(
            File.Exists(
                orphanedArtifactFinding.ArtifactPath));
    }

    [Fact]
    public async Task ReconcileAsync_WhenStoredPathUsesDifferentDirectoryWithSameDocumentIdentity_ReturnsPathMismatch()
    {
        // Arrange
        await using var environment =
            await TestEnvironment.CreateAsync();

        Guid documentId =
            Guid.NewGuid();

        string sourceFilePath =
            Path.Combine(
                environment.RootDirectory,
                "outside-boundary-source.txt");

        await File.WriteAllTextAsync(
            sourceFilePath,
            "Outside boundary reconciliation test.");

        await environment.Harness.StoreArtifactAsync(
            sourceFilePath,
            documentId);

        string canonicalArtifactPath =
            Path.Combine(
                environment.Harness.DataPaths.DocumentsDirectory,
                $"{documentId}.dvault");

        string outsideDirectory =
            Path.Combine(
                environment.RootDirectory,
                "Outside");

        Directory.CreateDirectory(
            outsideDirectory);

        string outsideArtifactPath =
            Path.Combine(
                outsideDirectory,
                $"{documentId}.dvault");

        File.Move(
            canonicalArtifactPath,
            outsideArtifactPath);

        Document document =
            Document.Create(
                documentId,
                "document.txt",
                "Outside Boundary Test Document",
                "sha256-outside-boundary-test-hash",
                outsideArtifactPath);

        await environment.Harness.PersistDocumentAsync(
            document);

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
                outsideArtifactPath),
            finding.ArtifactPath);

        Assert.True(
            File.Exists(
                outsideArtifactPath));

        Assert.False(
            File.Exists(
                canonicalArtifactPath));
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
