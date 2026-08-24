using DeskVault.Application.Documents.Commands.RemoveDocument;
using DeskVault.Application.Interfaces;
using DeskVault.Domain.Documents;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DeskVault.Application.Tests;

public sealed class RemoveDocumentHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenDocumentDoesNotExist_ReturnsNotFound()
    {
        Guid documentId =
            Guid.NewGuid();

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetByIdAsync(
                documentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Document?)null);

        var storageService =
            new TestStorageService();

        var handler =
            new RemoveDocumentHandler(
                repository.Object,
                storageService,
                NullLogger<RemoveDocumentHandler>.Instance);

        RemoveDocumentResult result =
            await handler.HandleAsync(
                new RemoveDocumentCommand(
                    documentId));

        Assert.Equal(
            RemoveDocumentResultStatus.NotFound,
            result.Status);

        Assert.Equal(
            "The requested document could not be found.",
            result.Message);

        Assert.False(
            storageService.DeleteWasCalled);

        repository.Verify(
            x => x.DeleteAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenStorageDeletionFails_ReturnsStorageDeletionFailed()
    {
        Document document =
            CreateDocument();

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetByIdAsync(
                document.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var storageService =
            new TestStorageService
            {
                ExceptionToThrow =
                    new IOException(
                        "Storage deletion failed.")
            };

        var handler =
            new RemoveDocumentHandler(
                repository.Object,
                storageService,
                NullLogger<RemoveDocumentHandler>.Instance);

        RemoveDocumentResult result =
            await handler.HandleAsync(
                new RemoveDocumentCommand(
                    document.Id));

        Assert.Equal(
            RemoveDocumentResultStatus.StorageDeletionFailed,
            result.Status);

        Assert.Equal(
            "Storage deletion failed.",
            result.Message);

        Assert.True(
            storageService.DeleteWasCalled);

        repository.Verify(
            x => x.DeleteAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenDeletionSucceeds_ReturnsSuccess()
    {
        Document document =
            CreateDocument();

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetByIdAsync(
                document.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var storageService =
            new TestStorageService();

        var handler =
            new RemoveDocumentHandler(
                repository.Object,
                storageService,
                NullLogger<RemoveDocumentHandler>.Instance);

        RemoveDocumentResult result =
            await handler.HandleAsync(
                new RemoveDocumentCommand(
                    document.Id));

        Assert.Equal(
            RemoveDocumentResultStatus.Success,
            result.Status);

        Assert.Equal(
            "Document removed successfully.",
            result.Message);

        Assert.True(
            storageService.DeleteWasCalled);

        repository.Verify(
            x => x.DeleteAsync(
                document.Id,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenMetadataDeletionFails_ReturnsMetadataDeletionFailed()
    {
        Document document =
            CreateDocument();

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetByIdAsync(
                document.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        repository
            .Setup(x => x.DeleteAsync(
                document.Id,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new InvalidOperationException(
                    "Metadata deletion failed."));

        var storageService =
            new TestStorageService();

        var handler =
            new RemoveDocumentHandler(
                repository.Object,
                storageService,
                NullLogger<RemoveDocumentHandler>.Instance);

        RemoveDocumentResult result =
            await handler.HandleAsync(
                new RemoveDocumentCommand(
                    document.Id));

        Assert.Equal(
            RemoveDocumentResultStatus.MetadataDeletionFailed,
            result.Status);

        Assert.Equal(
            "Metadata deletion failed.",
            result.Message);

        Assert.True(
            storageService.DeleteWasCalled);

        repository.Verify(
            x => x.DeleteAsync(
                document.Id,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenStorageDeletionIsUnauthorized_ReturnsStorageDeletionFailed()
    {
        Document document =
            CreateDocument();

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetByIdAsync(
                document.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var storageService =
            new TestStorageService
            {
                ExceptionToThrow =
                    new UnauthorizedAccessException(
                        "Access denied.")
            };

        var handler =
            new RemoveDocumentHandler(
                repository.Object,
                storageService,
                NullLogger<RemoveDocumentHandler>.Instance);

        RemoveDocumentResult result =
            await handler.HandleAsync(
                new RemoveDocumentCommand(
                    document.Id));

        Assert.Equal(
            RemoveDocumentResultStatus.StorageDeletionFailed,
            result.Status);

        Assert.Equal(
            "Access denied.",
            result.Message);

        Assert.DoesNotContain(
            repository.Invocations,
            invocation =>
                invocation.Method.Name ==
                nameof(IDocumentRepository.DeleteAsync));
    }

    [Fact]
    public async Task HandleAsync_WhenMetadataDeletionThrowsIOException_ReturnsMetadataDeletionFailed()
    {
        Document document =
            CreateDocument();

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetByIdAsync(
                document.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        repository
            .Setup(x => x.DeleteAsync(
                document.Id,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new IOException(
                    "Metadata storage operation failed."));

        var storageService =
            new TestStorageService();

        var handler =
            new RemoveDocumentHandler(
                repository.Object,
                storageService,
                NullLogger<RemoveDocumentHandler>.Instance);

        RemoveDocumentResult result =
            await handler.HandleAsync(
                new RemoveDocumentCommand(
                    document.Id));

        Assert.Equal(
            RemoveDocumentResultStatus.MetadataDeletionFailed,
            result.Status);

        Assert.Equal(
            "Metadata storage operation failed.",
            result.Message);

        Assert.Contains(
            repository.Invocations,
            invocation =>
                invocation.Method.Name ==
                nameof(IDocumentRepository.DeleteAsync));
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
            return Task.FromResult(
                "stored.dvault");
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
