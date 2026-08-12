namespace DeskVault.UI.Services;

public interface IDocumentWorkspace
{
    Task OpenAsync(
        Stream documentStream,
        string fileName,
        CancellationToken cancellationToken = default);
}
