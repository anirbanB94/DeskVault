using DeskVault.Application.Configurations;
using DeskVault.Application.Documents.Extraction;
using DeskVault.Application.Documents.Processing;

namespace DeskVault.Application.Documents.Normalization;

public sealed class DocumentTextNormalizer
    : IDocumentTextNormalizer
{

    private const string RuleVersionValue = "unicode-nfc-lf-v1";

    private readonly long _maxProcessedTextBytes;

    public DocumentTextNormalizer()
        : this(new DocumentProcessingOptions())
    {
    }

    public DocumentTextNormalizer(
        DocumentProcessingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.MaxProcessedTextBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options.MaxProcessedTextBytes),
                "Maximum processed text size must be greater than zero.");
        }

        _maxProcessedTextBytes =
            options.MaxProcessedTextBytes;
    }

    public string RuleVersion => RuleVersionValue;

    public Task<DocumentTextNormalizationResult> NormalizeAsync(
        DocumentTextExtractionResult extractionResult,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            extractionResult);

        cancellationToken.ThrowIfCancellationRequested();

        string normalizedText =
            extractionResult.Text.Normalize();

        var output =
            new DocumentProcessingTextBuffer(
                _maxProcessedTextBytes);

        for (int index = 0;
             index < normalizedText.Length;
             index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            char current =
                normalizedText[index];

            if (current == '\r')
            {
                output.Append('\n');

                if (index + 1 < normalizedText.Length &&
                    normalizedText[index + 1] == '\n')
                {
                    index++;
                }

                continue;
            }

            output.Append(current);
        }

        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(
            new DocumentTextNormalizationResult(
                output.ToString(),
                extractionResult.SourceLocationMappingKind));
    }
}
