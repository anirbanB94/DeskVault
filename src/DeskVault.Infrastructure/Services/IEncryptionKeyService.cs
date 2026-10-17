namespace DeskVault.Infrastructure.Services;

public interface IEncryptionKeyService
{
    /// <summary>
    /// Retrieves and validates the existing document-encryption key.
    /// </summary>
    /// <remarks>
    /// This is strict retrieval only. It never provisions, repairs, replaces,
    /// or rotates a key. A missing or unusable canonical key artifact causes
    /// retrieval to fail without creating or modifying canonical key state.
    /// Cancellation is propagated as cancellation.
    /// </remarks>
    Task<EncryptionKeyMaterial> GetKeyAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the existing document-encryption key or provisions an initial
    /// key when the caller's lifecycle context explicitly permits provisioning.
    /// </summary>
    /// <remarks>
    /// This method is provisioning-capable; it is not a key-replacement,
    /// rotation, or recovery API. Callers must establish that initial
    /// provisioning is authorized before invoking it. Absence of a key in an
    /// existing vault does not itself authorize replacement. If the canonical
    /// key artifact exists, it must be retrieved and validated; an unusable
    /// existing artifact must cause failure rather than being overwritten
    /// or repaired.
    /// </remarks>
    Task<EncryptionKeyMaterial> GetOrCreateKeyAsync(
        CancellationToken cancellationToken = default);
}
