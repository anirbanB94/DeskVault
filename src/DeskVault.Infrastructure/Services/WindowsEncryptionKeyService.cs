using System.Security.Cryptography;
using DeskVault.Shared.Resources;
using Microsoft.Extensions.Logging;

namespace DeskVault.Infrastructure.Services;

public sealed class WindowsEncryptionKeyService :
    IEncryptionKeyService
{
    private const string KeyFileName = "master.key";

    private readonly DeskVaultDataPaths _dataPaths;
    private readonly ILogger<WindowsEncryptionKeyService> _logger;

    public WindowsEncryptionKeyService(
        DeskVaultDataPaths dataPaths,
        ILogger<WindowsEncryptionKeyService> logger)
    {
        _dataPaths = dataPaths;
        _logger = logger;
    }

    public async Task<byte[]> GetOrCreateKeyAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation(
            LogMessages.EncryptionKeyRetrievalStarted);

        try
        {
            string securityDirectory =
                _dataPaths.SecurityDirectory;

            Directory.CreateDirectory(
                securityDirectory);

            string keyFilePath =
                Path.Combine(
                    securityDirectory,
                    KeyFileName);

            if (File.Exists(keyFilePath))
            {
                byte[] protectedKeyFromFile =
                    await File.ReadAllBytesAsync(
                        keyFilePath,
                        cancellationToken);

                byte[] key =
                    ProtectedData.Unprotect(
                        protectedKeyFromFile,
                        null,
                        DataProtectionScope.CurrentUser);

                _logger.LogInformation(
                    LogMessages.EncryptionKeyLoaded);

                _logger.LogInformation(
                    LogMessages.EncryptionKeyOperationCompleted);

                return key;
            }

            byte[] newKey =
                RandomNumberGenerator.GetBytes(32);

            byte[] protectedKey =
                ProtectedData.Protect(
                    newKey,
                    null,
                    DataProtectionScope.CurrentUser);

            await File.WriteAllBytesAsync(
                keyFilePath,
                protectedKey,
                cancellationToken);

            _logger.LogInformation(
                LogMessages.EncryptionKeyCreated);

            _logger.LogInformation(
                LogMessages.EncryptionKeyOperationCompleted);

            return newKey;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                LogMessages.EncryptionKeyOperationFailed);

            throw;
        }
    }
}
