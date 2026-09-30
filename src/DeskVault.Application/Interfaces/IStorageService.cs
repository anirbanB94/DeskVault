namespace DeskVault.Application.Interfaces;

public interface IStorageService
{
    Task<string> StoreAsync(
        string sourceFilePath,
        Guid documentId,
        CancellationToken cancellationToken = default,
        string? expectedSha256Hash = null);

    Task DeleteAsync(
        Guid documentId,
        CancellationToken cancellationToken = default);

    bool IsOwnedArtifactPath(
        Guid documentId,
        string storedFilePath);
}
