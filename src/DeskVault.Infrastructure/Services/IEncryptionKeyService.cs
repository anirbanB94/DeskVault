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
}
