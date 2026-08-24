using DeskVault.Application.Documents.Queries.GetDocument;
using DeskVault.Application.Interfaces;
using DeskVault.Domain.Documents;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DeskVault.Application.Tests;

public sealed class GetDocumentHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenDocumentExists_ReturnsDocumentResult()
    {
        Guid documentId = Guid.NewGuid();

        DateTime importedAt =
            new(2026, 8, 17, 10, 30, 0, DateTimeKind.Utc);

        Document document =
            CreateDocument(
                documentId,
                importedAt);

        var repository =
            CreateRepository(
                documentId,
                document);

        var handler =
            CreateHandler(repository);

        GetDocumentResult result =
            await handler.HandleAsync(
                new GetDocumentQuery(documentId));

        Assert.Equal(
            document.Id,
            result.Id);

        Assert.Equal(
            document.FileName,
            result.FileName);

        Assert.Equal(
            document.DisplayName,
            result.DisplayName);

        Assert.Equal(
            document.Sha256Hash,
            result.Sha256Hash);

        Assert.Equal(
            document.ImportedAt,
            result.ImportedAt);

        Assert.Equal(
            document.Status,
            result.Status);

        repository.Verify(
            x => x.GetByIdAsync(
                documentId,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenDocumentDoesNotExist_ThrowsFileNotFoundException()
    {
        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetByIdAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Document?)null);

        var handler =
            CreateHandler(repository);

        await Assert.ThrowsAsync<FileNotFoundException>(
            () =>
                handler.HandleAsync(
                    new GetDocumentQuery(
                        Guid.NewGuid())));

        repository.Verify(
            x => x.GetByIdAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static Document CreateDocument(
        Guid documentId,
        DateTime importedAt)
    {
        return Document.Restore(
            documentId,
            "document.txt",
            "Test Document",
            "sha256-test-hash",
            "document.dvault",
            importedAt,
            DocumentStatus.Imported);
    }

    private static Mock<IDocumentRepository> CreateRepository(
        Guid documentId,
        Document document)
    {
        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetByIdAsync(
                documentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        return repository;
    }

    private static GetDocumentHandler CreateHandler(
        Mock<IDocumentRepository> repository)
    {
        return new GetDocumentHandler(
            repository.Object,
            NullLogger<GetDocumentHandler>.Instance);
    }
}
