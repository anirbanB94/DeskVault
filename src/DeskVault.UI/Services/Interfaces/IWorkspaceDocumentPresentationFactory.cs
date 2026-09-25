using DeskVault.UI.Services.Workspace;

namespace DeskVault.UI.Services.Interfaces;

public interface IWorkspaceDocumentPresentationFactory
{
    WorkspaceDocumentPresentation Create(
        Guid workspaceId,
        Guid documentId);
}
