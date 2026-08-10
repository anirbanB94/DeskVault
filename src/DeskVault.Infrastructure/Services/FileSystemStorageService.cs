using DeskVault.Application.Interfaces;

namespace DeskVault.Infrastructure.Services;

public sealed class FileSystemStorageService : IStorageService
{

    private const string RootFolder = "DeskVault";
    private const string DocumentsFolder = "Documents";

    public async Task<string> StoreAsync(
        string sourceFilePath,
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        string localAppData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);

        string rootDirectory = Path.Combine(localAppData, RootFolder);
        string documentsDirectory = Path.Combine(rootDirectory, DocumentsFolder);

        Directory.CreateDirectory(documentsDirectory);

        string extension = Path.GetExtension(sourceFilePath);
        string destinationFilePath = Path.Combine(
            documentsDirectory,
            $"{documentId}{extension}");

        await using var source = new FileStream(
            sourceFilePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);

        await using var destination = new FileStream(
            destinationFilePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);

        await source.CopyToAsync(destination, cancellationToken);

        return destinationFilePath;
    }
}