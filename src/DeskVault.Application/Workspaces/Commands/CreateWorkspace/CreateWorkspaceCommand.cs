namespace DeskVault.Application.Workspaces.Commands.CreateWorkspace;

public sealed record CreateWorkspaceCommand(
    string? Name,
    string? Description,
    bool IsPersistent,
    IReadOnlyCollection<Guid>? DocumentIds);
