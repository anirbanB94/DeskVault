namespace DeskVault.Application.Workspaces.Commands.SaveTemporaryWorkspace;

public sealed record SaveTemporaryWorkspaceCommand(
    Guid WorkspaceId,
    string? Name);
