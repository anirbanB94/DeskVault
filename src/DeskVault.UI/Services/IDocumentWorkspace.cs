namespace DeskVault.UI.Services;

public interface IDocumentWorkspace
{
    Task OpenAsync(
    Guid documentId,
    Stream documentStream,
    string fileName,
    CancellationToken cancellationToken = default);
}
