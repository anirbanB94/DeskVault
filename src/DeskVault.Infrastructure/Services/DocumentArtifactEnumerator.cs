using DeskVault.Application.Interfaces;

namespace DeskVault.Infrastructure.Services;

public sealed class DocumentArtifactEnumerator
    : IDocumentArtifactEnumerator
{
    private const string ArtifactExtension = ".dvault";

    private readonly DeskVaultDataPaths _dataPaths;

    public DocumentArtifactEnumerator(
        DeskVaultDataPaths dataPaths)
    {
        _dataPaths = dataPaths;
    }

    public Task<IReadOnlyList<string>> EnumerateAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!Directory.Exists(
                _dataPaths.DocumentsDirectory))
        {
            return Task.FromResult<
                IReadOnlyList<string>>(
                Array.Empty<string>());
        }

        string[] artifactPaths =
            Directory.GetFiles(
                _dataPaths.DocumentsDirectory,
                $"*{ArtifactExtension}",
                SearchOption.TopDirectoryOnly);

        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult<
            IReadOnlyList<string>>(
            artifactPaths);
    }
}
