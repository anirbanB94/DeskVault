using System.Text;
using DeskVault.Application.Configurations;
using DeskVault.Application.Documents.Content;
using DeskVault.Application.Documents.Extraction;
using DeskVault.Application.Documents.Processing;

namespace DeskVault.Application.Documents.Normalization;

public sealed class DocumentTextNormalizer
    : IDocumentTextNormalizer
{
    private const string RuleVersionValue =
        "unicode-nfc-lf-v1";

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

        // Structured content becomes the source of the searchable projection;
        // text-only extraction continues to use the existing text path.
        DocumentContent? normalizedContent =
            extractionResult.Content is null
                ? null
                : NormalizeContent(
                    extractionResult.Content,
                    cancellationToken);

        string sourceText =
            normalizedContent?.SearchableText
            ?? extractionResult.Text;

        // Keep the existing normalization algorithm intact so text-only
        // processing and its resource-limit behavior do not change.
        string normalizedText =
            sourceText.Normalize();

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
                extractionResult.SourceLocationMappingKind,
                normalizedContent));
    }

    private static DocumentContent NormalizeContent(
        DocumentContent content,
        CancellationToken cancellationToken)
    {
        var units =
            new DocumentContentUnit[
                content.Units.Count];

        for (int unitIndex = 0;
             unitIndex < content.Units.Count;
             unitIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            DocumentContentUnit unit =
                content.Units[unitIndex];

            switch (unit.Kind)
            {
                case DocumentContentUnitKind.Text:
                    units[unitIndex] =
                        new DocumentContentUnit(
                            unit.Order,
                            unit.Kind,
                            text: NormalizeValue(
                                unit.Text!,
                                cancellationToken));
                    break;

                case DocumentContentUnitKind.TableRow:
                    units[unitIndex] =
                        new DocumentContentUnit(
                            unit.Order,
                            unit.Kind,
                            fields: NormalizeFields(
                                unit.Fields,
                                cancellationToken));
                    break;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(unit.Kind),
                        unit.Kind,
                        "Unsupported document-content unit kind.");
            }
        }

        // Warnings describe extraction conditions rather than document text,
        // so normalization does not alter or add them to the searchable projection.
        return new DocumentContent(
            units,
            content.Warnings);
    }

    private static IReadOnlyList<DocumentContentField> NormalizeFields(
        IReadOnlyList<DocumentContentField> fields,
        CancellationToken cancellationToken)
    {
        var normalizedFields =
            new DocumentContentField[
                fields.Count];

        for (int fieldIndex = 0;
             fieldIndex < fields.Count;
             fieldIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            DocumentContentField field =
                fields[fieldIndex];

            normalizedFields[fieldIndex] =
                new DocumentContentField(
                    NormalizeValue(
                        field.Name,
                        cancellationToken),
                    NormalizeValue(
                        field.Value,
                        cancellationToken));
        }

        return normalizedFields;
    }

    private static string NormalizeValue(
        string value,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(value);

        string normalized =
            value.Normalize();

        if (normalized.Length == 0)
        {
            return string.Empty;
        }

        var output =
            new StringBuilder(
                normalized.Length);

        for (int index = 0;
             index < normalized.Length;
             index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            char current =
                normalized[index];

            if (current == '\r')
            {
                output.Append('\n');

                if (index + 1 < normalized.Length &&
                    normalized[index + 1] == '\n')
                {
                    index++;
                }

                continue;
            }

            output.Append(current);
        }

        return output.ToString();
    }
}
