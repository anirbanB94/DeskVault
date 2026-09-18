using DeskVault.Domain.Workspaces;

namespace DeskVault.Application.Workspaces.Commands.RenameWorkspace;

public sealed record RenameWorkspaceResult(
    RenameWorkspaceResultStatus Status,
    Workspace? Workspace,
    string Description);
