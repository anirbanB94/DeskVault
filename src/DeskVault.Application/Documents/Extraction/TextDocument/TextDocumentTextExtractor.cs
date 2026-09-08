using System.Text;

namespace DeskVault.Application.Documents.Extraction.TextDocument;

public sealed class TextDocumentTextExtractor
    : IDocumentTextExtractor
{
    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt",
        ".log",
        ".c",
        ".cpp",
        ".h",
        ".hpp",
        ".cs",
        ".java",
        ".py",
        ".js",
        ".ts",
        ".css",
        ".sql",
        ".ps1"
    };

    public bool CanExtract(string fileName)
    {
        return SupportedExtensions.Contains(
            Path.GetExtension(fileName));
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
