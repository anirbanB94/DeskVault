namespace DeskVault.Infrastructure.Services;

public interface IEncryptionKeyService
{
    /// <summary>
    /// Retrieves the existing document-encryption key without
    /// provisioning a replacement.
    /// </summary>
    Task<EncryptionKeyMaterial> GetKeyAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the existing document-encryption key or explicitly
    /// provisions one when the key does not exist.
    /// </summary>
    Task<EncryptionKeyMaterial> GetOrCreateKeyAsync(
        CancellationToken cancellationToken = default);
}
