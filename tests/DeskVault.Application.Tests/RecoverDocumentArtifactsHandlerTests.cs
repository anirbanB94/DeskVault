using System.Security.Cryptography;
using DeskVault.Application.Documents.Commands.RecoverDocumentArtifacts;
using DeskVault.Application.Documents.Queries.ReconcileDocumentArtifacts;
using DeskVault.Application.Interfaces;
using DeskVault.Domain.Documents;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DeskVault.Application.Tests;

public sealed class RecoverDocumentArtifactsHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenCanonicalOrphanExists_DeletesArtifactAndReturnsRecovered()
    {
        // Arrange
        Guid orphanId =
            Guid.NewGuid();

        string orphanPath =
            Path.GetFullPath(
                Path.Combine(
                    "Documents",
                    $"{orphanId}.dvault"));

        var repository =
            CreateRepository([]);

        var artifactEnumerator =
            CreateArtifactEnumerator(
                [orphanPath]);

        var reader =
            new Mock<IDocumentReader>();

        var storageService =
            CreateStorageServiceMock();

        var hashService =
            CreateMatchingHashService();

        var handler =
            CreateHandler(
                repository,
                artifactEnumerator,
                reader,
                storageService,
                hashService);

        // Act
        RecoverDocumentArtifactsResult result =
            await handler.HandleAsync(
                new RecoverDocumentArtifactsCommand());

        // Assert
        Assert.Equal(
            RecoverDocumentArtifactsResultStatus.Recovered,
            result.Status);

        var finding =
            Assert.Single(result.Findings);

        Assert.Equal(
            DocumentArtifactReconciliationStatus.OrphanedArtifact,
            finding.Status);

        Assert.Equal(
            orphanId,
            finding.DocumentId);

        Assert.Equal(
            Path.GetFullPath(
                orphanPath),
            finding.ArtifactPath);

        Assert.Equal(
            DocumentArtifactReconciliationRecoveryAction.CleanupOrphanedArtifact,
            finding.RecoveryAction);

        storageService.Verify(
            x => x.IsOwnedArtifactPath(
                orphanId,
                Path.GetFullPath(orphanPath)),
            Times.Once);

        storageService.Verify(
            x => x.DeleteAsync(
                orphanId,
                It.IsAny<CancellationToken>()),
            Times.Once);

        reader.Verify(
            x => x.OpenReadAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenOrphanArtifactIsAlreadyMissing_DeleteRemainsRetrySafe()
    {
        // Arrange
        Guid orphanId =
            Guid.NewGuid();

        string orphanPath =
            Path.GetFullPath(
                Path.Combine(
                    "Documents",
                    $"{orphanId}.dvault"));

        var repository =
            CreateRepository([]);

        var artifactEnumerator =
            new Mock<IDocumentArtifactEnumerator>();

        artifactEnumerator
            .SetupSequence(x => x.EnumerateAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                orphanPath
            ])
            .ReturnsAsync(
                Array.Empty<string>());

        var reader =
            new Mock<IDocumentReader>();

        var storageService =
            CreateStorageServiceMock();

        var hashService =
            CreateMatchingHashService();

        var handler =
            CreateHandler(
                repository,
                artifactEnumerator,
                reader,
                storageService,
                hashService);

        // Act
        RecoverDocumentArtifactsResult firstResult =
            await handler.HandleAsync(
                new RecoverDocumentArtifactsCommand());

        RecoverDocumentArtifactsResult retryResult =
            await handler.HandleAsync(
                new RecoverDocumentArtifactsCommand());

        // Assert
        Assert.Equal(
            RecoverDocumentArtifactsResultStatus.Recovered,
            firstResult.Status);

        Assert.Equal(
            RecoverDocumentArtifactsResultStatus.NoActionRequired,
            retryResult.Status);

        storageService.Verify(
            x => x.DeleteAsync(
                orphanId,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenCleanupIsInterrupted_CanBeRetried()
    {
        // Arrange
        Guid orphanId =
            Guid.NewGuid();

        string orphanPath =
            Path.GetFullPath(
                Path.Combine(
                    "Documents",
                    $"{orphanId}.dvault"));

        var repository =
            CreateRepository([]);

        var artifactEnumerator =
            new Mock<IDocumentArtifactEnumerator>();

        artifactEnumerator
            .SetupSequence(x => x.EnumerateAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                orphanPath
            ])
            .ReturnsAsync(
            [
                orphanPath
            ]);

        var reader =
            new Mock<IDocumentReader>();

        var storageService =
            CreateStorageServiceMock();

        storageService
            .SetupSequence(x => x.DeleteAsync(
                orphanId,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new IOException(
                    "Simulated recovery interruption."))
            .Returns(
                Task.CompletedTask);

        var hashService =
            CreateMatchingHashService();

        var handler =
            CreateHandler(
                repository,
                artifactEnumerator,
                reader,
                storageService,
                hashService);

        // Act
        await Assert.ThrowsAsync<IOException>(
            () =>
                handler.HandleAsync(
                    new RecoverDocumentArtifactsCommand()));

        RecoverDocumentArtifactsResult retryResult =
            await handler.HandleAsync(
                new RecoverDocumentArtifactsCommand());

        // Assert
        Assert.Equal(
            RecoverDocumentArtifactsResultStatus.Recovered,
            retryResult.Status);

        var finding =
            Assert.Single(
                retryResult.Findings);

        Assert.Equal(
            DocumentArtifactReconciliationStatus.OrphanedArtifact,
            finding.Status);

        Assert.Equal(
            orphanId,
            finding.DocumentId);

        Assert.Equal(
            DocumentArtifactReconciliationRecoveryAction.CleanupOrphanedArtifact,
            finding.RecoveryAction);

        storageService.Verify(
            x => x.DeleteAsync(
                orphanId,
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task HandleAsync_WhenArtifactBelongsToPersistedDocument_DoesNotDeleteIt()
    {
        // Arrange
        Document document =
            CreateDocument();

        string expectedArtifactPath =
            Path.GetFullPath(
                document.StoredFilePath);

        var repository =
            CreateRepository([document]);

        var artifactEnumerator =
            CreateArtifactEnumerator(
                [expectedArtifactPath]);

        var reader =
            new Mock<IDocumentReader>();

        reader
            .Setup(x => x.OpenReadAsync(
                document.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new MemoryStream(
                    "decrypted-content"u8.ToArray()));

        var storageService =
            CreateStorageServiceMock();

        var hashService =
            CreateMatchingHashService();

        var handler =
            CreateHandler(
                repository,
                artifactEnumerator,
                reader,
                storageService,
                hashService);

        // Act
        RecoverDocumentArtifactsResult result =
            await handler.HandleAsync(
                new RecoverDocumentArtifactsCommand());

        // Assert
        Assert.Equal(
            RecoverDocumentArtifactsResultStatus.NoActionRequired,
            result.Status);

        var finding =
            Assert.Single(result.Findings);

        Assert.Equal(
            DocumentArtifactReconciliationStatus.Matched,
            finding.Status);

        storageService.Verify(
            x => x.DeleteAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenOrphanPathIsOutsideOwnedBoundary_PreservesArtifact()
    {
        // Arrange
        Guid orphanId =
            Guid.NewGuid();

        string orphanPath =
            Path.GetFullPath(
                Path.Combine(
                    "Outside",
                    $"{orphanId}.dvault"));

        var repository =
            CreateRepository([]);

        var artifactEnumerator =
            CreateArtifactEnumerator(
                [orphanPath]);

        var reader =
            new Mock<IDocumentReader>();

        var storageService =
            new Mock<IStorageService>();

        storageService
            .Setup(x => x.IsOwnedArtifactPath(
                orphanId,
                orphanPath))
            .Returns(false);

        var hashService =
            CreateMatchingHashService();

        var handler =
            CreateHandler(
                repository,
                artifactEnumerator,
                reader,
                storageService,
                hashService);

        // Act
        RecoverDocumentArtifactsResult result =
            await handler.HandleAsync(
                new RecoverDocumentArtifactsCommand());

        // Assert
        Assert.Equal(
            RecoverDocumentArtifactsResultStatus.PreservedForRecovery,
            result.Status);

        var finding =
            Assert.Single(result.Findings);

        Assert.Equal(
            DocumentArtifactReconciliationStatus.OrphanedArtifact,
            result.Findings.Single().Status);

        Assert.Equal(
            DocumentArtifactReconciliationRecoveryAction.PreserveForRecovery,
            finding.RecoveryAction);

        storageService.Verify(
            x => x.DeleteAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenArtifactHasNoParseableDocumentIdentity_PreservesArtifact()
    {
        // Arrange
        string orphanPath =
            Path.GetFullPath(
                Path.Combine(
                    "Documents",
                    "not-a-document-id.dvault"));

        var repository =
            CreateRepository([]);

        var artifactEnumerator =
            CreateArtifactEnumerator(
                [orphanPath]);

        var reader =
            new Mock<IDocumentReader>();

        var storageService =
            CreateStorageServiceMock();

        var hashService =
            CreateMatchingHashService();

        var handler =
            CreateHandler(
                repository,
                artifactEnumerator,
                reader,
                storageService,
                hashService);

        // Act
        RecoverDocumentArtifactsResult result =
            await handler.HandleAsync(
                new RecoverDocumentArtifactsCommand());

        // Assert
        Assert.Equal(
            RecoverDocumentArtifactsResultStatus.PreservedForRecovery,
            result.Status);

        var finding =
            Assert.Single(result.Findings);

        Assert.Equal(
            DocumentArtifactReconciliationStatus.OrphanedArtifact,
            finding.Status);

        Assert.Null(
            finding.DocumentId);

        Assert.Equal(
            DocumentArtifactReconciliationRecoveryAction.PreserveForRecovery,
            finding.RecoveryAction);

        storageService.Verify(
            x => x.DeleteAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenDocumentHasMissingArtifact_PreservesDocument()
    {
        // Arrange
        Document document =
            CreateDocument();

        var repository =
            CreateRepository([document]);

        var artifactEnumerator =
            CreateArtifactEnumerator([]);

        var reader =
            new Mock<IDocumentReader>();

        var storageService =
            CreateStorageServiceMock();

        var hashService =
            CreateMatchingHashService();

        var handler =
            CreateHandler(
                repository,
                artifactEnumerator,
                reader,
                storageService,
                hashService);

        // Act
        RecoverDocumentArtifactsResult result =
            await handler.HandleAsync(
                new RecoverDocumentArtifactsCommand());

        // Assert
        Assert.Equal(
            RecoverDocumentArtifactsResultStatus.PreservedForRecovery,
            result.Status);

        var finding =
            Assert.Single(result.Findings);

        Assert.Equal(
            DocumentArtifactReconciliationStatus.MissingArtifact,
            finding.Status);

        Assert.Equal(
            DocumentArtifactReconciliationRecoveryAction.PreserveForRecovery,
            finding.RecoveryAction);

        storageService.Verify(
            x => x.DeleteAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenArtifactIsUnreadable_PreservesDocument()
    {
        // Arrange
        Document document =
            CreateDocument();

        string expectedArtifactPath =
            Path.GetFullPath(
                document.StoredFilePath);

        var repository =
            CreateRepository([document]);

        var artifactEnumerator =
            CreateArtifactEnumerator(
                [expectedArtifactPath]);

        var reader =
            new Mock<IDocumentReader>();

        reader
            .Setup(x => x.OpenReadAsync(
                document.Id,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new CryptographicException(
                    "Artifact authentication failed."));

        var storageService =
            CreateStorageServiceMock();

        var hashService =
            CreateMatchingHashService();

        var handler =
            CreateHandler(
                repository,
                artifactEnumerator,
                reader,
                storageService,
                hashService);

        // Act
        RecoverDocumentArtifactsResult result =
            await handler.HandleAsync(
                new RecoverDocumentArtifactsCommand());

        // Assert
        Assert.Equal(
            RecoverDocumentArtifactsResultStatus.PreservedForRecovery,
            result.Status);

        var finding =
            Assert.Single(result.Findings);

        Assert.Equal(
            DocumentArtifactReconciliationStatus.UnreadableArtifact,
            finding.Status);

        Assert.Equal(
            DocumentArtifactReconciliationRecoveryAction.PreserveForRecovery,
            finding.RecoveryAction);

        storageService.Verify(
            x => x.DeleteAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenReadableArtifactContentDoesNotMatchDocumentHash_PreservesDocument()
    {
        // Arrange
        Document document =
            CreateDocument();

        string expectedArtifactPath =
            Path.GetFullPath(
                document.StoredFilePath);

        var repository =
            CreateRepository([document]);

        var artifactEnumerator =
            CreateArtifactEnumerator(
                [expectedArtifactPath]);

        var reader =
            new Mock<IDocumentReader>();

        reader
            .Setup(x => x.OpenReadAsync(
                document.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new MemoryStream(
                    "different-content"u8.ToArray()));

        var storageService =
            CreateStorageServiceMock();

        var hashService =
            new Mock<IHashService>();

        hashService
            .Setup(x => x.ComputeSha256Async(
                It.IsAny<Stream>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                "different-sha256-hash");

        var handler =
            CreateHandler(
                repository,
                artifactEnumerator,
                reader,
                storageService,
                hashService);

        // Act
        RecoverDocumentArtifactsResult result =
            await handler.HandleAsync(
                new RecoverDocumentArtifactsCommand());

        // Assert
        Assert.Equal(
            RecoverDocumentArtifactsResultStatus.PreservedForRecovery,
            result.Status);

        var finding =
            Assert.Single(result.Findings);

        Assert.Equal(
            DocumentArtifactReconciliationStatus.ContentMismatch,
            finding.Status);

        Assert.Equal(
            DocumentArtifactReconciliationRecoveryAction.PreserveForRecovery,
            finding.RecoveryAction);

        storageService.Verify(
            x => x.DeleteAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenCancellationIsAlreadyRequested_ThrowsAndDoesNotAccessDependencies()
    {
        // Arrange
        var repository =
            CreateRepository([]);

        var artifactEnumerator =
            new Mock<IDocumentArtifactEnumerator>();

        var reader =
            new Mock<IDocumentReader>();

        var storageService =
            CreateStorageServiceMock();

        var hashService =
            CreateMatchingHashService();

        ReconcileDocumentArtifactsHandler reconciliationHandler =
            CreateReconciliationHandler(
                repository,
                artifactEnumerator,
                reader,
                storageService,
                hashService);

        var handler =
            new RecoverDocumentArtifactsHandler(
                repository.Object,
                reconciliationHandler,
                storageService.Object,
                NullLogger<RecoverDocumentArtifactsHandler>.Instance);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                handler.HandleAsync(
                    new RecoverDocumentArtifactsCommand(),
                    cancellationTokenSource.Token));

        // Assert
        repository.Verify(
            x => x.GetAllAsync(
                It.IsAny<CancellationToken>()),
            Times.Never);

        artifactEnumerator.Verify(
            x => x.EnumerateAsync(
                It.IsAny<CancellationToken>()),
            Times.Never);

        storageService.Verify(
            x => x.DeleteAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static RecoverDocumentArtifactsHandler CreateHandler(
        Mock<IDocumentRepository> repository,
        Mock<IDocumentArtifactEnumerator> artifactEnumerator,
        Mock<IDocumentReader> reader,
        Mock<IStorageService> storageService,
        Mock<IHashService> hashService)
    {
        ReconcileDocumentArtifactsHandler reconciliationHandler =
            CreateReconciliationHandler(
                repository,
                artifactEnumerator,
                reader,
                storageService,
                hashService);

        return new RecoverDocumentArtifactsHandler(
            repository.Object,
            reconciliationHandler,
            storageService.Object,
            NullLogger<RecoverDocumentArtifactsHandler>.Instance);
    }

    private static ReconcileDocumentArtifactsHandler CreateReconciliationHandler(
        Mock<IDocumentRepository> repository,
        Mock<IDocumentArtifactEnumerator> artifactEnumerator,
        Mock<IDocumentReader> reader,
        Mock<IStorageService> storageService,
        Mock<IHashService> hashService)
    {
        return new ReconcileDocumentArtifactsHandler(
            repository.Object,
            artifactEnumerator.Object,
            reader.Object,
            storageService.Object,
            hashService.Object,
            NullLogger<ReconcileDocumentArtifactsHandler>.Instance);
    }

    private static Mock<IDocumentRepository> CreateRepository(
        IReadOnlyList<Document> documents)
    {
        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetAllAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                documents);

        return repository;
    }

    private static Mock<IDocumentArtifactEnumerator> CreateArtifactEnumerator(
        IReadOnlyList<string> artifactPaths)
    {
        var artifactEnumerator =
            new Mock<IDocumentArtifactEnumerator>();

        artifactEnumerator
            .Setup(x => x.EnumerateAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                artifactPaths);

        return artifactEnumerator;
    }

    private static Mock<IStorageService> CreateStorageServiceMock()
    {
        var storageService =
            new Mock<IStorageService>();

        storageService
            .Setup(x => x.IsOwnedArtifactPath(
                It.IsAny<Guid>(),
                It.IsAny<string>()))
            .Returns(true);

        return storageService;
    }

    private static Mock<IHashService> CreateMatchingHashService()
    {
        var hashService =
            new Mock<IHashService>();

        hashService
            .Setup(x => x.ComputeSha256Async(
                It.IsAny<Stream>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                "sha256-test-hash");

        return hashService;
    }

    private static Document CreateDocument()
    {
        Guid documentId =
            Guid.NewGuid();

        return Document.Create(
            documentId,
            "document.txt",
            "Test Document",
            "sha256-test-hash",
            Path.Combine(
                "Documents",
                $"{documentId}.dvault"));
    }
}
