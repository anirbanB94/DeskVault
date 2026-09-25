namespace DeskVault.Application.Workspaces.Commands.UpdateWorkspaceDescription;

public sealed record UpdateWorkspaceDescriptionCommand(
    Guid WorkspaceId,
    string? Description);
