using DeskVault.Application.Interfaces;
using DeskVault.Domain.Documents;

namespace DeskVault.Infrastructure.Repositories;

public sealed class InMemoryDocumentRepository : IDocumentRepository
{
    private readonly List<Document> _documents = [];

    public Task<bool> ExistsByHashAsync(
        string sha256Hash,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        bool exists = _documents.Any(
            x => x.Sha256Hash == sha256Hash);

        return Task.FromResult(exists);
    }

    public Task AddAsync(
        Document document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        cancellationToken.ThrowIfCancellationRequested();

        _documents.Add(document);

        return Task.CompletedTask;
    }

    public Task<Document?> GetByIdAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Document? document =
            _documents.FirstOrDefault(
                x => x.Id == documentId);

        return Task.FromResult(document);
    }

    public Task<IReadOnlyList<Document>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<Document> documents =
            [.. _documents];

        return Task.FromResult(documents);
    }

    public Task<IReadOnlyList<Document>> GetPageAsync(
        DocumentPageCursor? cursor,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (pageSize <= 0 ||
            pageSize > IDocumentRepository.MaximumDocumentPageSize)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize));
        }

        if (cursor is not null &&
            (cursor.ImportedAt == default ||
             cursor.DocumentId == Guid.Empty))
        {
            throw new ArgumentException(
                "Document page cursor is invalid.",
                nameof(cursor));
        }

        cancellationToken.ThrowIfCancellationRequested();

        IEnumerable<Document> query = _documents;

        if (cursor is not null)
        {
            string cursorDocumentId = cursor.DocumentId.ToString();

            query = query.Where(document =>
                document.ImportedAt < cursor.ImportedAt ||
                (document.ImportedAt == cursor.ImportedAt &&
                 string.CompareOrdinal(
                     document.Id.ToString(),
                     cursorDocumentId) > 0));
        }

        IReadOnlyList<Document> documents = query
            .OrderByDescending(document => document.ImportedAt)
            .ThenBy(
                document => document.Id.ToString(),
                StringComparer.Ordinal)
            .Take(pageSize)
            .ToArray();

        return Task.FromResult(documents);
    }

    public Task UpdateAsync(
        Document document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        cancellationToken.ThrowIfCancellationRequested();

        int index =
            _documents.FindIndex(
                x => x.Id == document.Id);

        if (index < 0)
        {
            throw new InvalidOperationException(
                $"Document '{document.Id}' was not found.");
        }

        _documents[index] = document;

        return Task.CompletedTask;
    }

    public Task DeleteAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Document? document =
            _documents.FirstOrDefault(
                x => x.Id == documentId);

        if (document is not null)
        {
            _documents.Remove(document);
        }

        return Task.CompletedTask;
    }
}
