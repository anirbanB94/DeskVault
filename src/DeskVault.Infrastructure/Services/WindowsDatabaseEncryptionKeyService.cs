using System.Security.Cryptography;
using DeskVault.Shared.Extensions;
using DeskVault.Shared.Resources;
using DiagnosticOperationCodes = DeskVault.Shared.Resources.DiagnosticOperationCodes;
using Microsoft.Extensions.Logging;

namespace DeskVault.Infrastructure.Services;

public sealed class WindowsDatabaseEncryptionKeyService :
    IDatabaseEncryptionKeyService,
    IDatabaseEncryptionKeyProvisioningService
{
    private const string KeyFileName = "database.key";
    private const int KeySize = 32;
    private const int SharingViolationRetryDelayMilliseconds = 100;
    private const int SharingViolationWin32Error = 32;

    private readonly DeskVaultDataPaths _dataPaths;
    private readonly ILogger<WindowsDatabaseEncryptionKeyService> _logger;
    private readonly Func<CancellationToken, Task>? _beforeKeyPublicationAsync;
    private readonly IKeyCandidateFileOperations _candidateFileOperations;
    private readonly Func<string, CancellationToken, Task<byte[]>> _readProtectedKeyFileAsync;

    public WindowsDatabaseEncryptionKeyService(
        DeskVaultDataPaths dataPaths,
        ILogger<WindowsDatabaseEncryptionKeyService> logger)
        : this(
            dataPaths,
            logger,
            beforeKeyPublicationAsync: null,
            candidateFileOperations: null,
            readProtectedKeyFileAsync: null)
    {
    }

    /// <summary>
    /// Provides deterministic coordination for publication race tests.
    /// </summary>
    internal WindowsDatabaseEncryptionKeyService(
        DeskVaultDataPaths dataPaths,
        ILogger<WindowsDatabaseEncryptionKeyService> logger,
        Func<CancellationToken, Task>? beforeKeyPublicationAsync,
        IKeyCandidateFileOperations? candidateFileOperations = null,
        Func<string, CancellationToken, Task<byte[]>>? readProtectedKeyFileAsync = null)
    {
        _dataPaths = dataPaths;
        _logger = logger;
        _beforeKeyPublicationAsync = beforeKeyPublicationAsync;
        _candidateFileOperations =
            candidateFileOperations ?? new KeyCandidateFileOperations();
        _readProtectedKeyFileAsync =
            readProtectedKeyFileAsync ?? File.ReadAllBytesAsync;
    }

    public async Task<EncryptionKeyMaterial> GetOrCreateKeyAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

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
                return await ReadExistingKeyForProvisioningAsync(
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
                // Synchronize candidates before canonical publication.
                await _beforeKeyPublicationAsync(cancellationToken);
            }

            try
            {
                // Publish the complete protected key without overwriting
                // an existing canonical database.key file.
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

                return await ReadExistingKeyForProvisioningAsync(
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
            _logger.LogSafeFailure(
                LogLevel.Error,
                DiagnosticOperationCodes.DatabaseEncryptionKeyOperationFailure,
                LogMessages.DatabaseEncryptionKeyOperationFailed,
                ex);

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
            _logger.LogSafeFailure(
                LogLevel.Error,
                DiagnosticOperationCodes.DatabaseEncryptionKeyOperationFailure,
                LogMessages.DatabaseEncryptionKeyOperationFailed,
                ex);

            throw;
        }
    }

    private async Task<EncryptionKeyMaterial> ReadExistingKeyForProvisioningAsync(
        string keyFilePath,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            byte[] protectedKeyFromFile;

            try
            {
                protectedKeyFromFile =
                    await _readProtectedKeyFileAsync(
                        keyFilePath,
                        cancellationToken);
            }
            catch (IOException exception)
                when (IsSharingViolation(exception))
            {
                await Task.Delay(
                    SharingViolationRetryDelayMilliseconds,
                    cancellationToken);

                continue;
            }

            return DecodeExistingKey(
                protectedKeyFromFile);
        }
    }

    private async Task<EncryptionKeyMaterial> ReadExistingKeyAsync(
        string keyFilePath,
        CancellationToken cancellationToken)
    {
        byte[] protectedKeyFromFile =
            await _readProtectedKeyFileAsync(
                keyFilePath,
                cancellationToken);

        return DecodeExistingKey(
            protectedKeyFromFile);
    }

    private EncryptionKeyMaterial DecodeExistingKey(
        byte[] protectedKeyFromFile)
    {
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

    private static bool IsSharingViolation(
        IOException exception)
    {
        return
            (exception.HResult & 0xFFFF) ==
            SharingViolationWin32Error;
    }
}
