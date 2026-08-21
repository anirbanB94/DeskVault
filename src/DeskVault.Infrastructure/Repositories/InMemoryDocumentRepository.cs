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
