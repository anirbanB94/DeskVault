namespace DeskVault.UI.Services;

public interface IDocumentViewer
{
    Task OpenAsync(
        Stream documentStream,
        string fileName,
        CancellationToken cancellationToken = default);
}