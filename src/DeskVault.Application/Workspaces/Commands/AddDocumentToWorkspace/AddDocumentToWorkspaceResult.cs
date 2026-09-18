using DeskVault.Domain.Workspaces;

namespace DeskVault.Application.Workspaces.Commands.AddDocumentToWorkspace;

public sealed record AddDocumentToWorkspaceResult(
    AddDocumentToWorkspaceResultStatus Status,
    Workspace? Workspace,
    string Description);
