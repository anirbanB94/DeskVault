using DeskVault.Application.Interfaces;
using DeskVault.Domain.Workspaces;

namespace DeskVault.Application.Workspaces.Commands.SaveTemporaryWorkspace;

public sealed class SaveTemporaryWorkspaceHandler
{
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IActiveWorkspaceRegistry _activeWorkspaceRegistry;

    public SaveTemporaryWorkspaceHandler(
        IWorkspaceRepository workspaceRepository,
        IActiveWorkspaceRegistry activeWorkspaceRegistry)
    {
        _workspaceRepository = workspaceRepository;
        _activeWorkspaceRegistry = activeWorkspaceRegistry;
    }

    public async Task<SaveTemporaryWorkspaceResult> HandleAsync(
        SaveTemporaryWorkspaceCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        Workspace? workspace =
            _activeWorkspaceRegistry.Get(command.WorkspaceId);

        if (workspace is null)
        {
            return new SaveTemporaryWorkspaceResult(
                SaveTemporaryWorkspaceResultStatus.WorkspaceNotFound,
                null,
                "The workspace is not active.");
        }

        if (workspace.TypeOfWorkspace != WorkspaceType.Temporary)
        {
            return new SaveTemporaryWorkspaceResult(
                SaveTemporaryWorkspaceResultStatus.WorkspaceNotTemporary,
                workspace,
                "Only temporary workspaces can be saved as persistent.");
        }

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            return new SaveTemporaryWorkspaceResult(
                SaveTemporaryWorkspaceResultStatus.NameRequired,
                workspace,
                "A workspace name is required.");
        }

        Workspace persistentWorkspace =
            Workspace.Restore(
                workspace.Id,
                command.Name,
                WorkspaceType.Persistent,
                workspace.Memberships,
                workspace.LastActiveDocumentId);

        await _workspaceRepository.AddAsync(
            persistentWorkspace,
            cancellationToken);

        _activeWorkspaceRegistry.Replace(
            persistentWorkspace);

        return new SaveTemporaryWorkspaceResult(
            SaveTemporaryWorkspaceResultStatus.Success,
            persistentWorkspace,
            "The temporary workspace was saved as persistent.");
    }
}
