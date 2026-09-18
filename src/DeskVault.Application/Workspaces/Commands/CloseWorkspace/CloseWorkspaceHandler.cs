using DeskVault.Application.Interfaces;
using DeskVault.Domain.Workspaces;

namespace DeskVault.Application.Workspaces.Commands.CloseWorkspace;

public sealed class CloseWorkspaceHandler
{
    private readonly IActiveWorkspaceRegistry _activeWorkspaceRegistry;

    public CloseWorkspaceHandler(
        IActiveWorkspaceRegistry activeWorkspaceRegistry)
    {
        _activeWorkspaceRegistry = activeWorkspaceRegistry;
    }

    public Task<CloseWorkspaceResult> HandleAsync(
        CloseWorkspaceCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        Workspace? workspace =
            _activeWorkspaceRegistry.Get(command.WorkspaceId);

        if (workspace is null)
        {
            return Task.FromResult(
                new CloseWorkspaceResult(
                    CloseWorkspaceResultStatus.WorkspaceNotFound,
                    null,
                    "The workspace is not active."));
        }

        _activeWorkspaceRegistry.Remove(workspace.Id);

        return Task.FromResult(
            new CloseWorkspaceResult(
                CloseWorkspaceResultStatus.Success,
                workspace,
                "The workspace was closed."));
    }
}
