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
                expectedArtifactPath,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new MemoryStream(
                    "decrypted-content"u8.ToArray()));

        var handler =
            CreateHandler(
                repository,
                artifactEnumerator,
                reader);

        ReconcileDocumentArtifactsResult result =
            await handler.HandleAsync(
                new ReconcileDocumentArtifactsQuery());

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

        reader.Verify(
            x => x.OpenReadAsync(
                expectedArtifactPath,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenArtifactIsMissing_ReturnsMissingArtifact()
    {
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

        var handler =
            CreateHandler(
                repository,
                artifactEnumerator,
                reader);

        ReconcileDocumentArtifactsResult result =
            await handler.HandleAsync(
                new ReconcileDocumentArtifactsQuery());

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

        reader.Verify(
            x => x.OpenReadAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenArtifactHasNoDocument_ReturnsOrphanedArtifact()
    {
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

        var handler =
            CreateHandler(
                repository,
                artifactEnumerator,
                reader);

        ReconcileDocumentArtifactsResult result =
            await handler.HandleAsync(
                new ReconcileDocumentArtifactsQuery());

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
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenArtifactCannotBeRead_ReturnsUnreadableArtifact()
    {
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
                expectedArtifactPath,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new CryptographicException(
                    "Artifact authentication failed."));

        var handler =
            CreateHandler(
                repository,
                artifactEnumerator,
                reader);

        ReconcileDocumentArtifactsResult result =
            await handler.HandleAsync(
                new ReconcileDocumentArtifactsQuery());

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
    }

    [Fact]
    public async Task HandleAsync_WhenStoredPathDoesNotMatchDocumentIdentity_ReturnsPathMismatch()
    {
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

        var handler =
            CreateHandler(
                repository,
                artifactEnumerator,
                reader);

        ReconcileDocumentArtifactsResult result =
            await handler.HandleAsync(
                new ReconcileDocumentArtifactsQuery());

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

        reader.Verify(
            x => x.OpenReadAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenCancellationIsAlreadyRequested_ThrowsAndDoesNotAccessDependencies()
    {
        var repository =
            new Mock<IDocumentRepository>();

        var artifactEnumerator =
            new Mock<IDocumentArtifactEnumerator>();

        var reader =
            new Mock<IDocumentReader>();

        var handler =
            CreateHandler(
                repository,
                artifactEnumerator,
                reader);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                handler.HandleAsync(
                    new ReconcileDocumentArtifactsQuery(),
                    cancellationTokenSource.Token));

        repository.Verify(
            x => x.GetAllAsync(
                It.IsAny<CancellationToken>()),
            Times.Never);

        artifactEnumerator.Verify(
            x => x.EnumerateAsync(
                It.IsAny<CancellationToken>()),
            Times.Never);

        reader.Verify(
            x => x.OpenReadAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenReconcilingDocuments_DoesNotMutateRepository()
    {
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

        var handler =
            CreateHandler(
                repository,
                artifactEnumerator,
                reader);

        await handler.HandleAsync(
            new ReconcileDocumentArtifactsQuery());

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
    }

    private static ReconcileDocumentArtifactsHandler CreateHandler(
        Mock<IDocumentRepository> repository,
        Mock<IDocumentArtifactEnumerator> artifactEnumerator,
        Mock<IDocumentReader> reader)
    {
        return new ReconcileDocumentArtifactsHandler(
            repository.Object,
            artifactEnumerator.Object,
            reader.Object,
            NullLogger<ReconcileDocumentArtifactsHandler>.Instance);
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
