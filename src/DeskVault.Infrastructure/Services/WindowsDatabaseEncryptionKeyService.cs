using System.Security.Cryptography;
using DeskVault.Shared.Resources;
using Microsoft.Extensions.Logging;

namespace DeskVault.Infrastructure.Services;

public sealed class WindowsDatabaseEncryptionKeyService :
    IDatabaseEncryptionKeyService
{
    private const string KeyFileName = "database.key";
    private const int KeySize = 32;

    private readonly DeskVaultDataPaths _dataPaths;
    private readonly ILogger<WindowsDatabaseEncryptionKeyService> _logger;

    public WindowsDatabaseEncryptionKeyService(
        DeskVaultDataPaths dataPaths,
        ILogger<WindowsDatabaseEncryptionKeyService> logger)
    {
        _dataPaths = dataPaths;
        _logger = logger;
    }

    public async Task<byte[]> GetOrCreateKeyAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

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
                return await ReadExistingKeyAsync(
                    keyFilePath,
                    cancellationToken);
            }

            byte[] newKey =
                RandomNumberGenerator.GetBytes(
                    KeySize);

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
                LogMessages.DatabaseEncryptionKeyCreated);

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
                LogMessages.DatabaseEncryptionKeyOperationFailed);

            throw;
        }
    }

    public async Task<byte[]> GetKeyAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            string keyFilePath =
                Path.Combine(
                    _dataPaths.SecurityDirectory,
                    KeyFileName);

            if (!File.Exists(keyFilePath))
            {
                throw new CryptographicException(
                    "The database encryption key is unavailable.");
            }

            return await ReadExistingKeyAsync(
                keyFilePath,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                LogMessages.DatabaseEncryptionKeyOperationFailed);

            throw;
        }
    }

    private async Task<byte[]> ReadExistingKeyAsync(
        string keyFilePath,
        CancellationToken cancellationToken)
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

        ValidateKey(key);

        _logger.LogInformation(
            LogMessages.DatabaseEncryptionKeyLoaded);

        return key;
    }

    private static void ValidateKey(
        byte[] key)
    {
        if (key.Length != KeySize)
        {
            throw new CryptographicException(
                "The database encryption key has an invalid length.");
        }
    }
}
