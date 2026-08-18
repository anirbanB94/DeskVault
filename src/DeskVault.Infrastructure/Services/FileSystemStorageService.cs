using DeskVault.Application.Interfaces;

namespace DeskVault.Infrastructure.Services;

public sealed class FileSystemStorageService : IStorageService
{
    private readonly DocumentEncryptionService _encryptionService;

    private readonly DeskVaultDataPaths _dataPaths;

    public FileSystemStorageService(
        DocumentEncryptionService encryptionService,
        DeskVaultDataPaths dataPaths)
    {
        _encryptionService = encryptionService;
        _dataPaths = dataPaths;
    }

    public async Task<string> StoreAsync(
        string sourceFilePath,
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        string documentsDirectory =
            _dataPaths.DocumentsDirectory;

        Directory.CreateDirectory(
            documentsDirectory);

        string destinationFilePath =
            Path.Combine(
                documentsDirectory,
                $"{documentId}.dvault");

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

        await _encryptionService.EncryptAsync(
            source,
            destination,
            cancellationToken);

        return destinationFilePath;
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

        File.Delete(
            storedFilePath);

        return Task.CompletedTask;
    }
}
