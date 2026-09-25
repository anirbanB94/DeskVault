using DeskVault.Domain.Workspaces;
using DeskVault.UI.Services.Workspace;

namespace DeskVault.UI.Services.Interfaces;

public interface IWorkspacePresentationManager
{
    WorkspacePresentation? Get(Guid workspaceId);

    WorkspacePresentation? FindTemporaryByDocument(
        Guid documentId);

    WorkspacePresentation GetOrCreate(
        Guid workspaceId,
        WorkspaceType workspaceType,
        string? workspaceName,
        string? description);

    bool Contains(Guid workspaceId);

    bool Activate(Guid workspaceId);

    bool Close(Guid workspaceId);
}
