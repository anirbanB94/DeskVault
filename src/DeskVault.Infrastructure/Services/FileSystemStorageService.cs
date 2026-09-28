using DeskVault.Application.Documents.Commands.ImportDocument;
using DeskVault.Application.Interfaces;
using DeskVault.Shared.Resources;
using Microsoft.Extensions.Logging;

namespace DeskVault.Infrastructure.Services;

public sealed class FileSystemStorageService : IStorageService
{
    private readonly DocumentEncryptionService _encryptionService;

    private readonly DeskVaultDataPaths _dataPaths;

    private readonly ILogger<FileSystemStorageService> _logger;

    public FileSystemStorageService(
        DocumentEncryptionService encryptionService,
        DeskVaultDataPaths dataPaths,
        ILogger<FileSystemStorageService> logger)
    {
        _encryptionService = encryptionService;
        _dataPaths = dataPaths;
        _logger = logger;
    }

    public async Task<string> StoreAsync(
        string sourceFilePath,
        Guid documentId,
        CancellationToken cancellationToken = default,
        string? expectedSha256Hash = null)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation(
            LogMessages.DocumentStorageStarted);

        string destinationFilePath =
            Path.Combine(
                _dataPaths.DocumentsDirectory,
                $"{documentId}.dvault");

        bool destinationCreated = false;

        try
        {
            string documentsDirectory =
                _dataPaths.DocumentsDirectory;

            Directory.CreateDirectory(
                documentsDirectory);

            await using var source =
                new FileStream(
                    sourceFilePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 81920,
                    useAsync: true);

            await using var destination =
                new FileStream(
                    destinationFilePath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 81920,
                    useAsync: true);

            destinationCreated = true;

            if (string.IsNullOrWhiteSpace(
                expectedSha256Hash))
            {
                await _encryptionService.EncryptAsync(
                    source,
                    destination,
                    cancellationToken);
            }
            else
            {
                using var hashingSource =
                    new HashingReadStream(source);

                await _encryptionService.EncryptAsync(
                    hashingSource,
                    destination,
                    cancellationToken);

                string actualSha256Hash =
                    hashingSource.GetHashHex();

                if (!string.Equals(
                    actualSha256Hash,
                    expectedSha256Hash,
                    StringComparison.OrdinalIgnoreCase))
                {
                    throw new DocumentImportContentMismatchException();
                }
            }

            _logger.LogInformation(
                LogMessages.DocumentStorageCompleted);

            return destinationFilePath;
        }
        catch (OperationCanceledException)
        {
            if (destinationCreated)
            {
                TryDeletePartialFile(
                    destinationFilePath);
            }

            throw;
        }
        catch (Exception ex)
        {
            if (destinationCreated)
            {
                TryDeletePartialFile(
                    destinationFilePath);
            }

            _logger.LogError(
                ex,
                LogMessages.DocumentStorageFailed);

            throw;
        }
    }

    public Task DeleteAsync(
        string storedFilePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(
            storedFilePath))
        {
            throw new ArgumentException(
                "Stored file path cannot be empty.",
                nameof(storedFilePath));
        }

        try
        {
            File.Delete(
                storedFilePath);

            _logger.LogInformation(
                LogMessages.DocumentStorageDeletionCompleted);

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                LogMessages.DocumentStorageDeletionFailed);

            throw;
        }
    }

    private void TryDeletePartialFile(
        string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch (Exception cleanupException)
        {
            _logger.LogError(
                cleanupException,
                LogMessages.DocumentStorageDeletionFailed);
        }
    }
}
