using System.Security.Cryptography;
using DeskVault.Application.Documents.Queries.ReconcileDocumentArtifacts;
using DeskVault.Application.Interfaces;
using DeskVault.Domain.Documents;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DeskVault.Application.Tests;

public sealed class ReconcileDocumentArtifactsHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenArtifactMatchesDocument_ReturnsMatched()
    {
        // Arrange
        Document document =
            CreateDocument();

        string expectedArtifactPath =
            Path.GetFullPath(
                document.StoredFilePath);

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetAllAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([document]);

        var artifactEnumerator =
            new Mock<IDocumentArtifactEnumerator>();

        artifactEnumerator
            .Setup(x => x.EnumerateAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                document.StoredFilePath
            ]);

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

        var handler =
            CreateHandler(
                repository,
                artifactEnumerator,
                reader,
                storageService);

        // Act
        ReconcileDocumentArtifactsResult result =
            await handler.HandleAsync(
                new ReconcileDocumentArtifactsQuery());

        // Assert
        var finding =
            Assert.Single(result.Findings);

        Assert.Equal(
            DocumentArtifactReconciliationStatus.Matched,
            finding.Status);

        Assert.Equal(
            document.Id,
            finding.DocumentId);

        Assert.Equal(
            expectedArtifactPath,
            finding.ArtifactPath);

        storageService.Verify(
            x => x.IsOwnedArtifactPath(
                document.Id,
                expectedArtifactPath),
            Times.Once);

        reader.Verify(
            x => x.OpenReadAsync(
                document.Id,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenArtifactIsMissing_ReturnsMissingArtifact()
    {
        // Arrange
        Document document =
            CreateDocument();

        string expectedArtifactPath =
            Path.GetFullPath(
                document.StoredFilePath);

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetAllAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([document]);

        var artifactEnumerator =
            new Mock<IDocumentArtifactEnumerator>();

        artifactEnumerator
            .Setup(x => x.EnumerateAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Array.Empty<string>());

        var reader =
            new Mock<IDocumentReader>();

        var storageService =
            CreateStorageServiceMock();

        var handler =
            CreateHandler(
                repository,
                artifactEnumerator,
                reader,
                storageService);

        // Act
        ReconcileDocumentArtifactsResult result =
            await handler.HandleAsync(
                new ReconcileDocumentArtifactsQuery());

        // Assert
        var finding =
            Assert.Single(result.Findings);

        Assert.Equal(
            DocumentArtifactReconciliationStatus.MissingArtifact,
            finding.Status);

        Assert.Equal(
            document.Id,
            finding.DocumentId);

        Assert.Equal(
            expectedArtifactPath,
            finding.ArtifactPath);

        storageService.Verify(
            x => x.IsOwnedArtifactPath(
                document.Id,
                expectedArtifactPath),
            Times.Once);

        reader.Verify(
            x => x.OpenReadAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenArtifactHasNoDocument_ReturnsOrphanedArtifact()
    {
        // Arrange
        Guid orphanId =
            Guid.NewGuid();

        string orphanPath =
            Path.Combine(
                "Documents",
                $"{orphanId}.dvault");

        string expectedArtifactPath =
            Path.GetFullPath(
                orphanPath);

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetAllAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var artifactEnumerator =
            new Mock<IDocumentArtifactEnumerator>();

        artifactEnumerator
            .Setup(x => x.EnumerateAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                orphanPath
            ]);

        var reader =
            new Mock<IDocumentReader>();

        var storageService =
            CreateStorageServiceMock();

        var handler =
            CreateHandler(
                repository,
                artifactEnumerator,
                reader,
                storageService);

        // Act
        ReconcileDocumentArtifactsResult result =
            await handler.HandleAsync(
                new ReconcileDocumentArtifactsQuery());

        // Assert
        var finding =
            Assert.Single(result.Findings);

        Assert.Equal(
            DocumentArtifactReconciliationStatus.OrphanedArtifact,
            finding.Status);

        Assert.Equal(
            orphanId,
            finding.DocumentId);

        Assert.Equal(
            expectedArtifactPath,
            finding.ArtifactPath);

        reader.Verify(
            x => x.OpenReadAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        storageService.Verify(
            x => x.IsOwnedArtifactPath(
                It.IsAny<Guid>(),
                It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenArtifactCannotBeRead_ReturnsUnreadableArtifact()
    {
        // Arrange
        Document document =
            CreateDocument();

        string expectedArtifactPath =
            Path.GetFullPath(
                document.StoredFilePath);

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetAllAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([document]);

        var artifactEnumerator =
            new Mock<IDocumentArtifactEnumerator>();

        artifactEnumerator
            .Setup(x => x.EnumerateAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                document.StoredFilePath
            ]);

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

        var handler =
            CreateHandler(
                repository,
                artifactEnumerator,
                reader,
                storageService);

        // Act
        ReconcileDocumentArtifactsResult result =
            await handler.HandleAsync(
                new ReconcileDocumentArtifactsQuery());

        // Assert
        var finding =
            Assert.Single(result.Findings);

        Assert.Equal(
            DocumentArtifactReconciliationStatus.UnreadableArtifact,
            finding.Status);

        Assert.Equal(
            document.Id,
            finding.DocumentId);

        Assert.Equal(
            expectedArtifactPath,
            finding.ArtifactPath);

        storageService.Verify(
            x => x.IsOwnedArtifactPath(
                document.Id,
                expectedArtifactPath),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenStoredPathDoesNotMatchDocumentIdentity_ReturnsPathMismatch()
    {
        // Arrange
        Guid documentId =
            Guid.NewGuid();

        Guid differentArtifactId =
            Guid.NewGuid();

        Document document =
            Document.Create(
                documentId,
                "document.txt",
                "Test Document",
                "sha256-test-hash",
                Path.Combine(
                    "Documents",
                    $"{differentArtifactId}.dvault"));

        string storedArtifactPath =
            Path.GetFullPath(
                document.StoredFilePath);

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetAllAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([document]);

        var artifactEnumerator =
            new Mock<IDocumentArtifactEnumerator>();

        artifactEnumerator
            .Setup(x => x.EnumerateAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                document.StoredFilePath
            ]);

        var reader =
            new Mock<IDocumentReader>();

        var storageService =
            CreateStorageServiceMock();

        storageService
            .Setup(x => x.IsOwnedArtifactPath(
                document.Id,
                storedArtifactPath))
            .Returns(false);

        var handler =
            CreateHandler(
                repository,
                artifactEnumerator,
                reader,
                storageService);

        // Act
        ReconcileDocumentArtifactsResult result =
            await handler.HandleAsync(
                new ReconcileDocumentArtifactsQuery());

        // Assert
        var finding =
            Assert.Single(result.Findings);

        Assert.Equal(
            DocumentArtifactReconciliationStatus.PathMismatch,
            finding.Status);

        Assert.Equal(
            document.Id,
            finding.DocumentId);

        Assert.Equal(
            storedArtifactPath,
            finding.ArtifactPath);

        storageService.Verify(
            x => x.IsOwnedArtifactPath(
                document.Id,
                storedArtifactPath),
            Times.Once);

        reader.Verify(
            x => x.OpenReadAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenStoredPathUsesDifferentDirectoryWithSameDocumentIdentity_ReturnsPathMismatch()
    {
        // Arrange
        Guid documentId =
            Guid.NewGuid();

        string storedArtifactPath =
            Path.GetFullPath(
                Path.Combine(
                    "Outside",
                    $"{documentId}.dvault"));

        Document document =
            Document.Create(
                documentId,
                "document.txt",
                "Test Document",
                "sha256-test-hash",
                storedArtifactPath);

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetAllAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([document]);

        var artifactEnumerator =
            new Mock<IDocumentArtifactEnumerator>();

        artifactEnumerator
            .Setup(x => x.EnumerateAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                storedArtifactPath
            ]);

        var reader =
            new Mock<IDocumentReader>();

        var storageService =
            CreateStorageServiceMock();

        storageService
            .Setup(x => x.IsOwnedArtifactPath(
                document.Id,
                storedArtifactPath))
            .Returns(false);

        var handler =
            CreateHandler(
                repository,
                artifactEnumerator,
                reader,
                storageService);

        // Act
        ReconcileDocumentArtifactsResult result =
            await handler.HandleAsync(
                new ReconcileDocumentArtifactsQuery());

        // Assert
        var finding =
            Assert.Single(result.Findings);

        Assert.Equal(
            DocumentArtifactReconciliationStatus.PathMismatch,
            finding.Status);

        Assert.Equal(
            document.Id,
            finding.DocumentId);

        Assert.Equal(
            storedArtifactPath,
            finding.ArtifactPath);

        storageService.Verify(
            x => x.IsOwnedArtifactPath(
                document.Id,
                storedArtifactPath),
            Times.Once);

        reader.Verify(
            x => x.OpenReadAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenCancellationIsAlreadyRequested_ThrowsAndDoesNotAccessDependencies()
    {
        // Arrange
        var repository =
            new Mock<IDocumentRepository>();

        var artifactEnumerator =
            new Mock<IDocumentArtifactEnumerator>();

        var reader =
            new Mock<IDocumentReader>();

        var storageService =
            CreateStorageServiceMock();

        var handler =
            CreateHandler(
                repository,
                artifactEnumerator,
                reader,
                storageService);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                handler.HandleAsync(
                    new ReconcileDocumentArtifactsQuery(),
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
            x => x.IsOwnedArtifactPath(
                It.IsAny<Guid>(),
                It.IsAny<string>()),
            Times.Never);

        reader.Verify(
            x => x.OpenReadAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenReconcilingDocuments_DoesNotMutateRepository()
    {
        // Arrange
        Document document =
            CreateDocument();

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetAllAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([document]);

        var artifactEnumerator =
            new Mock<IDocumentArtifactEnumerator>();

        artifactEnumerator
            .Setup(x => x.EnumerateAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Array.Empty<string>());

        var reader =
            new Mock<IDocumentReader>();

        var storageService =
            CreateStorageServiceMock();

        var handler =
            CreateHandler(
                repository,
                artifactEnumerator,
                reader,
                storageService);

        // Act
        await handler.HandleAsync(
            new ReconcileDocumentArtifactsQuery());

        // Assert
        repository.Verify(
            x => x.AddAsync(
                It.IsAny<Document>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        repository.Verify(
            x => x.UpdateAsync(
                It.IsAny<Document>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        repository.Verify(
            x => x.DeleteAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        storageService.Verify(
            x => x.IsOwnedArtifactPath(
                document.Id,
                It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenReadableArtifactContentMatchesDocumentHash_ReturnsMatched()
    {
        // Arrange
        Document document =
            CreateDocument();

        string expectedArtifactPath =
            Path.GetFullPath(
                document.StoredFilePath);

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetAllAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([document]);

        var artifactEnumerator =
            new Mock<IDocumentArtifactEnumerator>();

        artifactEnumerator
            .Setup(x => x.EnumerateAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                document.StoredFilePath
            ]);

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
            new Mock<IHashService>();

        hashService
            .Setup(x => x.ComputeSha256Async(
                It.IsAny<Stream>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                document.Sha256Hash);

        var handler =
            CreateHandler(
                repository,
                artifactEnumerator,
                reader,
                storageService,
                hashService);

        // Act
        ReconcileDocumentArtifactsResult result =
            await handler.HandleAsync(
                new ReconcileDocumentArtifactsQuery());

        // Assert
        var finding =
            Assert.Single(result.Findings);

        Assert.Equal(
            DocumentArtifactReconciliationStatus.Matched,
            finding.Status);

        hashService.Verify(
            x => x.ComputeSha256Async(
                It.IsAny<Stream>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        Assert.Equal(
            expectedArtifactPath,
            finding.ArtifactPath);
    }

    [Fact]
    public async Task HandleAsync_WhenReadableArtifactContentDoesNotMatchDocumentHash_ReturnsContentMismatch()
    {
        // Arrange
        Document document =
            CreateDocument();

        string expectedArtifactPath =
            Path.GetFullPath(
                document.StoredFilePath);

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetAllAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([document]);

        var artifactEnumerator =
            new Mock<IDocumentArtifactEnumerator>();

        artifactEnumerator
            .Setup(x => x.EnumerateAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                document.StoredFilePath
            ]);

        var reader =
            new Mock<IDocumentReader>();

        reader
            .Setup(x => x.OpenReadAsync(
                document.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new MemoryStream(
                    "wrong-content"u8.ToArray()));

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
        ReconcileDocumentArtifactsResult result =
            await handler.HandleAsync(
                new ReconcileDocumentArtifactsQuery());

        // Assert
        var finding =
            Assert.Single(result.Findings);

        Assert.Equal(
            DocumentArtifactReconciliationStatus.ContentMismatch,
            finding.Status);

        Assert.Equal(
            document.Id,
            finding.DocumentId);

        Assert.Equal(
            expectedArtifactPath,
            finding.ArtifactPath);

        Assert.Equal(
            DocumentArtifactReconciliationRecoveryAction.PreserveForRecovery,
            finding.RecoveryAction);

        repository.Verify(
            x => x.DeleteAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static ReconcileDocumentArtifactsHandler CreateHandler(
        Mock<IDocumentRepository> repository,
        Mock<IDocumentArtifactEnumerator> artifactEnumerator,
        Mock<IDocumentReader> reader,
        Mock<IStorageService> storageService,
        Mock<IHashService>? hashService = null)
    {
        if (hashService is null)
        {
            hashService =
                new Mock<IHashService>();

            hashService
                .Setup(x => x.ComputeSha256Async(
                    It.IsAny<Stream>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(
                    "sha256-test-hash");
        }

        return new ReconcileDocumentArtifactsHandler(
            repository.Object,
            artifactEnumerator.Object,
            reader.Object,
            storageService.Object,
            hashService.Object,
            NullLogger<ReconcileDocumentArtifactsHandler>.Instance);
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
