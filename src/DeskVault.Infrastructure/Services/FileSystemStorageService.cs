using DeskVault.Application.Interfaces;

namespace DeskVault.Infrastructure.Services;

public sealed class FileSystemStorageService : IStorageService
{
    private const string RootFolder = "DeskVault";
    private const string DocumentsFolder = "Documents";

    private readonly DocumentEncryptionService _encryptionService;

    public FileSystemStorageService(
        DocumentEncryptionService encryptionService)
    {
        _encryptionService = encryptionService;
    }

    public async Task<string> StoreAsync(
        string sourceFilePath,
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        string localAppData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);

        string rootDirectory = Path.Combine(
            localAppData,
            RootFolder);

        string documentsDirectory = Path.Combine(
            rootDirectory,
            DocumentsFolder);

        Directory.CreateDirectory(documentsDirectory);

        string destinationFilePath = Path.Combine(
            documentsDirectory,
            $"{documentId}.dvault");

        await using var source = new FileStream(
            sourceFilePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);

        await using var destination = new FileStream(
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
}