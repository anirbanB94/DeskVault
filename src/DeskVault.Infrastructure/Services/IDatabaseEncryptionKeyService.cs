namespace DeskVault.Infrastructure.Services;

public interface IDatabaseEncryptionKeyService
{
    /// <summary>
    /// Retrieves the existing database-encryption key without
    /// provisioning a replacement.
    /// </summary>
    Task<EncryptionKeyMaterial> GetKeyAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the existing database-encryption key or explicitly
    /// provisions one when the key does not exist.
    /// </summary>
    Task<EncryptionKeyMaterial> GetOrCreateKeyAsync(
        CancellationToken cancellationToken = default);
}
