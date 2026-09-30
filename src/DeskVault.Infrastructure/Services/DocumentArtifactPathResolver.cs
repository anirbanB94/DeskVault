namespace DeskVault.Infrastructure.Services;

public sealed class DocumentArtifactPathResolver
{
    private const string ArtifactExtension = ".dvault";

    private readonly DeskVaultDataPaths _dataPaths;

    public DocumentArtifactPathResolver(
        DeskVaultDataPaths dataPaths)
    {
        ArgumentNullException.ThrowIfNull(dataPaths);

        _dataPaths = dataPaths;
    }

    public string GetPath(
        Guid documentId)
    {
        if (documentId == Guid.Empty)
        {
            throw new ArgumentException(
                "Document ID cannot be empty.",
                nameof(documentId));
        }

        return Path.Combine(
            _dataPaths.DocumentsDirectory,
            $"{documentId}{ArtifactExtension}");
    }

    public bool IsOwnedArtifactPath(
        Guid documentId,
        string storedFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            storedFilePath);

        string canonicalPath =
            Path.GetFullPath(
                GetPath(documentId));

        string normalizedStoredPath =
            Path.GetFullPath(
                storedFilePath);

        return string.Equals(
            normalizedStoredPath,
            canonicalPath,
            StringComparison.OrdinalIgnoreCase);
    }
}
