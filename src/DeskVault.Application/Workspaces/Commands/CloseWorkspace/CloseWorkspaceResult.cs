using DeskVault.Domain.Workspaces;

namespace DeskVault.Application.Workspaces.Commands.CloseWorkspace;

public sealed record CloseWorkspaceResult(
    CloseWorkspaceResultStatus Status,
    Workspace? Workspace,
    string Description);
