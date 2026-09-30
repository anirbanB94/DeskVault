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
        // Arrange
        Guid documentId =
            Guid.NewGuid();

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetByIdAsync(
                documentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Document?)null);

        var workspaceRepository =
            new Mock<IWorkspaceRepository>();

        var storageService =
            new Mock<IStorageService>();

        var handler =
            CreateHandler(
                repository,
                workspaceRepository,
                storageService);

        // Act
        RemoveDocumentResult result =
            await handler.HandleAsync(
                new RemoveDocumentCommand(
                    documentId));

        // Assert
        Assert.Equal(
            RemoveDocumentResultStatus.NotFound,
            result.Status);

        Assert.Equal(
            "The requested document could not be found.",
            result.Message);

        storageService.Verify(
            x => x.DeleteAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        repository.Verify(
            x => x.DeleteAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        workspaceRepository.Verify(
            x => x.RemoveDocumentFromAllWorkspacesAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenStorageDeletionFails_ReturnsStorageDeletionFailed()
    {
        // Arrange
        Document document =
            CreateDocument();

        var repository =
            CreateRepository(
                document);

        var workspaceRepository =
            new Mock<IWorkspaceRepository>();

        var storageService =
            new Mock<IStorageService>();

        storageService
            .Setup(x => x.DeleteAsync(
                document.Id,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new IOException(
                    "Storage deletion failed."));

        var handler =
            CreateHandler(
                repository,
                workspaceRepository,
                storageService);

        // Act
        RemoveDocumentResult result =
            await handler.HandleAsync(
                new RemoveDocumentCommand(
                    document.Id));

        // Assert
        Assert.Equal(
            RemoveDocumentResultStatus.StorageDeletionFailed,
            result.Status);

        Assert.Equal(
            "Storage deletion failed.",
            result.Message);

        storageService.Verify(
            x => x.DeleteAsync(
                document.Id,
                It.IsAny<CancellationToken>()),
            Times.Once);

        repository.Verify(
            x => x.DeleteAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        workspaceRepository.Verify(
            x => x.RemoveDocumentFromAllWorkspacesAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenDeletionSucceeds_ReturnsSuccess()
    {
        // Arrange
        Document document =
            CreateDocument();

        var repository =
            CreateRepository(
                document);

        var workspaceRepository =
            new Mock<IWorkspaceRepository>();

        var storageService =
            new Mock<IStorageService>();

        var handler =
            CreateHandler(
                repository,
                workspaceRepository,
                storageService);

        // Act
        RemoveDocumentResult result =
            await handler.HandleAsync(
                new RemoveDocumentCommand(
                    document.Id));

        // Assert
        Assert.Equal(
            RemoveDocumentResultStatus.Success,
            result.Status);

        Assert.Equal(
            "Document removed successfully.",
            result.Message);

        storageService.Verify(
            x => x.DeleteAsync(
                document.Id,
                It.IsAny<CancellationToken>()),
            Times.Once);

        repository.Verify(
            x => x.DeleteAsync(
                document.Id,
                It.IsAny<CancellationToken>()),
            Times.Once);

        workspaceRepository.Verify(
            x => x.RemoveDocumentFromAllWorkspacesAsync(
                document.Id,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenMetadataDeletionFails_ReturnsMetadataDeletionFailed()
    {
        // Arrange
        Document document =
            CreateDocument();

        var repository =
            CreateRepository(
                document);

        repository
            .Setup(x => x.DeleteAsync(
                document.Id,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new InvalidOperationException(
                    "Metadata deletion failed."));

        var workspaceRepository =
            new Mock<IWorkspaceRepository>();

        var storageService =
            new Mock<IStorageService>();

        var handler =
            CreateHandler(
                repository,
                workspaceRepository,
                storageService);

        // Act
        RemoveDocumentResult result =
            await handler.HandleAsync(
                new RemoveDocumentCommand(
                    document.Id));

        // Assert
        Assert.Equal(
            RemoveDocumentResultStatus.MetadataDeletionFailed,
            result.Status);

        Assert.Equal(
            "Metadata deletion failed.",
            result.Message);

        storageService.Verify(
            x => x.DeleteAsync(
                document.Id,
                It.IsAny<CancellationToken>()),
            Times.Once);

        repository.Verify(
            x => x.DeleteAsync(
                document.Id,
                It.IsAny<CancellationToken>()),
            Times.Once);

        workspaceRepository.Verify(
            x => x.RemoveDocumentFromAllWorkspacesAsync(
                document.Id,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenStorageDeletionIsUnauthorized_ReturnsStorageDeletionFailed()
    {
        // Arrange
        Document document =
            CreateDocument();

        var repository =
            CreateRepository(
                document);

        var workspaceRepository =
            new Mock<IWorkspaceRepository>();

        var storageService =
            new Mock<IStorageService>();

        storageService
            .Setup(x => x.DeleteAsync(
                document.Id,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new UnauthorizedAccessException(
                    "Access denied."));

        var handler =
            CreateHandler(
                repository,
                workspaceRepository,
                storageService);

        // Act
        RemoveDocumentResult result =
            await handler.HandleAsync(
                new RemoveDocumentCommand(
                    document.Id));

        // Assert
        Assert.Equal(
            RemoveDocumentResultStatus.StorageDeletionFailed,
            result.Status);

        Assert.Equal(
            "Access denied.",
            result.Message);

        storageService.Verify(
            x => x.DeleteAsync(
                document.Id,
                It.IsAny<CancellationToken>()),
            Times.Once);

        repository.Verify(
            x => x.DeleteAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        workspaceRepository.Verify(
            x => x.RemoveDocumentFromAllWorkspacesAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenMetadataDeletionThrowsIOException_ReturnsMetadataDeletionFailed()
    {
        // Arrange
        Document document =
            CreateDocument();

        var repository =
            CreateRepository(
                document);

        repository
            .Setup(x => x.DeleteAsync(
                document.Id,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new IOException(
                    "Metadata storage operation failed."));

        var workspaceRepository =
            new Mock<IWorkspaceRepository>();

        var storageService =
            new Mock<IStorageService>();

        var handler =
            CreateHandler(
                repository,
                workspaceRepository,
                storageService);

        // Act
        RemoveDocumentResult result =
            await handler.HandleAsync(
                new RemoveDocumentCommand(
                    document.Id));

        // Assert
        Assert.Equal(
            RemoveDocumentResultStatus.MetadataDeletionFailed,
            result.Status);

        Assert.Equal(
            "Metadata storage operation failed.",
            result.Message);

        storageService.Verify(
            x => x.DeleteAsync(
                document.Id,
                It.IsAny<CancellationToken>()),
            Times.Once);

        repository.Verify(
            x => x.DeleteAsync(
                document.Id,
                It.IsAny<CancellationToken>()),
            Times.Once);

        workspaceRepository.Verify(
            x => x.RemoveDocumentFromAllWorkspacesAsync(
                document.Id,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenWorkspaceMembershipCleanupFails_ReturnsWorkspaceMembershipCleanupFailed()
    {
        // Arrange
        Document document =
            CreateDocument();

        var repository =
            CreateRepository(
                document);

        repository
            .Setup(x => x.DeleteAsync(
                document.Id,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var workspaceRepository =
            new Mock<IWorkspaceRepository>();

        workspaceRepository
            .Setup(x => x.RemoveDocumentFromAllWorkspacesAsync(
                document.Id,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new InvalidOperationException(
                    "Workspace membership cleanup failed."));

        var storageService =
            new Mock<IStorageService>();

        var handler =
            CreateHandler(
                repository,
                workspaceRepository,
                storageService);

        // Act
        RemoveDocumentResult result =
            await handler.HandleAsync(
                new RemoveDocumentCommand(
                    document.Id));

        // Assert
        Assert.Equal(
            RemoveDocumentResultStatus.WorkspaceMembershipCleanupFailed,
            result.Status);

        Assert.Equal(
            "Workspace membership cleanup failed.",
            result.Message);

        storageService.Verify(
            x => x.DeleteAsync(
                document.Id,
                It.IsAny<CancellationToken>()),
            Times.Once);

        repository.Verify(
            x => x.DeleteAsync(
                document.Id,
                It.IsAny<CancellationToken>()),
            Times.Never);

        workspaceRepository.Verify(
            x => x.RemoveDocumentFromAllWorkspacesAsync(
                document.Id,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenWorkspaceCleanupFails_RetryCompletesRemoval()
    {
        // Arrange
        Document document =
            CreateDocument();

        var repository =
            CreateRepository(
                document);

        var workspaceRepository =
            new Mock<IWorkspaceRepository>();

        workspaceRepository
            .SetupSequence(x => x.RemoveDocumentFromAllWorkspacesAsync(
                document.Id,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new InvalidOperationException(
                    "Workspace cleanup failed."))
            .Returns(
                Task.CompletedTask);

        var storageService =
            new Mock<IStorageService>();

        var handler =
            CreateHandler(
                repository,
                workspaceRepository,
                storageService);

        // Act
        RemoveDocumentResult firstResult =
            await handler.HandleAsync(
                new RemoveDocumentCommand(
                    document.Id));

        RemoveDocumentResult retryResult =
            await handler.HandleAsync(
                new RemoveDocumentCommand(
                    document.Id));

        // Assert
        Assert.Equal(
            RemoveDocumentResultStatus.WorkspaceMembershipCleanupFailed,
            firstResult.Status);

        Assert.Equal(
            RemoveDocumentResultStatus.Success,
            retryResult.Status);

        storageService.Verify(
            x => x.DeleteAsync(
                document.Id,
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));

        workspaceRepository.Verify(
            x => x.RemoveDocumentFromAllWorkspacesAsync(
                document.Id,
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));

        repository.Verify(
            x => x.DeleteAsync(
                document.Id,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenMetadataDeletionFails_RetryCompletesRemoval()
    {
        // Arrange
        Document document =
            CreateDocument();

        var repository =
            CreateRepository(
                document);

        repository
            .SetupSequence(x => x.DeleteAsync(
                document.Id,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new InvalidOperationException(
                    "Metadata deletion failed."))
            .Returns(
                Task.CompletedTask);

        var workspaceRepository =
            new Mock<IWorkspaceRepository>();

        var storageService =
            new Mock<IStorageService>();

        var handler =
            CreateHandler(
                repository,
                workspaceRepository,
                storageService);

        // Act
        RemoveDocumentResult firstResult =
            await handler.HandleAsync(
                new RemoveDocumentCommand(
                    document.Id));

        RemoveDocumentResult retryResult =
            await handler.HandleAsync(
                new RemoveDocumentCommand(
                    document.Id));

        // Assert
        Assert.Equal(
            RemoveDocumentResultStatus.MetadataDeletionFailed,
            firstResult.Status);

        Assert.Equal(
            RemoveDocumentResultStatus.Success,
            retryResult.Status);

        storageService.Verify(
            x => x.DeleteAsync(
                document.Id,
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));

        workspaceRepository.Verify(
            x => x.RemoveDocumentFromAllWorkspacesAsync(
                document.Id,
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));

        repository.Verify(
            x => x.DeleteAsync(
                document.Id,
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    private static Mock<IDocumentRepository> CreateRepository(
        Document document)
    {
        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetByIdAsync(
                document.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        return repository;
    }

    private static RemoveDocumentHandler CreateHandler(
        Mock<IDocumentRepository> repository,
        Mock<IWorkspaceRepository> workspaceRepository,
        Mock<IStorageService> storageService)
    {
        return new RemoveDocumentHandler(
            repository.Object,
            workspaceRepository.Object,
            storageService.Object,
            NullLogger<RemoveDocumentHandler>.Instance);
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
}
