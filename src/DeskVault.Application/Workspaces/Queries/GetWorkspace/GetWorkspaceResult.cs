using DeskVault.Domain.Workspaces;

namespace DeskVault.Application.Workspaces.Queries.GetWorkspace;

public sealed record GetWorkspaceResult(
    GetWorkspaceResultStatus Status,
    Workspace? Workspace,
    string Description);
