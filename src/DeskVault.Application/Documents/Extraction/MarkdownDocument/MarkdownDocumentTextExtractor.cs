using System.Text;

namespace DeskVault.Application.Documents.Extraction.MarkdownDocument;

public sealed class MarkdownDocumentTextExtractor
    : IDocumentTextExtractor
{
    public bool CanExtract(string fileName)
    {
        return string.Equals(
            Path.GetExtension(fileName),
            ".md",
            StringComparison.OrdinalIgnoreCase);
    }

    public async Task<DocumentTextExtractionResult> ExtractAsync(
        Stream documentStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(
            documentStream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 1024,
            leaveOpen: true);

        string text = await reader.ReadToEndAsync(
            cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        return new DocumentTextExtractionResult(text);
    }
}
