using DeskVault.Domain.Workspaces;

namespace DeskVault.Application.Workspaces.Queries.GetWorkspaces;

public sealed record GetWorkspacesResult(
    GetWorkspacesResultStatus Status,
    IReadOnlyList<Workspace> Workspaces,
    string Description);
