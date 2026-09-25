using DeskVault.Domain.Workspaces;

namespace DeskVault.Application.Workspaces.Commands.UpdateWorkspaceDescription;

public sealed record UpdateWorkspaceDescriptionResult(
    UpdateWorkspaceDescriptionResultStatus Status,
    Workspace? Workspace,
    string Description);
