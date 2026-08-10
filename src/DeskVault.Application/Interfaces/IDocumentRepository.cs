using DeskVault.Domain.Documents;

namespace DeskVault.Application.Interfaces;

public interface IDocumentRepository
{
    Task<bool> ExistsByHashAsync(
        string sha256Hash,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        Document document,
        CancellationToken cancellationToken = default);
}