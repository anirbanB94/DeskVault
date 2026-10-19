namespace DeskVault.Infrastructure.Services;

public interface IDatabaseEncryptionKeyProvisioningService
{
    /// <summary>
    /// Retrieves the existing database-encryption key or provisions an
    /// initial key when the caller's lifecycle context explicitly permits it.
    /// </summary>
    /// <remarks>
    /// This method is provisioning-capable; it is not a key-replacement,
    /// rotation, repair, or recovery API. Callers must establish that initial
    /// provisioning is authorized before invoking it.
    /// </remarks>
    Task<EncryptionKeyMaterial> GetOrCreateKeyAsync(
        CancellationToken cancellationToken = default);
}
