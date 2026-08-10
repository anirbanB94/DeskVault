namespace DeskVault.Infrastructure.Services;

public interface IEncryptionKeyService
{
    Task<byte[]> GetOrCreateKeyAsync(
        CancellationToken cancellationToken = default);
}