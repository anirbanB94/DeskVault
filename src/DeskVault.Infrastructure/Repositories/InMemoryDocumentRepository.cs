using DeskVault.Application.Interfaces;
using DeskVault.Domain.Documents;

namespace DeskVault.Infrastructure.Repositories;

public sealed class InMemoryDocumentRepository : IDocumentRepository
{
    private readonly List<Document> _documents = [];

    public Task<bool> ExistsByHashAsync(string sha256Hash, CancellationToken cancellationToken = default)
    {
        bool exists = _documents.Any(x => string.Equals(
            x.Sha256Hash,
            sha256Hash,
            StringComparison.Ordinal));

        return Task.FromResult(exists);
    }

    public Task AddAsync(Document document, CancellationToken cancellationToken = default)
    {
        _documents.Add(document);

        return Task.CompletedTask;
    }
}