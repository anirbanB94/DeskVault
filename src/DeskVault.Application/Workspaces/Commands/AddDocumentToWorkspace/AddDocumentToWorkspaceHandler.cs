using DeskVault.Application.Interfaces;
using DeskVault.Domain.Workspaces;

namespace DeskVault.Application.Workspaces.Commands.AddDocumentToWorkspace;

public sealed class AddDocumentToWorkspaceHandler
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IActiveWorkspaceRegistry _activeWorkspaceRegistry;

    public AddDocumentToWorkspaceHandler(
        IDocumentRepository documentRepository,
        IWorkspaceRepository workspaceRepository,
        IActiveWorkspaceRegistry activeWorkspaceRegistry)
    {
        _documentRepository = documentRepository;
        _workspaceRepository = workspaceRepository;
        _activeWorkspaceRegistry = activeWorkspaceRegistry;
    }

    public async Task<AddDocumentToWorkspaceResult> HandleAsync(
        AddDocumentToWorkspaceCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        Workspace? workspace =
            _activeWorkspaceRegistry.Get(command.WorkspaceId);

        if (workspace is null)
        {
            return new AddDocumentToWorkspaceResult(
                AddDocumentToWorkspaceResultStatus.WorkspaceNotFound,
                null,
                "The workspace is not active.");
        }

        if (workspace.Memberships.Any(
                membership => membership.DocumentId == command.DocumentId))
        {
            return new AddDocumentToWorkspaceResult(
                AddDocumentToWorkspaceResultStatus.AlreadyMember,
                workspace,
                "The document is already a member of the workspace.");
        }

        var document =
            await _documentRepository.GetByIdAsync(
                command.DocumentId,
                cancellationToken);

        if (document is null)
        {
            return new AddDocumentToWorkspaceResult(
                AddDocumentToWorkspaceResultStatus.DocumentNotFound,
                workspace,
                "The document could not be found.");
        }

        if (workspace.TypeOfWorkspace == WorkspaceType.Temporary)
        {
            workspace.AddDocument(command.DocumentId);

            return new AddDocumentToWorkspaceResult(
                AddDocumentToWorkspaceResultStatus.Success,
                workspace,
                "Document added to the workspace.");
        }

        Workspace updatedWorkspace = CreateUpdatedWorkspace(workspace);
        updatedWorkspace.AddDocument(command.DocumentId);

        await _workspaceRepository.UpdateAsync(
            updatedWorkspace,
            cancellationToken);

        _activeWorkspaceRegistry.Replace(updatedWorkspace);

        return new AddDocumentToWorkspaceResult(
            AddDocumentToWorkspaceResultStatus.Success,
            updatedWorkspace,
            "Document added to the workspace.");
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
