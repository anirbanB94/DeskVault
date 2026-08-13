namespace DeskVault.UI.Views;

public interface IDocumentWorkspaceView
{
    event EventHandler OpenExternallyRequested;

    Task ShowDocumentAsync(
        Stream documentStream,
        string fileName,
        CancellationToken cancellationToken = default);

    void ShowUnsupportedPreview(
        string message);

    void ShowError(
        string message,
        string title);
}
