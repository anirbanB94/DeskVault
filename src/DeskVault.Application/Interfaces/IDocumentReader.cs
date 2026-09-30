namespace DeskVault.Application.Interfaces;

public interface IDocumentReader
{
    Task<Stream> OpenReadAsync(
        Guid documentId,
        CancellationToken cancellationToken = default,
        long? maximumPlaintextBytes = null);
}
