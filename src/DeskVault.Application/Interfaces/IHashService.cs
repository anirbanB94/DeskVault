namespace DeskVault.Application.Interfaces;

public interface IHashService
{
    Task<string> ComputeSha256Async(
        string filePath,
        CancellationToken cancellationToken = default);

    Task<string> ComputeSha256Async(
        Stream content,
        CancellationToken cancellationToken = default);
}
