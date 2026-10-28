using DeskVault.Application.Configurations;
using DeskVault.Application.Documents.Processing;
using System.Buffers;
using System.Security.Cryptography;
using System.Text.Json;
using SystemTextJsonDocument = System.Text.Json.JsonDocument;

namespace DeskVault.Application.Documents.Extraction.JsonDocument;

public sealed class JsonDocumentTextExtractor
    : IDocumentTextExtractor
{
    private const string RuleVersionValue =
        "json-extractor-v1";

    private readonly long _maxProcessedTextBytes;
    private readonly int _maxParserNestingDepth;
    private readonly int _maxStructuredNodeCount;
    private readonly long _maxDecryptedDocumentBytes;

    public JsonDocumentTextExtractor()
        : this(new DocumentProcessingOptions())
    {
    }

    public JsonDocumentTextExtractor(
        DocumentProcessingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _maxProcessedTextBytes = options.MaxProcessedTextBytes;
        _maxParserNestingDepth = options.MaxParserNestingDepth;
        _maxStructuredNodeCount = options.MaxStructuredNodeCount;
        _maxDecryptedDocumentBytes = options.MaxDecryptedDocumentBytes;

        if (_maxProcessedTextBytes <= 0 ||
            _maxParserNestingDepth <= 0 ||
            _maxStructuredNodeCount <= 0 ||
            _maxDecryptedDocumentBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "JSON processing resource limits must be greater than zero.");
        }
    }

    public string RuleVersion => RuleVersionValue;

    public bool CanExtract(string fileName)
    {
        return SupportedFileTypes.GetTextExtractionKind(fileName)
            == DocumentTextExtractionKind.Json;
    }

    public async Task<DocumentTextExtractionResult> ExtractAsync(
        Stream documentStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documentStream);
        cancellationToken.ThrowIfCancellationRequested();

        using var inputBuffer = new MemoryStream();

        byte[] readBuffer = ArrayPool<byte>.Shared.Rent(81920);

        try
        {
            await ReadBoundedInputAsync(
                documentStream,
                inputBuffer,
                readBuffer,
                cancellationToken);

            if (!inputBuffer.TryGetBuffer(
                    out ArraySegment<byte> inputSegment) ||
                inputSegment.Array is null)
            {
                throw new InvalidOperationException(
                    "Unable to access the bounded JSON input buffer.");
            }

            var jsonBytes = new ReadOnlyMemory<byte>(
                inputSegment.Array,
                inputSegment.Offset,
                checked((int)inputBuffer.Length));

            // The preflight reader operates on bytes directly, so exclude
            // an optional UTF-8 BOM from its input. ParseAsync receives the
            // original bounded stream; System.Text.Json supports a UTF-8 BOM
            // when parsing from a stream.
            int jsonStartOffset =
                jsonBytes.Length >= 3 &&
                jsonBytes.Span[0] == 0xEF &&
                jsonBytes.Span[1] == 0xBB &&
                jsonBytes.Span[2] == 0xBF
                    ? 3
                    : 0;

            ReadOnlyMemory<byte> jsonContent =
                jsonBytes.Slice(jsonStartOffset);

            ValidateJsonStructure(
                jsonContent.Span,
                cancellationToken);

            inputBuffer.Position = 0;

            using SystemTextJsonDocument document =
                await SystemTextJsonDocument.ParseAsync(
                    inputBuffer,
                    new JsonDocumentOptions
                    {
                        MaxDepth = _maxParserNestingDepth
                    },
                    cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            var output = new DocumentProcessingTextBuffer(
                _maxProcessedTextBytes);

            AppendElement(
                document.RootElement,
                output,
                indentationLevel: 0,
                propertyName: null,
                cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            return new DocumentTextExtractionResult(
                output.ToString().TrimEnd('\r', '\n'));
        }
        finally
        {
            if (inputBuffer.TryGetBuffer(
                    out ArraySegment<byte> inputSegment) &&
                inputSegment.Array is not null)
            {
                CryptographicOperations.ZeroMemory(
                    inputSegment.Array.AsSpan(
                        inputSegment.Offset,
                        inputSegment.Count));
            }

            CryptographicOperations.ZeroMemory(readBuffer);
            ArrayPool<byte>.Shared.Return(readBuffer);
        }
    }

    private async Task ReadBoundedInputAsync(
        Stream documentStream,
        MemoryStream inputBuffer,
        byte[] readBuffer,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            long remainingBytes =
                _maxDecryptedDocumentBytes - inputBuffer.Length;

            int bytesToRead = remainingBytes >= readBuffer.Length
                ? readBuffer.Length
                : checked((int)remainingBytes + 1);

            int bytesRead = await documentStream.ReadAsync(
                readBuffer.AsMemory(0, bytesToRead),
                cancellationToken);

            if (bytesRead == 0)
            {
                return;
            }

            long attemptedBytes =
                inputBuffer.Length + bytesRead;

            if (attemptedBytes > _maxDecryptedDocumentBytes)
            {
                throw new ResourceLimitExceededException(
                    _maxDecryptedDocumentBytes,
                    attemptedBytes);
            }

            inputBuffer.Write(
                readBuffer,
                0,
                bytesRead);
        }
    }

    private void ValidateJsonStructure(
        ReadOnlySpan<byte> jsonBytes,
        CancellationToken cancellationToken)
    {
        var reader = new Utf8JsonReader(
            jsonBytes,
            new JsonReaderOptions
            {
                MaxDepth = int.MaxValue
            });

        int nestingDepth = 0;
        long structuredNodeCount = 0;

        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();

            switch (reader.TokenType)
            {
                case JsonTokenType.StartObject:
                case JsonTokenType.StartArray:
                    nestingDepth++;

                    if (nestingDepth > _maxParserNestingDepth)
                    {
                        throw new DocumentStructureLimitExceededException(
                            "JSON nesting depth",
                            _maxParserNestingDepth,
                            nestingDepth);
                    }

                    break;

                case JsonTokenType.EndObject:
                case JsonTokenType.EndArray:
                    nestingDepth--;
                    break;
            }

            if (reader.TokenType is
                JsonTokenType.StartObject or
                JsonTokenType.StartArray or
                JsonTokenType.PropertyName or
                JsonTokenType.String or
                JsonTokenType.Number or
                JsonTokenType.True or
                JsonTokenType.False or
                JsonTokenType.Null)
            {
                structuredNodeCount++;

                if (structuredNodeCount > _maxStructuredNodeCount)
                {
                    throw new DocumentStructureLimitExceededException(
                        "JSON structured nodes",
                        _maxStructuredNodeCount,
                        structuredNodeCount);
                }
            }
        }
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
