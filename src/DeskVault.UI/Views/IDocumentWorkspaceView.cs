namespace DeskVault.UI.Views;

public interface IDocumentWorkspaceView
{
    void ShowUnsupportedPreview(
        string message);

    Task OpenExternallyAsync(
        Stream documentStream,
        string fileName,
        CancellationToken cancellationToken = default);
}
