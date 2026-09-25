using DeskVault.UI.Services.Workspace;

namespace DeskVault.UI.Views;

public interface IWorkspacePresentationView
{
    event EventHandler<DocumentActivatedEventArgs> DocumentActivated;

    event EventHandler<NavigationModeChangedEventArgs> NavigationModeChanged;

    event EventHandler AddDocumentsRequested;

    event EventHandler RemoveDocumentsRequested;

    event EventHandler RemoveDocumentRequested;

    event EventHandler UpdateWorkspaceDetailsRequested;

    event EventHandler SaveAsWorkspaceRequested;

    event EventHandler DocumentInformationRequested;

    event EventHandler DeleteWorkspaceRequested;

    event EventHandler CloseWorkspaceRequested;

    event EventHandler CloseDocumentRequested;

    void SetWorkspaceIdentity(
        string workspaceName,
        string? description);

    void SetTemporaryDocumentPresentation(
        string documentFileName);

    void SetWorkspaceDocuments(
        IReadOnlyDictionary<Guid, string> documents);

    void AddDocumentPresentation(
        WorkspaceDocumentPresentation presentation);

    bool RemoveDocumentPresentation(
        Guid documentId);

    bool ActivateDocumentPresentation(
        Guid documentId);

    void SetNavigationMode(
        WorkspacePresentationNavigationMode navigationMode);

    void ActivateWorkspace();

    void CloseWorkspace();
}
