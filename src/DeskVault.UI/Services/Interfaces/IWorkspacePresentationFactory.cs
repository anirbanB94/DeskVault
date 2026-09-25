using DeskVault.Domain.Workspaces;
using DeskVault.UI.Services.Workspace;

namespace DeskVault.UI.Services.Interfaces;

public interface IWorkspacePresentationFactory
{
    WorkspacePresentation Create(
        Guid workspaceId,
        WorkspaceType workspaceType,
        string? workspaceName,
        string? description);
}
