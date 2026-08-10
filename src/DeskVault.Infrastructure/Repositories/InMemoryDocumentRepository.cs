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
        bool exists = _documents.Any(
            x => x.Sha256Hash == sha256Hash);

        return Task.FromResult(exists);
    }

    public Task AddAsync(
        Document document,
        CancellationToken cancellationToken = default)
    {
        _documents.Add(document);

        return Task.CompletedTask;
    }

    public Task<Document?> GetByIdAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        Document? document = _documents.FirstOrDefault(
            x => x.Id == documentId);

        return Task.FromResult(document);
    }

    public Task<IReadOnlyList<Document>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Document> documents = [.. _documents];

        return Task.FromResult(documents);
    }
}