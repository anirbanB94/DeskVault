using DeskVault.Application.Interfaces;
using DeskVault.Domain.Workspaces;

namespace DeskVault.Application.Workspaces;

public sealed class ActiveWorkspaceRegistry
    : IActiveWorkspaceRegistry
{
    private readonly Dictionary<Guid, Workspace> _workspaces = [];
    private readonly object _syncRoot = new();

    public void Add(Workspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        lock (_syncRoot)
        {
            _workspaces.Add(
                workspace.Id,
                workspace);
        }
    }

    public Workspace? Get(Guid workspaceId)
    {
        lock (_syncRoot)
        {
            return _workspaces.TryGetValue(
                workspaceId,
                out Workspace? workspace)
                    ? workspace
                    : null;
        }
    }

    public IReadOnlyList<Workspace> GetAll()
    {
        lock (_syncRoot)
        {
            return _workspaces.Values.ToList();
        }
    }

    public void Replace(Workspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        lock (_syncRoot)
        {
            if (!_workspaces.ContainsKey(workspace.Id))
            {
                throw new InvalidOperationException(
                    $"Workspace '{workspace.Id}' is not active.");
            }

            _workspaces[workspace.Id] = workspace;
        }
    }

    public bool Remove(Guid workspaceId)
    {
        lock (_syncRoot)
        {
            return _workspaces.Remove(workspaceId);
        }
    }
}
