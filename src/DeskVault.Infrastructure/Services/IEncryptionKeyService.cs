namespace DeskVault.Infrastructure.Services;

public interface IEncryptionKeyService
{
    /// <summary>
    /// Retrieves the existing document-encryption key without
    /// provisioning a replacement.
    /// </summary>
    Task<byte[]> GetKeyAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the existing document-encryption key or explicitly
    /// provisions one when the key does not exist.
    /// </summary>
    Task<byte[]> GetOrCreateKeyAsync(
        CancellationToken cancellationToken = default);
}
