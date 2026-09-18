using DeskVault.Application.Interfaces;
using DeskVault.Domain.Workspaces;

namespace DeskVault.Application.Workspaces.Commands.ActivateWorkspace;

public sealed class ActivateWorkspaceHandler
{
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IActiveWorkspaceRegistry _activeWorkspaceRegistry;

    public ActivateWorkspaceHandler(
        IWorkspaceRepository workspaceRepository,
        IActiveWorkspaceRegistry activeWorkspaceRegistry)
    {
        _workspaceRepository = workspaceRepository;
        _activeWorkspaceRegistry = activeWorkspaceRegistry;
    }

    public async Task<ActivateWorkspaceResult> HandleAsync(
        ActivateWorkspaceCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        Workspace? activeWorkspace =
            _activeWorkspaceRegistry.Get(command.WorkspaceId);

        if (activeWorkspace is not null)
        {
            return new ActivateWorkspaceResult(
                ActivateWorkspaceResultStatus.AlreadyActive,
                activeWorkspace,
                "The workspace is already active.");
        }

        Workspace? workspace =
            await _workspaceRepository.GetByIdAsync(
                command.WorkspaceId,
                cancellationToken);

        if (workspace is null)
        {
            return new ActivateWorkspaceResult(
                ActivateWorkspaceResultStatus.WorkspaceNotFound,
                null,
                "The workspace could not be found.");
        }

        _activeWorkspaceRegistry.Add(workspace);

        return new ActivateWorkspaceResult(
            ActivateWorkspaceResultStatus.Activated,
            workspace,
            "The workspace was activated.");
    }
}
