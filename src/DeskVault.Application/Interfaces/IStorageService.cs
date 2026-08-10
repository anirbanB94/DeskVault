namespace DeskVault.Application.Interfaces;

public interface IStorageService
{
    Task<string> StoreAsync(
        string sourceFilePath,
        Guid documentId,
        CancellationToken cancellationToken = default);
}