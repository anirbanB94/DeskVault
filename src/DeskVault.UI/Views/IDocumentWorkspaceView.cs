namespace DeskVault.UI.Views;

public interface IDocumentWorkspaceView
{
    event EventHandler OpenExternallyRequested;

    event EventHandler DocumentInformationRequested;

    event EventHandler RemoveDocumentRequested;

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
        string sha256Hash);

    bool ConfirmRemoval(
        string fileName);

    void CloseWorkspace();

    void ShowError(
        string message,
        string title);
}
