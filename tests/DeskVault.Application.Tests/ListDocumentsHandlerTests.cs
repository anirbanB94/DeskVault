using DeskVault.Application.Documents.Queries.ListDocuments;
using DeskVault.Application.Interfaces;
using DeskVault.Domain.Documents;

namespace DeskVault.Application.Tests;

public sealed class ListDocumentsHandlerTests
{
    [Fact]
    public async Task HandleAsync_ReturnsDocumentsFromRepository()
    {
        Document firstDocument = CreateDocument(
            "first.txt",
            "First Document");

        Document secondDocument = CreateDocument(
            "second.md",
            "Second Document");

        IReadOnlyList<Document> expected =
        [
            firstDocument,
            secondDocument
        ];

        var repository =
            new TestDocumentRepository(expected);

        var handler =
            new ListDocumentsHandler(repository);

        IReadOnlyList<Document> result =
            await handler.HandleAsync(
                new ListDocumentsQuery());

        Assert.Equal(
            expected,
            result);
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

    private sealed class TestDocumentRepository
        : IDocumentRepository
    {
        private readonly IReadOnlyList<Document> _documents;

        public TestDocumentRepository(
            IReadOnlyList<Document> documents)
        {
            _documents = documents;
        }

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
            return Task.FromResult<Document?>(null);
        }

        public Task<IReadOnlyList<Document>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_documents);
        }

        public Task DeleteAsync(
            Guid documentId,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
