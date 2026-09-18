namespace DeskVault.Application.Workspaces.Commands.RenameWorkspace;

public sealed record RenameWorkspaceCommand(
    Guid WorkspaceId,
    string? Name);
