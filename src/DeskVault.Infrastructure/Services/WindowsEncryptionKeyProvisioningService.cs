using Microsoft.Extensions.Logging;

namespace DeskVault.Infrastructure.Services;

/// <summary>
/// Exposes only the document-key provisioning capability.
/// </summary>
internal sealed class WindowsEncryptionKeyProvisioningService
    : IEncryptionKeyProvisioningService
{
    private readonly WindowsEncryptionKeyService _innerService;

    public WindowsEncryptionKeyProvisioningService(
        DeskVaultDataPaths dataPaths,
        ILogger<WindowsEncryptionKeyService> logger)
    {
        ArgumentNullException.ThrowIfNull(dataPaths);
        ArgumentNullException.ThrowIfNull(logger);

        _innerService =
            new WindowsEncryptionKeyService(
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
