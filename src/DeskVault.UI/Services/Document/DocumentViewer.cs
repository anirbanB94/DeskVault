using System.Collections.Concurrent;
using DeskVault.UI.Services.Interfaces;

namespace DeskVault.UI.Services.Document;

public sealed class DocumentViewer :
    IDocumentViewer,
    IDisposable
{
    private readonly IExternalDocumentLauncher _externalDocumentLauncher;

    private readonly ConcurrentDictionary<
        Guid,
        TemporaryDocumentArtifact> _activeArtifacts =
        new();

    private bool _disposed;

    public DocumentViewer(
        IExternalDocumentLauncher externalDocumentLauncher)
    {
        ArgumentNullException.ThrowIfNull(
            externalDocumentLauncher);

        _externalDocumentLauncher =
            externalDocumentLauncher;
    }

    public async Task OpenAsync(
        Stream documentStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);

        ArgumentNullException.ThrowIfNull(
            documentStream);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            fileName);

        string extension =
            Path.GetExtension(fileName);

        TemporaryDocumentArtifact artifact =
            CreateTemporaryArtifact(
                extension);

        _activeArtifacts.TryAdd(
            artifact.Id,
            artifact);

        try
        {
            await using (
                FileStream temporaryFile =
                    new(
                        artifact.FilePath,
                        FileMode.Open,
                        FileAccess.Write,
                        FileShare.Read,
                        bufferSize: 81920,
                        options: FileOptions.Asynchronous))
            {
                await documentStream.CopyToAsync(
                    temporaryFile,
                    cancellationToken);

                await temporaryFile.FlushAsync(
                    cancellationToken);
            }

            IExternalDocumentLaunch launch =
                _externalDocumentLauncher.Launch(
                    artifact.FilePath);

            artifact.HasObservableExternalLifecycle =
                launch.Completion is not null;

            _ = ObserveExternalLaunchAsync(
                artifact,
                launch);
        }
        catch
        {
            RemoveArtifact(
                artifact);

            throw;
        }
    }

    private static TemporaryDocumentArtifact CreateTemporaryArtifact(
        string extension)
    {
        string filePath =
            Path.Combine(
                Path.GetTempPath(),
                $"{Guid.NewGuid():N}{extension}");

        using (
            FileStream temporaryFile =
                new(
                    filePath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.Read,
                    bufferSize: 81920,
                    options: FileOptions.Asynchronous))
        {
        }

        return new TemporaryDocumentArtifact(
            Guid.NewGuid(),
            filePath);
    }

    private async Task ObserveExternalLaunchAsync(
        TemporaryDocumentArtifact artifact,
        IExternalDocumentLaunch launch)
    {
        try
        {
            if (launch.Completion is not null)
            {
                await launch.Completion.ConfigureAwait(
                    false);

                RemoveArtifact(
                    artifact);
            }
        }
        catch
        {
            RemoveArtifact(
                artifact);
        }
        finally
        {
            launch.Dispose();
        }
    }

    private void RemoveArtifact(
        TemporaryDocumentArtifact artifact)
    {
        if (_activeArtifacts.TryRemove(
            artifact.Id,
            out TemporaryDocumentArtifact? removedArtifact))
        {
            removedArtifact.Dispose();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (
            KeyValuePair<Guid, TemporaryDocumentArtifact> entry
            in _activeArtifacts)
        {
            if (entry.Value.HasObservableExternalLifecycle)
            {
                continue;
            }

            if (_activeArtifacts.TryRemove(
                entry.Key,
                out TemporaryDocumentArtifact? artifact))
            {
                artifact.Dispose();
            }
        }
    }

    private sealed class TemporaryDocumentArtifact :
        IDisposable
    {
        public TemporaryDocumentArtifact(
            Guid id,
            string filePath)
        {
            Id = id;
            FilePath = filePath;
        }

        public Guid Id { get; }

        public string FilePath { get; }

        public bool HasObservableExternalLifecycle { get; set; }

        public void Dispose()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    File.Delete(FilePath);
                }
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
    }
}
