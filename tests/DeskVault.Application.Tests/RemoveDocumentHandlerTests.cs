using DeskVault.Application.Documents.Commands.RemoveDocument;
using DeskVault.Application.Interfaces;
using DeskVault.Domain.Documents;

namespace DeskVault.Application.Tests;

public sealed class RemoveDocumentHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenDocumentDoesNotExist_ReturnsNotFound()
    {
        var repository =
            new TestDocumentRepository(null);

        var storageService =
            new TestStorageService();

        var handler =
            new RemoveDocumentHandler(
                repository,
                storageService);

        RemoveDocumentResult result =
            await handler.HandleAsync(
                new RemoveDocumentCommand(Guid.NewGuid()));

        Assert.Equal(
            RemoveDocumentResultStatus.NotFound,
            result.Status);

        Assert.Equal(
            "The requested document could not be found.",
            result.Message);

        Assert.False(storageService.DeleteWasCalled);
        Assert.False(repository.DeleteWasCalled);
    }

    [Fact]
    public async Task HandleAsync_WhenStorageDeletionFails_ReturnsStorageDeletionFailed()
    {
        Document document =
            CreateDocument();

        var repository =
            new TestDocumentRepository(document);

        var storageService =
            new TestStorageService
            {
                ExceptionToThrow =
                    new IOException("Storage deletion failed.")
            };

        var handler =
            new RemoveDocumentHandler(
                repository,
                storageService);

        RemoveDocumentResult result =
            await handler.HandleAsync(
                new RemoveDocumentCommand(document.Id));

        Assert.Equal(
            RemoveDocumentResultStatus.StorageDeletionFailed,
            result.Status);

        Assert.Equal(
            "Storage deletion failed.",
            result.Message);

        Assert.True(storageService.DeleteWasCalled);
        Assert.False(repository.DeleteWasCalled);
    }

    [Fact]
    public async Task HandleAsync_WhenDeletionSucceeds_ReturnsSuccess()
    {
        Document document =
            CreateDocument();

        var repository =
            new TestDocumentRepository(document);

        var storageService =
            new TestStorageService();

        var handler =
            new RemoveDocumentHandler(
                repository,
                storageService);

        RemoveDocumentResult result =
            await handler.HandleAsync(
                new RemoveDocumentCommand(document.Id));

        Assert.Equal(
            RemoveDocumentResultStatus.Success,
            result.Status);

        Assert.Equal(
            "Document removed successfully.",
            result.Message);

        Assert.True(storageService.DeleteWasCalled);
        Assert.True(repository.DeleteWasCalled);

        Assert.Equal(
            document.Id,
            repository.DeletedDocumentId);
    }

    [Fact]
    public async Task HandleAsync_WhenMetadataDeletionFails_ReturnsMetadataDeletionFailed()
    {
        Document document =
            CreateDocument();

        var repository =
            new TestDocumentRepository(document)
            {
                DeleteException =
                    new InvalidOperationException(
                        "Metadata deletion failed.")
            };

        var storageService =
            new TestStorageService();

        var handler =
            new RemoveDocumentHandler(
                repository,
                storageService);

        RemoveDocumentResult result =
            await handler.HandleAsync(
                new RemoveDocumentCommand(document.Id));

        Assert.Equal(
            RemoveDocumentResultStatus.MetadataDeletionFailed,
            result.Status);

        Assert.Equal(
            "Metadata deletion failed.",
            result.Message);

        Assert.True(storageService.DeleteWasCalled);
        Assert.True(repository.DeleteWasCalled);
    }

    [Fact]
    public async Task HandleAsync_WhenStorageDeletionIsUnauthorized_ReturnsStorageDeletionFailed()
    {
        Document document =
            CreateDocument();

        var repository =
            new TestDocumentRepository(document);

        var storageService =
            new TestStorageService
            {
                ExceptionToThrow =
                    new UnauthorizedAccessException(
                        "Access denied.")
            };

        var handler =
            new RemoveDocumentHandler(
                repository,
                storageService);

        RemoveDocumentResult result =
            await handler.HandleAsync(
                new RemoveDocumentCommand(document.Id));

        Assert.Equal(
            RemoveDocumentResultStatus.StorageDeletionFailed,
            result.Status);

        Assert.Equal(
            "Access denied.",
            result.Message);

        Assert.False(repository.DeleteWasCalled);
    }

    [Fact]
    public async Task HandleAsync_WhenMetadataDeletionThrowsIOException_ReturnsMetadataDeletionFailed()
    {
        Document document =
            CreateDocument();

        var repository =
            new TestDocumentRepository(document)
            {
                DeleteException =
                    new IOException(
                        "Metadata storage operation failed.")
            };

        var storageService =
            new TestStorageService();

        var handler =
            new RemoveDocumentHandler(
                repository,
                storageService);

        RemoveDocumentResult result =
            await handler.HandleAsync(
                new RemoveDocumentCommand(document.Id));

        Assert.Equal(
            RemoveDocumentResultStatus.MetadataDeletionFailed,
            result.Status);

        Assert.Equal(
            "Metadata storage operation failed.",
            result.Message);

        Assert.True(repository.DeleteWasCalled);
    }

    private static Document CreateDocument()
    {
        return Document.Create(
            Guid.NewGuid(),
            "document.txt",
            "Test Document",
            "sha256-test-hash",
            "document.dvault");
    }

    private sealed class TestDocumentRepository
        : IDocumentRepository
    {
        private readonly Document? _document;

        public TestDocumentRepository(
            Document? document)
        {
            _document = document;
        }

        public bool DeleteWasCalled { get; private set; }

        public Guid? DeletedDocumentId { get; private set; }

        public Exception? DeleteException { get; set; }

        public Task<bool> ExistsByHashAsync(
            string sha256Hash,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }

        public Task AddAsync(
            Document document,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<Document?> GetByIdAsync(
            Guid documentId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_document);
        }

        public Task<IReadOnlyList<Document>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Document>>(
                Array.Empty<Document>());
        }

        public Task DeleteAsync(
            Guid documentId,
            CancellationToken cancellationToken = default)
        {
            DeleteWasCalled = true;
            DeletedDocumentId = documentId;

            if (DeleteException is not null)
            {
                throw DeleteException;
            }

            return Task.CompletedTask;
        }
    }

    private sealed class TestStorageService
        : IStorageService
    {
        public bool DeleteWasCalled { get; private set; }

        public Exception? ExceptionToThrow { get; set; }

        public Task<string> StoreAsync(
            string sourceFilePath,
            Guid documentId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult("stored.dvault");
        }

        public Task DeleteAsync(
            string storedFilePath,
            CancellationToken cancellationToken = default)
        {
            DeleteWasCalled = true;

            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return Task.CompletedTask;
        }
    }
}
