using DeskVault.Domain.Workspaces;

namespace DeskVault.Application.Workspaces.Commands.DeleteWorkspace;

public sealed record DeleteWorkspaceResult(
    DeleteWorkspaceResultStatus Status,
    Workspace? Workspace,
    string Description);
