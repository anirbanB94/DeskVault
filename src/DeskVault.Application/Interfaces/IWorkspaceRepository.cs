using DeskVault.Domain.Workspaces;

namespace DeskVault.Application.Interfaces;

public interface IWorkspaceRepository
{
    Task AddAsync(
        Workspace workspace,
        CancellationToken cancellationToken = default);

    Task<Workspace?> GetByIdAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Workspace>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        Workspace workspace,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default);
}
