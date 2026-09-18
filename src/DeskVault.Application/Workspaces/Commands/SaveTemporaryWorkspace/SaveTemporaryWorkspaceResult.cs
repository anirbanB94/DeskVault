using DeskVault.Domain.Workspaces;

namespace DeskVault.Application.Workspaces.Commands.SaveTemporaryWorkspace;

public sealed record SaveTemporaryWorkspaceResult(
    SaveTemporaryWorkspaceResultStatus Status,
    Workspace? Workspace,
    string Description);
