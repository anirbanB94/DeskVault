using System.Diagnostics;

namespace DeskVault.UI.Services;

public sealed class DocumentViewer : IDocumentViewer
{
    public async Task OpenAsync(
        Stream documentStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        string extension = Path.GetExtension(fileName);

        string temporaryFilePath = Path.Combine(
            Path.GetTempPath(),
            $"{Guid.NewGuid():N}{extension}");

        try
        {
            await using (documentStream)
            await using (var temporaryFile = new FileStream(
                temporaryFilePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true))
            {
                await documentStream.CopyToAsync(
                    temporaryFile,
                    cancellationToken);
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = temporaryFilePath,
                UseShellExecute = true
            });
        }
        catch
        {
            TryDeleteTemporaryFile(temporaryFilePath);
            throw;
        }
    }

    private static void TryDeleteTemporaryFile(
        string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch
        {
            // Best-effort cleanup.
        }
    }
}