namespace DeskVault.Application.Workspaces.Commands.CreateWorkspace;

public sealed record CreateWorkspaceCommand(
    string? Name,
    bool IsPersistent,
    IReadOnlyCollection<Guid>? DocumentIds);
