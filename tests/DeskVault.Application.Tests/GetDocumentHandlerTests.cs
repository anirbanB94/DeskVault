using DeskVault.Application.Documents.Queries.GetDocument;
using DeskVault.Application.Interfaces;
using DeskVault.Domain.Documents;

namespace DeskVault.Application.Tests;

public sealed class GetDocumentHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenDocumentExists_ReturnsDocumentResult()
    {
        Guid documentId = Guid.NewGuid();

        DateTime importedAt =
            new(2026, 8, 17, 10, 30, 0, DateTimeKind.Utc);

        Document document = Document.Restore(
            documentId,
            "document.txt",
            "Test Document",
            "sha256-test-hash",
            "document.dvault",
            importedAt,
            DocumentStatus.Imported);

        var repository =
            new TestDocumentRepository(document);

        var handler =
            new GetDocumentHandler(repository);

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
    }

    [Fact]
    public async Task HandleAsync_WhenDocumentDoesNotExist_ThrowsFileNotFoundException()
    {
        var repository =
            new TestDocumentRepository(null);

        var handler =
            new GetDocumentHandler(repository);

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => handler.HandleAsync(
                new GetDocumentQuery(Guid.NewGuid())));
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
            return Task.CompletedTask;
        }
    }
}
