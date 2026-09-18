using DeskVault.Domain.Workspaces;

namespace DeskVault.Application.Interfaces;

public interface IActiveWorkspaceRegistry
{
    void Add(Workspace workspace);

    Workspace? Get(Guid workspaceId);

    IReadOnlyList<Workspace> GetAll();

    void Replace(Workspace workspace);

    bool Remove(Guid workspaceId);
}
