namespace DeskVault.UI.Services;

public interface IDocumentWorkspace
{
    event EventHandler DocumentRemoved;

    Task OpenAsync(
        Guid documentId,
        Stream documentStream,
        string fileName,
        CancellationToken cancellationToken = default);
}
