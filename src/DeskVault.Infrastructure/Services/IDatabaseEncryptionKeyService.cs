namespace DeskVault.Infrastructure.Services;

public interface IDatabaseEncryptionKeyService
{
    Task<byte[]> GetOrCreateKeyAsync(
        CancellationToken cancellationToken = default);
}
