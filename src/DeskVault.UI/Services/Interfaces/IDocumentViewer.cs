namespace DeskVault.UI.Services.Interfaces;

public interface IDocumentViewer
{
    Task OpenAsync(
        Stream documentStream,
        string fileName,
        CancellationToken cancellationToken = default);
}
