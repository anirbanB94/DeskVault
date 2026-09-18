namespace DeskVault.Application.Workspaces.Commands.AddDocumentToWorkspace;

public sealed record AddDocumentToWorkspaceCommand(
    Guid WorkspaceId,
    Guid DocumentId);
