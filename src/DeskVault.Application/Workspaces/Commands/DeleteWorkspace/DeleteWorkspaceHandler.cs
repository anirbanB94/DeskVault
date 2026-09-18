using DeskVault.Application.Interfaces;
using DeskVault.Domain.Workspaces;

namespace DeskVault.Application.Workspaces.Commands.DeleteWorkspace;

public sealed class DeleteWorkspaceHandler
{
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IActiveWorkspaceRegistry _activeWorkspaceRegistry;

    public DeleteWorkspaceHandler(
        IWorkspaceRepository workspaceRepository,
        IActiveWorkspaceRegistry activeWorkspaceRegistry)
    {
        _workspaceRepository = workspaceRepository;
        _activeWorkspaceRegistry = activeWorkspaceRegistry;
    }

    public async Task<DeleteWorkspaceResult> HandleAsync(
        DeleteWorkspaceCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        Workspace? workspace =
            _activeWorkspaceRegistry.Get(command.WorkspaceId);

        if (workspace is null)
        {
            return new DeleteWorkspaceResult(
                DeleteWorkspaceResultStatus.WorkspaceNotFound,
                null,
                "The workspace is not active.");
        }

        if (workspace.TypeOfWorkspace ==
            WorkspaceType.Persistent)
        {
            await _workspaceRepository.DeleteAsync(
                workspace.Id,
                cancellationToken);
        }

        _activeWorkspaceRegistry.Remove(workspace.Id);

        return new DeleteWorkspaceResult(
            DeleteWorkspaceResultStatus.Success,
            workspace,
            "The workspace was deleted.");
    }
}
