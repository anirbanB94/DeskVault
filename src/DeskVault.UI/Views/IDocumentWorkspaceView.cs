namespace DeskVault.UI.Views;

public interface IDocumentWorkspaceView
{
    event EventHandler OpenExternallyRequested;

    event EventHandler DocumentInformationRequested;

    event EventHandler RemoveDocumentRequested;

    event EventHandler CloseWorkspaceRequested;

    Task ShowDocumentAsync(
        Stream documentStream,
        string fileName,
        CancellationToken cancellationToken = default);

    void ShowUnsupportedPreview(
        string message);

    void ShowDocumentInformation(
        string displayName,
        string fileName,
        string fileType,
        DateTime importedAt,
        string status,
        string sha256Hash,
        long expectedSessionRevision = 0);

    bool ConfirmRemoval(
        string fileName,
        long expectedSessionRevision = 0);

    void ClearPresentation();

    void PostToUi(Action action);

    void ActivateWorkspace();

    void CloseWorkspace();

    void ShowError(
        string message,
        string title,
        long expectedSessionRevision = 0);
}
