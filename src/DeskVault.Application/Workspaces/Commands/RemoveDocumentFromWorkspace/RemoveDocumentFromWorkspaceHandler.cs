using DeskVault.Application.Interfaces;
using DeskVault.Domain.Workspaces;

namespace DeskVault.Application.Workspaces.Commands.RemoveDocumentFromWorkspace;

public sealed class RemoveDocumentFromWorkspaceHandler
{
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IActiveWorkspaceRegistry _activeWorkspaceRegistry;

    public RemoveDocumentFromWorkspaceHandler(
        IWorkspaceRepository workspaceRepository,
        IActiveWorkspaceRegistry activeWorkspaceRegistry)
    {
        _workspaceRepository = workspaceRepository;
        _activeWorkspaceRegistry = activeWorkspaceRegistry;
    }

    public async Task<RemoveDocumentFromWorkspaceResult> HandleAsync(
        RemoveDocumentFromWorkspaceCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        Workspace? workspace =
            _activeWorkspaceRegistry.Get(command.WorkspaceId);

        if (workspace is null)
        {
            return new RemoveDocumentFromWorkspaceResult(
                RemoveDocumentFromWorkspaceResultStatus.WorkspaceNotFound,
                null,
                "The workspace is not active.");
        }

        if (!workspace.Memberships.Any(
                membership => membership.DocumentId == command.DocumentId))
        {
            return new RemoveDocumentFromWorkspaceResult(
                RemoveDocumentFromWorkspaceResultStatus.DocumentNotMember,
                workspace,
                "The document is not a member of the workspace.");
        }

        if (workspace.TypeOfWorkspace == WorkspaceType.Temporary)
        {
            workspace.RemoveDocument(command.DocumentId);

            return new RemoveDocumentFromWorkspaceResult(
                RemoveDocumentFromWorkspaceResultStatus.Success,
                workspace,
                "Document removed from the workspace.");
        }

        Workspace updatedWorkspace = CreateUpdatedWorkspace(workspace);
        updatedWorkspace.RemoveDocument(command.DocumentId);

        await _workspaceRepository.UpdateAsync(
            updatedWorkspace,
            cancellationToken);

        _activeWorkspaceRegistry.Replace(updatedWorkspace);

        return new RemoveDocumentFromWorkspaceResult(
            RemoveDocumentFromWorkspaceResultStatus.Success,
            updatedWorkspace,
            "Document removed from the workspace.");
    }

    private static Workspace CreateUpdatedWorkspace(
        Workspace workspace)
    {
        return Workspace.Restore(
            workspace.Id,
            workspace.Name,
            workspace.TypeOfWorkspace,
            workspace.Memberships,
            workspace.LastActiveDocumentId);
    }
}
