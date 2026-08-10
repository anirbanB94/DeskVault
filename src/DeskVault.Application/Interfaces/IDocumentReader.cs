namespace DeskVault.Application.Interfaces;

public interface IDocumentReader
{
    Task<Stream> OpenReadAsync(
        string storedFilePath,
        CancellationToken cancellationToken = default);
}