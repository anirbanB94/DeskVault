using DeskVault.Application.Interfaces;
using DeskVault.Domain.Workspaces;

namespace DeskVault.Application.Workspaces.Commands.RenameWorkspace;

public sealed class RenameWorkspaceHandler
{
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IActiveWorkspaceRegistry _activeWorkspaceRegistry;

    public RenameWorkspaceHandler(
        IWorkspaceRepository workspaceRepository,
        IActiveWorkspaceRegistry activeWorkspaceRegistry)
    {
        _workspaceRepository = workspaceRepository;
        _activeWorkspaceRegistry = activeWorkspaceRegistry;
    }

    public async Task<RenameWorkspaceResult> HandleAsync(
        RenameWorkspaceCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        Workspace? workspace =
            _activeWorkspaceRegistry.Get(command.WorkspaceId);

        if (workspace is null)
        {
            return new RenameWorkspaceResult(
                RenameWorkspaceResultStatus.WorkspaceNotFound,
                null,
                "The workspace is not active.");
        }

        if (workspace.TypeOfWorkspace != WorkspaceType.Persistent)
        {
            return new RenameWorkspaceResult(
                RenameWorkspaceResultStatus.WorkspaceNotPersistent,
                workspace,
                "Only persistent workspaces can be renamed.");
        }

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            return new RenameWorkspaceResult(
                RenameWorkspaceResultStatus.NameRequired,
                workspace,
                "A workspace name is required.");
        }

        Workspace updatedWorkspace =
            Workspace.Restore(
                workspace.Id,
                workspace.Name,
                workspace.TypeOfWorkspace,
                workspace.Memberships,
                workspace.LastActiveDocumentId);

        updatedWorkspace.Rename(command.Name);

        await _workspaceRepository.UpdateAsync(
            updatedWorkspace,
            cancellationToken);

        _activeWorkspaceRegistry.Replace(
            updatedWorkspace);

        return new RenameWorkspaceResult(
            RenameWorkspaceResultStatus.Success,
            updatedWorkspace,
            "The workspace was renamed.");
    }
}
