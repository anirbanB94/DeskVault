using DeskVault.Application.Interfaces;
using System.Runtime.CompilerServices;

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

    public async IAsyncEnumerable<string> EnumerateIncrementallyAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        cancellationToken.ThrowIfCancellationRequested();

        if (!DocumentsDirectoryExists())
        {
            yield break;
        }

        foreach (string artifactPath in Directory.EnumerateFiles(
                     _dataPaths.DocumentsDirectory,
                     $"*{ArtifactExtension}",
                     SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();

            yield return artifactPath;
        }
    }

    public bool ExistsForDocument(Guid documentId)
    {
        if (documentId == Guid.Empty)
        {
            throw new ArgumentException(
                "Document ID cannot be empty.",
                nameof(documentId));
        }

        if (!DocumentsDirectoryExists())
        {
            return false;
        }

        string artifactPath =
            Path.Combine(
                _dataPaths.DocumentsDirectory,
                $"{documentId}{ArtifactExtension}");

        FileAttributes attributes;

        try
        {
            attributes = File.GetAttributes(artifactPath);
        }
        catch (Exception exception) when (
            exception is FileNotFoundException or
            DirectoryNotFoundException)
        {
            return false;
        }

        if ((attributes & FileAttributes.Directory) != 0)
        {
            throw new IOException(
                "The canonical document artifact path is not a file.");
        }

        return true;
    }

    private bool DocumentsDirectoryExists()
    {
        FileAttributes attributes;

        try
        {
            attributes =
                File.GetAttributes(
                    _dataPaths.DocumentsDirectory);
        }
        catch (Exception exception)
            when (exception is FileNotFoundException
                or DirectoryNotFoundException)
        {
            return false;
        }

        if ((attributes & FileAttributes.Directory) == 0)
        {
            throw new IOException(
                "The configured document storage path is not a directory.");
        }

        return true;
    }
}
