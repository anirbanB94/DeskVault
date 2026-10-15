namespace DeskVault.Application.Interfaces;

public interface IDocumentReader
{
    Task<Stream> OpenReadAsync(
        Guid documentId,
        string expectedSha256Hash,
        CancellationToken cancellationToken = default,
        long? maximumPlaintextBytes = null);
}
