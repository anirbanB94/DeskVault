using DeskVault.Application.Configurations;
using DeskVault.Application.Documents.Processing;
using DeskVault.Application.Documents.Provenance;
using System.Text;

namespace DeskVault.Application.Documents.Extraction.TextDocument;

public sealed class TextDocumentTextExtractor
    : IDocumentTextExtractor
{
    private const string RuleVersionValue =
        "text-extractor-v1";

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

    private readonly long _maxProcessedTextBytes;

    public TextDocumentTextExtractor()
        : this(new DocumentProcessingOptions())
    {
    }

    public TextDocumentTextExtractor(
        DocumentProcessingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _maxProcessedTextBytes =
            options.MaxProcessedTextBytes;
    }

    public string RuleVersion => RuleVersionValue;

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
        ArgumentNullException.ThrowIfNull(documentStream);

        cancellationToken.ThrowIfCancellationRequested();

        using var reader =
            new StreamReader(
                documentStream,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true,
                bufferSize: 1024,
                leaveOpen: true);

        var textBuffer =
            new DocumentProcessingTextBuffer(
                _maxProcessedTextBytes);

        char[] buffer = new char[4096];

        while (true)
        {
            int charsRead =
                await reader.ReadAsync(
                    buffer.AsMemory(),
                    cancellationToken);

            if (charsRead == 0)
            {
                break;
            }

            textBuffer.Append(
                buffer.AsSpan(0, charsRead));

            cancellationToken.ThrowIfCancellationRequested();
        }

        cancellationToken.ThrowIfCancellationRequested();

        return new DocumentTextExtractionResult(
            textBuffer.ToString(),
            DocumentSourceLocationMappingKind.DirectText);
    }
}
