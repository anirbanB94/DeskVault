using DeskVault.Application.Documents.Queries.ListDocuments;
using DeskVault.Application.Interfaces;
using DeskVault.Domain.Documents;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DeskVault.Application.Tests;

public sealed class ListDocumentsHandlerTests
{
    [Fact]
    public async Task HandleAsync_ReturnsDocumentsFromRepository()
    {
        Document firstDocument =
            CreateDocument(
                "first.txt",
                "First Document");

        Document secondDocument =
            CreateDocument(
                "second.md",
                "Second Document");

        IReadOnlyList<Document> expected =
        [
            firstDocument,
            secondDocument
        ];

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetAllAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var handler =
            new ListDocumentsHandler(
                repository.Object,
                NullLogger<ListDocumentsHandler>.Instance);

        IReadOnlyList<Document> result =
            await handler.HandleAsync(
                new ListDocumentsQuery());

        Assert.Equal(
            expected,
            result);

        repository.Verify(
            x => x.GetAllAsync(
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static Document CreateDocument(
        string fileName,
        string displayName)
    {
        return Document.Create(
            Guid.NewGuid(),
            fileName,
            displayName,
            $"{fileName}-hash",
            $"{fileName}.dvault");
    }
}
