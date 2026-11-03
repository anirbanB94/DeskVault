using Microsoft.Extensions.Logging;

namespace DeskVault.Infrastructure.Services;

/// <summary>
/// Exposes only the database-key provisioning capability.
/// </summary>
internal sealed class WindowsDatabaseEncryptionKeyProvisioningService
    : IDatabaseEncryptionKeyProvisioningService
{
    private readonly WindowsDatabaseEncryptionKeyService _innerService;

    public WindowsDatabaseEncryptionKeyProvisioningService(
        DeskVaultDataPaths dataPaths,
        ILogger<WindowsDatabaseEncryptionKeyService> logger)
    {
        ArgumentNullException.ThrowIfNull(dataPaths);
        ArgumentNullException.ThrowIfNull(logger);

        _innerService =
            new WindowsDatabaseEncryptionKeyService(
                dataPaths,
                logger);
    }

    /// <inheritdoc />
    public Task<EncryptionKeyMaterial> GetOrCreateKeyAsync(
        CancellationToken cancellationToken = default)
    {
        return _innerService.GetOrCreateKeyAsync(
            cancellationToken);
    }
}
