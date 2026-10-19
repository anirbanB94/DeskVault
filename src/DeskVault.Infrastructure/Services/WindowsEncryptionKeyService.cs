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
    private readonly Func<CancellationToken, Task>? _beforeKeyPublicationAsync;
    private readonly IKeyCandidateFileOperations _candidateFileOperations;

    public WindowsEncryptionKeyService(
        DeskVaultDataPaths dataPaths,
        ILogger<WindowsEncryptionKeyService> logger)
        : this(
            dataPaths,
            logger,
            beforeKeyPublicationAsync: null,
            candidateFileOperations: null)
    {
    }

    /// <summary>
    /// Provides a publication seam for deterministic concurrency tests.
    /// </summary>
    internal WindowsEncryptionKeyService(
        DeskVaultDataPaths dataPaths,
        ILogger<WindowsEncryptionKeyService> logger,
        Func<CancellationToken, Task>? beforeKeyPublicationAsync,
        IKeyCandidateFileOperations? candidateFileOperations = null)
    {
        _dataPaths = dataPaths;
        _logger = logger;
        _beforeKeyPublicationAsync = beforeKeyPublicationAsync;
        _candidateFileOperations =
            candidateFileOperations ?? new KeyCandidateFileOperations();
    }

    public async Task<EncryptionKeyMaterial> GetOrCreateKeyAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation(
            LogMessages.EncryptionKeyRetrievalStarted);

        string? temporaryKeyFilePath = null;
        bool ownsTemporaryKeyFile = false;
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
                _candidateFileOperations.GetCandidateFilePath(
                    securityDirectory,
                    KeyFileName);

            await using (var temporaryKeyFile =
                _candidateFileOperations.CreateCandidateFile(
                    temporaryKeyFilePath))
            {
                // Exclusive creation succeeded; this operation owns the candidate.
                ownsTemporaryKeyFile = true;

                await temporaryKeyFile.WriteAsync(
                    protectedKey,
                    cancellationToken);

                await temporaryKeyFile.FlushAsync(
                    cancellationToken);

                _candidateFileOperations.FlushCandidateFileToDisk(
                    temporaryKeyFile);
            }

            if (_beforeKeyPublicationAsync is not null)
            {
                await _beforeKeyPublicationAsync(
                    cancellationToken);
            }

            try
            {
                // Moving within the same directory publishes the complete
                // protected key without overwriting an existing master.key.
                _candidateFileOperations.PublishCandidateFile(
                    temporaryKeyFilePath,
                    keyFilePath);

                // Publication succeeded; the temporary path is no longer owned.
                ownsTemporaryKeyFile = false;
                temporaryKeyFilePath = null;
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

            if (ownsTemporaryKeyFile &&
                temporaryKeyFilePath is not null)
            {
                try
                {
                    _candidateFileOperations.DeleteCandidateFile(
                        temporaryKeyFilePath);
                }
                catch (Exception)
                {
                    // Cleanup must not replace the provisioning outcome.
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
