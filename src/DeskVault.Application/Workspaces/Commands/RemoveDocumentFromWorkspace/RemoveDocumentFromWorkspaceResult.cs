using DeskVault.Domain.Workspaces;

namespace DeskVault.Application.Workspaces.Commands.RemoveDocumentFromWorkspace;

public sealed record RemoveDocumentFromWorkspaceResult(
    RemoveDocumentFromWorkspaceResultStatus Status,
    Workspace? Workspace,
    string Description);
