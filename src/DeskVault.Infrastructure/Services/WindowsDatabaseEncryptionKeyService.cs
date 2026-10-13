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

        string? temporaryKeyFilePath = null;
        byte[]? candidateKey = null;
        byte[]? protectedKey = null;
        bool candidateKeyPublished = false;

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

            candidateKey =
                RandomNumberGenerator.GetBytes(
                    KeySize);

            protectedKey =
                ProtectedData.Protect(
                    candidateKey,
                    null,
                    DataProtectionScope.CurrentUser);

            temporaryKeyFilePath =
                Path.Combine(
                    securityDirectory,
                    $"{KeyFileName}.{Guid.NewGuid():N}.tmp");

            await using (var temporaryKeyFile =
                new FileStream(
                    temporaryKeyFilePath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 4096,
                    useAsync: true))
            {
                await temporaryKeyFile.WriteAsync(
                    protectedKey,
                    cancellationToken);

                await temporaryKeyFile.FlushAsync(
                    cancellationToken);

                temporaryKeyFile.Flush(
                    flushToDisk: true);
            }

            try
            {
                // Publish the complete protected key without overwriting
                // an existing canonical database.key file.
                File.Move(
                    temporaryKeyFilePath,
                    keyFilePath);
            }
            catch (IOException) when (File.Exists(keyFilePath))
            {
                // Another initializer published the authoritative key first.
                // Discard this candidate and retrieve the persisted key.
                CryptographicOperations.ZeroMemory(
                    candidateKey);

                candidateKey = null;

                return await ReadExistingKeyAsync(
                    keyFilePath,
                    cancellationToken);
            }

            candidateKeyPublished = true;

            _logger.LogInformation(
                LogMessages.DatabaseEncryptionKeyCreated);

            return candidateKey;
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
        finally
        {
            if (!candidateKeyPublished &&
                candidateKey is not null)
            {
                CryptographicOperations.ZeroMemory(
                    candidateKey);
            }

            if (protectedKey is not null)
            {
                CryptographicOperations.ZeroMemory(
                    protectedKey);
            }

            if (temporaryKeyFilePath is not null)
            {
                try
                {
                    File.Delete(
                        temporaryKeyFilePath);
                }
                catch (IOException)
                {
                    _logger.LogWarning(
                        LogMessages.DatabaseEncryptionKeyTemporaryFileCleanupFailed);
                }
                catch (UnauthorizedAccessException)
                {
                    _logger.LogWarning(
                        LogMessages.DatabaseEncryptionKeyTemporaryFileCleanupFailed);
                }
            }
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

        byte[]? key = null;

        try
        {
            key =
                ProtectedData.Unprotect(
                    protectedKeyFromFile,
                    null,
                    DataProtectionScope.CurrentUser);

            ValidateKey(key);

            _logger.LogInformation(
                LogMessages.DatabaseEncryptionKeyLoaded);

            byte[] retrievedKey = key;
            key = null;

            return retrievedKey;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                protectedKeyFromFile);

            if (key is not null)
            {
                CryptographicOperations.ZeroMemory(
                    key);
            }
        }
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
