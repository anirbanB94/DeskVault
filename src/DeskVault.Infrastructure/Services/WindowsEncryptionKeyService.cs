using System.Security.Cryptography;
using DeskVault.Shared.Resources;
using Microsoft.Extensions.Logging;

namespace DeskVault.Infrastructure.Services;

public sealed class WindowsEncryptionKeyService :
    IEncryptionKeyService,
    IEncryptionKeyProvisioningService
{
    private const string KeyFileName = "master.key";
    private const int KeySize = 32;

    private readonly DeskVaultDataPaths _dataPaths;
    private readonly ILogger<WindowsEncryptionKeyService> _logger;

    public WindowsEncryptionKeyService(
        DeskVaultDataPaths dataPaths,
        ILogger<WindowsEncryptionKeyService> logger)
    {
        _dataPaths = dataPaths;
        _logger = logger;
    }

    public async Task<EncryptionKeyMaterial> GetOrCreateKeyAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation(
            LogMessages.EncryptionKeyRetrievalStarted);

        string? temporaryKeyFilePath = null;
        byte[]? candidateKey = null;
        byte[]? keyPayload = null;
        byte[]? protectedKey = null;
        Guid candidateKeyId = Guid.Empty;
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
                EncryptionKeyMaterial existingKey =
                    await ReadKeyFromFileAsync(
                        keyFilePath,
                        cancellationToken);

                _logger.LogInformation(
                    LogMessages.EncryptionKeyLoaded);

                _logger.LogInformation(
                    LogMessages.EncryptionKeyOperationCompleted);

                return existingKey;
            }

            candidateKey =
                RandomNumberGenerator.GetBytes(KeySize);

            candidateKeyId = Guid.NewGuid();

            keyPayload =
                ProtectedEncryptionKeyPayload.Encode(
                    candidateKey,
                    EncryptionKeyPurpose.Document,
                    candidateKeyId,
                    keyVersion: 1);

            protectedKey =
                ProtectedData.Protect(
                    keyPayload,
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
                // Moving within the same directory publishes the complete
                // protected key without overwriting an existing master.key.
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

                EncryptionKeyMaterial authoritativeKey =
                    await ReadKeyFromFileAsync(
                        keyFilePath,
                        cancellationToken);

                _logger.LogInformation(
                    LogMessages.EncryptionKeyLoaded);

                _logger.LogInformation(
                    LogMessages.EncryptionKeyOperationCompleted);

                return authoritativeKey;
            }

            EncryptionKeyMaterial candidateMaterial =
                new(
                    candidateKey,
                    candidateKeyId,
                    keyVersion: 1);

            candidateKeyPublished = true;

            _logger.LogInformation(
                LogMessages.EncryptionKeyCreated);

            _logger.LogInformation(
                LogMessages.EncryptionKeyOperationCompleted);

            return candidateMaterial;
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
        finally
        {
            if (!candidateKeyPublished &&
                candidateKey is not null)
            {
                CryptographicOperations.ZeroMemory(
                    candidateKey);
            }

            if (keyPayload is not null)
            {
                CryptographicOperations.ZeroMemory(
                    keyPayload);
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
                        LogMessages.EncryptionKeyTemporaryFileCleanupFailed);
                }
                catch (UnauthorizedAccessException)
                {
                    _logger.LogWarning(
                        LogMessages.EncryptionKeyTemporaryFileCleanupFailed);
                }
            }
        }
    }

    public async Task<EncryptionKeyMaterial> GetKeyAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation(
            LogMessages.EncryptionKeyRetrievalStarted);

        try
        {
            string keyFilePath =
                Path.Combine(
                    _dataPaths.SecurityDirectory,
                    KeyFileName);

            if (!File.Exists(keyFilePath))
            {
                throw new CryptographicException(
                    "The document encryption key is unavailable.");
            }

            EncryptionKeyMaterial key =
                await ReadKeyFromFileAsync(
                    keyFilePath,
                    cancellationToken);

            _logger.LogInformation(
                LogMessages.EncryptionKeyLoaded);

            _logger.LogInformation(
                LogMessages.EncryptionKeyOperationCompleted);

            return key;
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

    private static async Task<EncryptionKeyMaterial> ReadKeyFromFileAsync(
        string keyFilePath,
        CancellationToken cancellationToken)
    {
        byte[] protectedKeyFromFile =
            await File.ReadAllBytesAsync(
                keyFilePath,
                cancellationToken);

        byte[]? keyPayload = null;

        try
        {
            keyPayload =
                ProtectedData.Unprotect(
                    protectedKeyFromFile,
                    null,
                    DataProtectionScope.CurrentUser);

            return ProtectedEncryptionKeyPayload.Decode(
                keyPayload,
                protectedKeyFromFile,
                EncryptionKeyPurpose.Document);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                protectedKeyFromFile);

            if (keyPayload is not null)
            {
                CryptographicOperations.ZeroMemory(
                    keyPayload);
            }
        }
    }
}
