using DeskVault.Domain.Workspaces;

namespace DeskVault.Application.Workspaces.Commands.ActivateWorkspace;

public sealed record ActivateWorkspaceResult(
    ActivateWorkspaceResultStatus Status,
    Workspace? Workspace,
    string Description);
