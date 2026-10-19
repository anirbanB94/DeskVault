using System.Security.Cryptography;
using DeskVault.Shared.Resources;
using Microsoft.Extensions.Logging;

namespace DeskVault.Infrastructure.Services;

public sealed class WindowsDatabaseEncryptionKeyService :
    IDatabaseEncryptionKeyService,
    IDatabaseEncryptionKeyProvisioningService
{
    private const string KeyFileName = "database.key";
    private const int KeySize = 32;

    private readonly DeskVaultDataPaths _dataPaths;
    private readonly ILogger<WindowsDatabaseEncryptionKeyService> _logger;
    private readonly Func<CancellationToken, Task>? _beforeKeyPublicationAsync;

    public WindowsDatabaseEncryptionKeyService(
        DeskVaultDataPaths dataPaths,
        ILogger<WindowsDatabaseEncryptionKeyService> logger)
        : this(
            dataPaths,
            logger,
            beforeKeyPublicationAsync: null)
    {
    }

    /// <summary>
    /// Provides deterministic coordination for publication race tests.
    /// </summary>
    internal WindowsDatabaseEncryptionKeyService(
        DeskVaultDataPaths dataPaths,
        ILogger<WindowsDatabaseEncryptionKeyService> logger,
        Func<CancellationToken, Task>? beforeKeyPublicationAsync)
    {
        _dataPaths = dataPaths;
        _logger = logger;
        _beforeKeyPublicationAsync = beforeKeyPublicationAsync;
    }

    public async Task<EncryptionKeyMaterial> GetOrCreateKeyAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

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
                return await ReadExistingKeyAsync(
                    keyFilePath,
                    cancellationToken);
            }

            candidateKey =
                RandomNumberGenerator.GetBytes(
                    KeySize);

            candidateKeyId = Guid.NewGuid();

            keyPayload =
                ProtectedEncryptionKeyPayload.Encode(
                    candidateKey,
                    EncryptionKeyPurpose.Database,
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

            if (_beforeKeyPublicationAsync is not null)
            {
                // Synchronize candidates before canonical publication.
                await _beforeKeyPublicationAsync(cancellationToken);
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

            EncryptionKeyMaterial candidateMaterial =
                new(
                    candidateKey,
                    candidateKeyId,
                    keyVersion: 1);

            candidateKeyPublished = true;

            _logger.LogInformation(
                LogMessages.DatabaseEncryptionKeyCreated);

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

    public async Task<EncryptionKeyMaterial> GetKeyAsync(
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

    private async Task<EncryptionKeyMaterial> ReadExistingKeyAsync(
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

            EncryptionKeyMaterial keyMaterial =
                ProtectedEncryptionKeyPayload.Decode(
                    keyPayload,
                    protectedKeyFromFile,
                    EncryptionKeyPurpose.Database);

            _logger.LogInformation(
                LogMessages.DatabaseEncryptionKeyLoaded);

            return keyMaterial;
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
