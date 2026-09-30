using DeskVault.Application.Documents.Commands.ImportDocument;
using DeskVault.Application.Documents.Commands.RemoveDocument;
using DeskVault.Application.Interfaces;
using DeskVault.Domain.Documents;
using DeskVault.Domain.Workspaces;

namespace DeskVault.Integration.Tests;

public sealed class DocumentRemovalIntegrationTests
{
    [Fact]
    public async Task RemoveDocument_WhenMetadataDeletionFails_RetryCompletesRemoval()
    {
        // Arrange
        await using var environment =
            await TestEnvironment.CreateAsync(
                repository =>
                    new FailOnceDocumentRepository(
                        repository));

        string sourceFilePath =
            Path.Combine(
                environment.RootDirectory,
                "retry-removal.txt");

        await File.WriteAllTextAsync(
            sourceFilePath,
            "Document removal retry integration test.");

        ImportDocumentResult importResult =
            await environment.Harness.ImportHandler.HandleAsync(
                new ImportDocumentCommand(
                    sourceFilePath,
                    "Retry Removal Integration Test"));

        Assert.Equal(
            ImportDocumentResultStatus.Success,
            importResult.Status);

        Assert.NotNull(
            importResult.DocumentId);

        Guid documentId =
            importResult.DocumentId.Value;

        Document? document =
            await environment.Harness.GetDocumentAsync(
                documentId);

        Assert.NotNull(
            document);

        string artifactPath =
            document.StoredFilePath;

        Assert.True(
            File.Exists(
                artifactPath));

        Workspace workspace =
            Workspace.CreatePersistent(
                Guid.NewGuid(),
                "Removal Retry Workspace");

        workspace.AddDocument(
            documentId);

        await environment.Harness.PersistWorkspaceAsync(
            workspace);

        // Act
        RemoveDocumentResult firstResult =
            await environment.Harness.RemoveHandler.HandleAsync(
                new RemoveDocumentCommand(
                    documentId));

        // Assert
        Assert.Equal(
            RemoveDocumentResultStatus.MetadataDeletionFailed,
            firstResult.Status);

        Assert.False(
            File.Exists(
                artifactPath));

        Assert.NotNull(
            await environment.Harness.GetDocumentAsync(
                documentId));

        Workspace? partialWorkspace =
            await environment.Harness.GetWorkspaceAsync(
                workspace.Id);

        Assert.NotNull(
            partialWorkspace);

        Assert.Empty(
            partialWorkspace.Memberships);

        // Act
        RemoveDocumentResult retryResult =
            await environment.Harness.RemoveHandler.HandleAsync(
                new RemoveDocumentCommand(
                    documentId));

        // Assert
        Assert.Equal(
            RemoveDocumentResultStatus.Success,
            retryResult.Status);

        Assert.False(
            File.Exists(
                artifactPath));

        Assert.Null(
            await environment.Harness.GetDocumentAsync(
                documentId));

        Workspace? finalWorkspace =
            await environment.Harness.GetWorkspaceAsync(
                workspace.Id);

        Assert.NotNull(
            finalWorkspace);

        Assert.Empty(
            finalWorkspace.Memberships);
    }

    [Fact]
    public async Task RemoveDocument_WhenArtifactIsAlreadyMissing_CompletesSuccessfully()
    {
        // Arrange
        await using var environment =
            await TestEnvironment.CreateAsync();

        string sourceFilePath =
            Path.Combine(
                environment.RootDirectory,
                "missing-artifact.txt");

        await File.WriteAllTextAsync(
            sourceFilePath,
            "Missing artifact removal integration test.");

        ImportDocumentResult importResult =
            await environment.Harness.ImportHandler.HandleAsync(
                new ImportDocumentCommand(
                    sourceFilePath,
                    "Missing Artifact Removal Test"));

        Assert.Equal(
            ImportDocumentResultStatus.Success,
            importResult.Status);

        Assert.NotNull(
            importResult.DocumentId);

        Guid documentId =
            importResult.DocumentId.Value;

        Document? document =
            await environment.Harness.GetDocumentAsync(
                documentId);

        Assert.NotNull(
            document);

        string artifactPath =
            document.StoredFilePath;

        Assert.True(
            File.Exists(
                artifactPath));

        File.Delete(
            artifactPath);

        // Act
        RemoveDocumentResult result =
            await environment.Harness.RemoveHandler.HandleAsync(
                new RemoveDocumentCommand(
                    documentId));

        // Assert
        Assert.Equal(
            RemoveDocumentResultStatus.Success,
            result.Status);

        Assert.False(
            File.Exists(
                artifactPath));

        Assert.Null(
            await environment.Harness.GetDocumentAsync(
                documentId));
    }

    [Fact]
    public async Task RemoveDocument_WhenAnotherDocumentExists_PreservesUnrelatedDocumentArtifactAndMembership()
    {
        // Arrange
        await using var environment =
            await TestEnvironment.CreateAsync();

        string firstSourceFilePath =
            Path.Combine(
                environment.RootDirectory,
                "first-document.txt");

        string secondSourceFilePath =
            Path.Combine(
                environment.RootDirectory,
                "second-document.txt");

        await File.WriteAllTextAsync(
            firstSourceFilePath,
            "First document.");

        await File.WriteAllTextAsync(
            secondSourceFilePath,
            "Second document.");

        ImportDocumentResult firstImportResult =
            await environment.Harness.ImportHandler.HandleAsync(
                new ImportDocumentCommand(
                    firstSourceFilePath,
                    "First Document"));

        ImportDocumentResult secondImportResult =
            await environment.Harness.ImportHandler.HandleAsync(
                new ImportDocumentCommand(
                    secondSourceFilePath,
                    "Second Document"));

        Assert.Equal(
            ImportDocumentResultStatus.Success,
            firstImportResult.Status);

        Assert.Equal(
            ImportDocumentResultStatus.Success,
            secondImportResult.Status);

        Assert.NotNull(
            firstImportResult.DocumentId);

        Assert.NotNull(
            secondImportResult.DocumentId);

        Guid firstDocumentId =
            firstImportResult.DocumentId.Value;

        Guid secondDocumentId =
            secondImportResult.DocumentId.Value;

        Document? firstDocument =
            await environment.Harness.GetDocumentAsync(
                firstDocumentId);

        Document? secondDocument =
            await environment.Harness.GetDocumentAsync(
                secondDocumentId);

        Assert.NotNull(
            firstDocument);

        Assert.NotNull(
            secondDocument);

        string firstArtifactPath =
            firstDocument.StoredFilePath;

        string secondArtifactPath =
            secondDocument.StoredFilePath;

        Assert.True(
            File.Exists(
                firstArtifactPath));

        Assert.True(
            File.Exists(
                secondArtifactPath));

        Workspace workspace =
            Workspace.CreatePersistent(
                Guid.NewGuid(),
                "Unrelated Document Workspace");

        workspace.AddDocument(
            firstDocumentId);

        workspace.AddDocument(
            secondDocumentId);

        await environment.Harness.PersistWorkspaceAsync(
            workspace);

        // Act
        RemoveDocumentResult result =
            await environment.Harness.RemoveHandler.HandleAsync(
                new RemoveDocumentCommand(
                    firstDocumentId));

        // Assert
        Assert.Equal(
            RemoveDocumentResultStatus.Success,
            result.Status);

        Assert.False(
            File.Exists(
                firstArtifactPath));

        Assert.True(
            File.Exists(
                secondArtifactPath));

        Assert.Null(
            await environment.Harness.GetDocumentAsync(
                firstDocumentId));

        Assert.NotNull(
            await environment.Harness.GetDocumentAsync(
                secondDocumentId));

        Workspace? workspaceResult =
            await environment.Harness.GetWorkspaceAsync(
                workspace.Id);

        Assert.NotNull(
            workspaceResult);

        Assert.DoesNotContain(
            workspaceResult.Memberships,
            membership =>
                membership.DocumentId == firstDocumentId);

        Assert.Contains(
            workspaceResult.Memberships,
            membership =>
                membership.DocumentId == secondDocumentId);
    }

    private sealed class FailOnceDocumentRepository
        : IDocumentRepository
    {
        private readonly IDocumentRepository _inner;

        private bool _hasFailed;

        public FailOnceDocumentRepository(
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

        public Task DeleteAsync(
            Guid documentId,
            CancellationToken cancellationToken = default)
        {
            if (!_hasFailed)
            {
                _hasFailed = true;

                throw new InvalidOperationException(
                    "Injected metadata deletion failure.");
            }

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

        public static Task<TestEnvironment> CreateAsync(
            Func<IDocumentRepository, IDocumentRepository>? removeRepositoryDecorator = null)
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
                System.Security.Cryptography.RandomNumberGenerator.GetBytes(
                    32);

            var harness =
                new DocumentPipelineTestHarness(
                    rootDirectory,
                    databasePath,
                    encryptionKey,
                    removeRepositoryDecorator:
                        removeRepositoryDecorator);

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
