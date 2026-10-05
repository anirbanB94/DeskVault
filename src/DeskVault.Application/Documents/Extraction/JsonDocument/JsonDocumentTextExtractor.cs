using DeskVault.Application.Configurations;
using DeskVault.Application.Documents.Processing;
using System.Text.Json;
using SystemTextJsonDocument = System.Text.Json.JsonDocument;

namespace DeskVault.Application.Documents.Extraction.JsonDocument;

public sealed class JsonDocumentTextExtractor
    : IDocumentTextExtractor
{
    private const string RuleVersionValue =
        "json-extractor-v1";

    private const string SupportedExtension = ".json";

    private readonly long _maxProcessedTextBytes;

    public JsonDocumentTextExtractor()
        : this(new DocumentProcessingOptions())
    {
    }

    public JsonDocumentTextExtractor(
        DocumentProcessingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _maxProcessedTextBytes =
            options.MaxProcessedTextBytes;
    }

    public string RuleVersion => RuleVersionValue;

    public bool CanExtract(
        string fileName)
    {
        return Path.GetExtension(fileName)
            .Equals(
                SupportedExtension,
                StringComparison.OrdinalIgnoreCase);
    }

    public async Task<DocumentTextExtractionResult> ExtractAsync(
        Stream documentStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using SystemTextJsonDocument document =
            await SystemTextJsonDocument.ParseAsync(
                documentStream,
                cancellationToken: cancellationToken);

        var output =
            new DocumentProcessingTextBuffer(
                _maxProcessedTextBytes);

        AppendElement(
            document.RootElement,
            output,
            indentationLevel: 0,
            propertyName: null,
            cancellationToken);

        return new DocumentTextExtractionResult(
            output
                .ToString()
                .TrimEnd('\r', '\n'));
    }

    private static void AppendElement(
        JsonElement element,
        DocumentProcessingTextBuffer output,
        int indentationLevel,
        string? propertyName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                AppendObject(
                    element,
                    output,
                    indentationLevel,
                    propertyName,
                    cancellationToken);
                break;

            case JsonValueKind.Array:
                AppendArray(
                    element,
                    output,
                    indentationLevel,
                    propertyName,
                    cancellationToken);
                break;

            default:
                AppendScalar(
                    element,
                    output,
                    indentationLevel,
                    propertyName);
                break;
        }
    }

    private static void AppendObject(
        JsonElement element,
        DocumentProcessingTextBuffer output,
        int indentationLevel,
        string? propertyName,
        CancellationToken cancellationToken)
    {
        if (propertyName is not null)
        {
            AppendIndentation(
                output,
                indentationLevel);

            output.Append(propertyName);
            output.Append(':');
            output.AppendLine();
        }

        foreach (JsonProperty property in element.EnumerateObject())
        {
            cancellationToken.ThrowIfCancellationRequested();

            AppendElement(
                property.Value,
                output,
                propertyName is null
                    ? indentationLevel
                    : indentationLevel + 1,
                property.Name,
                cancellationToken);
        }
    }

    private static void AppendArray(
        JsonElement element,
        DocumentProcessingTextBuffer output,
        int indentationLevel,
        string? propertyName,
        CancellationToken cancellationToken)
    {
        if (propertyName is not null)
        {
            AppendIndentation(
                output,
                indentationLevel);

            output.Append(propertyName);
            output.Append(':');
            output.AppendLine();
        }

        int index = 0;

        foreach (JsonElement item in element.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();

            AppendIndentation(
                output,
                propertyName is null
                    ? indentationLevel
                    : indentationLevel + 1);

            output.Append('[');
            output.Append(index);
            output.Append(']');

            if (item.ValueKind is
                JsonValueKind.Object or
                JsonValueKind.Array)
            {
                output.Append(':');
                output.AppendLine();

                AppendElement(
                    item,
                    output,
                    propertyName is null
                        ? indentationLevel + 1
                        : indentationLevel + 2,
                    propertyName: null,
                    cancellationToken);
            }
            else
            {
                output.Append(": ");
                output.Append(
                    GetScalarText(item));
                output.AppendLine();
            }

            index++;
        }
    }

    private static void AppendScalar(
        JsonElement element,
        DocumentProcessingTextBuffer output,
        int indentationLevel,
        string? propertyName)
    {
        AppendIndentation(
            output,
            indentationLevel);

        if (propertyName is not null)
        {
            output.Append(propertyName);
            output.Append(": ");
        }

        output.Append(
            GetScalarText(element));

        output.AppendLine();
    }

    private static string GetScalarText(
        JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String =>
                element.GetString() ?? string.Empty,

            JsonValueKind.Number =>
                element.GetRawText(),

            JsonValueKind.True =>
                "true",

            JsonValueKind.False =>
                "false",

            JsonValueKind.Null =>
                "null",

            _ =>
                element.GetRawText()
        };
    }

    private static void AppendIndentation(
        DocumentProcessingTextBuffer output,
        int indentationLevel)
    {
        int indentationWidth =
            indentationLevel * 2;

        for (int index = 0;
             index < indentationWidth;
             index++)
        {
            output.Append(' ');
        }
    }
}
