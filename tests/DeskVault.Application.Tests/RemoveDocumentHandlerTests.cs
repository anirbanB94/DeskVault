using DeskVault.Application.Documents.Commands.RemoveDocument;
using DeskVault.Application.Interfaces;
using DeskVault.Application.Tests.TestDoubles;
using DeskVault.Domain.Documents;
using DeskVault.Shared.Resources;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using static DeskVault.Application.Tests.TestDoubles.SafeDiagnosticLogAssertions;

namespace DeskVault.Application.Tests;

public sealed class RemoveDocumentHandlerTests
{

    private static readonly string[] SuccessfulRemovalLeaseLifecycle =
    [
        "LeaseAcquired",
        "MetadataRead",
        "ArtifactDeleted",
        "WorkspaceMembershipsRemoved",
        "MetadataDeleted",
        "LeaseReleased"
    ];

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
            await handler.HandleAsync(new RemoveDocumentCommand(
                    documentId), TestContext.Current.CancellationToken);

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
                    CreateSensitiveDetails(),
                    new InvalidOperationException(CreateSensitiveDetails())));

        var logger = new RecordingLogger<RemoveDocumentHandler>();

        var handler =
            CreateHandler(
                repository,
                workspaceRepository,
                storageService,
                logger);

        // Act
        RemoveDocumentResult result =
            await handler.HandleAsync(new RemoveDocumentCommand(
                    document.Id), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            RemoveDocumentResultStatus.StorageDeletionFailed,
            result.Status);

        Assert.Equal(
            "The document could not be removed because its stored artifact could not be deleted.",
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

        AssertSafeFailureLog(
            logger.Entries,
            LogLevel.Error,
            DiagnosticOperationCodes.DocumentRemovalStorageDeletionFailure,
            LogMessages.DocumentStorageDeletionFailed,
            nameof(IOException));
    }

    [Fact]
    public async Task HandleAsync_WhenDeletionSucceeds_HoldsLeaseAcrossRemovalLifecycle()
    {
        // Arrange
        Document document =
            CreateDocument();

        List<string> lifecycle =
            [];

        var repository =
            new Mock<IDocumentRepository>();

        var workspaceRepository =
            new Mock<IWorkspaceRepository>();

        var storageService =
            new Mock<IStorageService>();

        var lease =
            new Mock<IAsyncDisposable>();

        var coordinator =
            new Mock<IDocumentArtifactCoordinator>();

        repository
            .Setup(x => x.GetByIdAsync(
                document.Id,
                It.IsAny<CancellationToken>()))
            .Callback<Guid, CancellationToken>(
                (_, _) => lifecycle.Add("MetadataRead"))
            .ReturnsAsync(document);

        coordinator
            .Setup(x => x.AcquireAsync(
                document.Id,
                It.IsAny<CancellationToken>()))
            .Callback<Guid, CancellationToken>(
                (_, _) => lifecycle.Add("LeaseAcquired"))
            .ReturnsAsync(lease.Object);

        lease
            .Setup(x => x.DisposeAsync())
            .Callback(() => lifecycle.Add("LeaseReleased"))
            .Returns(ValueTask.CompletedTask);

        storageService
            .Setup(x => x.DeleteAsync(
                document.Id,
                It.IsAny<CancellationToken>()))
            .Callback<Guid, CancellationToken>(
                (_, _) => lifecycle.Add("ArtifactDeleted"))
            .Returns(Task.CompletedTask);

        workspaceRepository
            .Setup(x => x.RemoveDocumentFromAllWorkspacesAsync(
                document.Id,
                It.IsAny<CancellationToken>()))
            .Callback<Guid, CancellationToken>(
                (_, _) => lifecycle.Add("WorkspaceMembershipsRemoved"))
            .Returns(Task.CompletedTask);

        repository
            .Setup(x => x.DeleteAsync(
                document.Id,
                It.IsAny<CancellationToken>()))
            .Callback<Guid, CancellationToken>(
                (_, _) => lifecycle.Add("MetadataDeleted"))
            .Returns(Task.CompletedTask);

        var handler =
            CreateHandler(
                repository,
                workspaceRepository,
                storageService,
                artifactCoordinator: coordinator);

        // Act
        RemoveDocumentResult result =
            await handler.HandleAsync(
                new RemoveDocumentCommand(document.Id),
                TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            RemoveDocumentResultStatus.Success,
            result.Status);

        Assert.Equal(
            "Document removed successfully.",
            result.Message);

        Assert.Equal(
            SuccessfulRemovalLeaseLifecycle,
            lifecycle);

        coordinator.Verify(
            x => x.AcquireAsync(
                document.Id,
                It.IsAny<CancellationToken>()),
            Times.Once);

        storageService.Verify(
            x => x.DeleteAsync(
                document.Id,
                It.IsAny<CancellationToken>()),
            Times.Once);

        workspaceRepository.Verify(
            x => x.RemoveDocumentFromAllWorkspacesAsync(
                document.Id,
                It.IsAny<CancellationToken>()),
            Times.Once);

        repository.Verify(
            x => x.DeleteAsync(
                document.Id,
                It.IsAny<CancellationToken>()),
            Times.Once);

        lease.Verify(
            x => x.DisposeAsync(),
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
                    CreateSensitiveDetails(),
                    new IOException(CreateSensitiveDetails())));

        var logger = new RecordingLogger<RemoveDocumentHandler>();

        var workspaceRepository =
            new Mock<IWorkspaceRepository>();

        var storageService =
            new Mock<IStorageService>();

        var handler =
            CreateHandler(
                repository,
                workspaceRepository,
                storageService,
                logger);

        // Act
        RemoveDocumentResult result =
            await handler.HandleAsync(new RemoveDocumentCommand(
                    document.Id), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            RemoveDocumentResultStatus.MetadataDeletionFailed,
            result.Status);

        Assert.Equal(
            "Document removal is incomplete because metadata cleanup failed.",
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

        AssertSafeFailureLog(
            logger.Entries,
            LogLevel.Error,
            DiagnosticOperationCodes.DocumentRemovalMetadataDeletionFailure,
            LogMessages.DocumentMetadataDeletionFailed,
            nameof(InvalidOperationException));
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
                    CreateSensitiveDetails(),
                    new InvalidOperationException(CreateSensitiveDetails())));

        var logger = new RecordingLogger<RemoveDocumentHandler>();

        var handler =
            CreateHandler(
                repository,
                workspaceRepository,
                storageService,
                logger);

        // Act
        RemoveDocumentResult result =
            await handler.HandleAsync(new RemoveDocumentCommand(
                    document.Id), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            RemoveDocumentResultStatus.StorageDeletionFailed,
            result.Status);

        Assert.Equal(
            "The document could not be removed because its stored artifact could not be deleted.",
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

        AssertSafeFailureLog(
            logger.Entries,
            LogLevel.Error,
            DiagnosticOperationCodes.DocumentRemovalStorageDeletionFailure,
            LogMessages.DocumentStorageDeletionFailed,
            nameof(UnauthorizedAccessException));
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
                    CreateSensitiveDetails(),
                    new InvalidOperationException(CreateSensitiveDetails())));

        var logger = new RecordingLogger<RemoveDocumentHandler>();

        var workspaceRepository =
            new Mock<IWorkspaceRepository>();

        var storageService =
            new Mock<IStorageService>();

        var handler =
            CreateHandler(
                repository,
                workspaceRepository,
                storageService,
                logger);

        // Act
        RemoveDocumentResult result =
            await handler.HandleAsync(new RemoveDocumentCommand(
                    document.Id), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            RemoveDocumentResultStatus.MetadataDeletionFailed,
            result.Status);

        Assert.Equal(
            "Document removal is incomplete because metadata cleanup failed.",
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

        AssertSafeFailureLog(
            logger.Entries,
            LogLevel.Error,
            DiagnosticOperationCodes.DocumentRemovalMetadataDeletionFailure,
            LogMessages.DocumentMetadataDeletionFailed,
            nameof(IOException));
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
                    CreateSensitiveDetails(),
                    new IOException(CreateSensitiveDetails())));

        var logger = new RecordingLogger<RemoveDocumentHandler>();

        var storageService =
            new Mock<IStorageService>();

        var handler =
            CreateHandler(
                repository,
                workspaceRepository,
                storageService,
                logger);

        // Act
        RemoveDocumentResult result =
            await handler.HandleAsync(new RemoveDocumentCommand(
                    document.Id), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            RemoveDocumentResultStatus.WorkspaceMembershipCleanupFailed,
            result.Status);

        Assert.Equal(
            "Document removal is incomplete because workspace membership cleanup failed.",
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

        AssertSafeFailureLog(
            logger.Entries,
            LogLevel.Error,
            DiagnosticOperationCodes.DocumentRemovalWorkspaceMembershipCleanupFailure,
            LogMessages.DocumentWorkspaceMembershipCleanupFailed,
            nameof(InvalidOperationException));
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
            await handler.HandleAsync(new RemoveDocumentCommand(
                    document.Id), TestContext.Current.CancellationToken);

        RemoveDocumentResult retryResult =
            await handler.HandleAsync(new RemoveDocumentCommand(
                    document.Id), TestContext.Current.CancellationToken);

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
            await handler.HandleAsync(new RemoveDocumentCommand(
                    document.Id), TestContext.Current.CancellationToken);

        RemoveDocumentResult retryResult =
            await handler.HandleAsync(new RemoveDocumentCommand(
                    document.Id), TestContext.Current.CancellationToken);

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

    [Fact]
    public async Task HandleAsync_WhenCoordinationAcquisitionFails_DoesNotReadOrMutateDocument()
    {
        // Arrange
        Guid documentId =
            Guid.NewGuid();

        var repository =
            new Mock<IDocumentRepository>();

        var workspaceRepository =
            new Mock<IWorkspaceRepository>();

        var storageService =
            new Mock<IStorageService>();

        var coordinator =
            new Mock<IDocumentArtifactCoordinator>();

        var logger =
            new RecordingLogger<RemoveDocumentHandler>();

        coordinator
            .Setup(x => x.AcquireAsync(
                documentId,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new IOException(
                    CreateSensitiveDetails()));

        var handler =
            CreateHandler(
                repository,
                workspaceRepository,
                storageService,
                logger,
                coordinator);

        // Act
        await Assert.ThrowsAsync<IOException>(
            () => handler.HandleAsync(
                new RemoveDocumentCommand(documentId),
                TestContext.Current.CancellationToken));

        // Assert
        coordinator.Verify(
            x => x.AcquireAsync(
                documentId,
                It.IsAny<CancellationToken>()),
            Times.Once);

        repository.Verify(
            x => x.GetByIdAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        storageService.Verify(
            x => x.DeleteAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        workspaceRepository.Verify(
            x => x.RemoveDocumentFromAllWorkspacesAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        repository.Verify(
            x => x.DeleteAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        AssertSafeFailureLog(
            logger.Entries,
            LogLevel.Error,
            DiagnosticOperationCodes.DocumentArtifactCoordinationFailure,
            LogMessages.DocumentArtifactCoordinationFailed,
            nameof(IOException));
    }

    [Fact]
    public async Task HandleAsync_WhenCoordinationAcquisitionIsCancelled_DoesNotReadOrMutateDocument()
    {
        // Arrange
        Guid documentId =
            Guid.NewGuid();

        var repository =
            new Mock<IDocumentRepository>();

        var workspaceRepository =
            new Mock<IWorkspaceRepository>();

        var storageService =
            new Mock<IStorageService>();

        var coordinator =
            new Mock<IDocumentArtifactCoordinator>();

        coordinator
            .Setup(x => x.AcquireAsync(
                documentId,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new OperationCanceledException());

        var handler =
            CreateHandler(
                repository,
                workspaceRepository,
                storageService,
                artifactCoordinator: coordinator);

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => handler.HandleAsync(
                new RemoveDocumentCommand(documentId),
                TestContext.Current.CancellationToken));

        // Assert
        coordinator.Verify(
            x => x.AcquireAsync(
                documentId,
                It.IsAny<CancellationToken>()),
            Times.Once);

        repository.Verify(
            x => x.GetByIdAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        storageService.Verify(
            x => x.DeleteAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        workspaceRepository.Verify(
            x => x.RemoveDocumentFromAllWorkspacesAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        repository.Verify(
            x => x.DeleteAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
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
        Mock<IStorageService> storageService,
        ILogger<RemoveDocumentHandler>? logger = null,
        Mock<IDocumentArtifactCoordinator>? artifactCoordinator = null)
    {
        Mock<IDocumentArtifactCoordinator> coordinator =
            artifactCoordinator ?? CreateCoordinator();

        return new RemoveDocumentHandler(
            repository.Object,
            workspaceRepository.Object,
            storageService.Object,
            coordinator.Object,
            logger ?? NullLogger<RemoveDocumentHandler>.Instance);
    }

    private static Mock<IDocumentArtifactCoordinator> CreateCoordinator()
    {
        var coordinator =
            new Mock<IDocumentArtifactCoordinator>();

        coordinator
            .Setup(x => x.AcquireAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new TestDocumentArtifactLease());

        return coordinator;
    }

    private sealed class TestDocumentArtifactLease : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
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
