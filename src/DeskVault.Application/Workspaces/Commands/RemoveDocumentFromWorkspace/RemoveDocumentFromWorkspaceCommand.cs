namespace DeskVault.Application.Workspaces.Commands.RemoveDocumentFromWorkspace;

public sealed record RemoveDocumentFromWorkspaceCommand(
    Guid WorkspaceId,
    Guid DocumentId);
