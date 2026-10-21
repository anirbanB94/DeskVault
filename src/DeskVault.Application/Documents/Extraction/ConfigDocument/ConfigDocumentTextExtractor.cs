using System.Text;
using DeskVault.Application.Configurations;
using DeskVault.Application.Documents.Extraction.IniDocument;
using DeskVault.Application.Documents.Extraction.XmlDocument;
using DeskVault.Application.Documents.Processing;

namespace DeskVault.Application.Documents.Extraction.ConfigDocument;

public sealed class ConfigDocumentTextExtractor
    : IDocumentTextExtractor
{
    private const string RuleVersionValue =
        "config-content-dispatch-v1";

    private readonly long _maxDecryptedDocumentBytes;
    private readonly XmlDocumentTextExtractor _xmlExtractor;
    private readonly IniDocumentTextExtractor _iniExtractor;

    public ConfigDocumentTextExtractor()
        : this(new DocumentProcessingOptions())
    {
    }

    public ConfigDocumentTextExtractor(
        DocumentProcessingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _maxDecryptedDocumentBytes =
            options.MaxDecryptedDocumentBytes;

        _xmlExtractor =
            new XmlDocumentTextExtractor(options);

        _iniExtractor =
            new IniDocumentTextExtractor(options);
    }

    public string RuleVersion =>
        $"{RuleVersionValue}" +
        $"|xml={_xmlExtractor.RuleVersion}" +
        $"|ini={_iniExtractor.RuleVersion}";

    public bool CanExtract(string fileName)
    {
        return SupportedFileTypes.GetTextExtractionKind(fileName)
            == DocumentTextExtractionKind.Config;
    }

    public async Task<DocumentTextExtractionResult> ExtractAsync(
        Stream documentStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documentStream);

        cancellationToken.ThrowIfCancellationRequested();

        // Prefer inspecting and rewinding seekable streams so the
        // detector does not create a full copy of the document.
        if (documentStream.CanSeek
            && TryGetRemainingByteCount(
                documentStream,
                out long remainingBytes))
        {
            if (remainingBytes > _maxDecryptedDocumentBytes)
            {
                throw new ResourceLimitExceededException(
                    _maxDecryptedDocumentBytes,
                    remainingBytes);
            }

            long originalPosition =
                documentStream.Position;

            ConfigDocumentFormat format;

            try
            {
                format =
                    await DetectFormatAsync(
                        documentStream,
                        cancellationToken);
            }
            finally
            {
                // The caller owns the input stream. Restore its
                // original position even if detection fails.
                documentStream.Position =
                    originalPosition;
            }

            return await ExtractUsingFormatAsync(
                documentStream,
                fileName,
                format,
                cancellationToken);
        }

        // Non-seekable streams require bounded buffering so the
        // selected extractor can receive the complete original content.
        await using MemoryStream bufferedStream = new();

        byte[] buffer = new byte[81920];

        long bufferedBytes = 0;

        while (true)
        {
            int bytesRead =
                await documentStream.ReadAsync(
                    buffer.AsMemory(),
                    cancellationToken);

            if (bytesRead == 0)
            {
                break;
            }

            if (bytesRead >
                _maxDecryptedDocumentBytes - bufferedBytes)
            {
                throw new ResourceLimitExceededException(
                    _maxDecryptedDocumentBytes,
                    bufferedBytes + bytesRead);
            }

            await bufferedStream.WriteAsync(
                buffer.AsMemory(
                    0,
                    bytesRead),
                cancellationToken);

            bufferedBytes += bytesRead;
        }

        cancellationToken.ThrowIfCancellationRequested();

        bufferedStream.Position = 0;

        ConfigDocumentFormat bufferedFormat =
            await DetectFormatAsync(
                bufferedStream,
                cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        bufferedStream.Position = 0;

        return await ExtractUsingFormatAsync(
            bufferedStream,
            fileName,
            bufferedFormat,
            cancellationToken);
    }

    private static bool TryGetRemainingByteCount(
        Stream documentStream,
        out long remainingByteCount)
    {
        remainingByteCount = 0;

        try
        {
            long length =
                documentStream.Length;

            long position =
                documentStream.Position;

            remainingByteCount =
                Math.Max(
                    0L,
                    length - position);

            return true;
        }
        catch (NotSupportedException)
        {
            // Fall back to bounded buffering when the stream claims
            // seek support but does not expose its remaining length.
            return false;
        }
    }

    private async Task<DocumentTextExtractionResult> ExtractUsingFormatAsync(
        Stream documentStream,
        string fileName,
        ConfigDocumentFormat format,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return format switch
        {
            ConfigDocumentFormat.Xml =>
                await _xmlExtractor.ExtractAsync(
                    documentStream,
                    fileName,
                    cancellationToken),

            ConfigDocumentFormat.Ini =>
                await _iniExtractor.ExtractAsync(
                    documentStream,
                    fileName,
                    cancellationToken),

            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(format),
                    format,
                    "Unsupported configuration content format.")
        };
    }

    private static async Task<ConfigDocumentFormat> DetectFormatAsync(
        Stream documentStream,
        CancellationToken cancellationToken)
    {
        using StreamReader reader =
            new(
                documentStream,
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false),
                detectEncodingFromByteOrderMarks: true,
                bufferSize: 4096,
                leaveOpen: true);

        bool sawIniComment = false;

        string? line;

        while ((line = await reader.ReadLineAsync(
                   cancellationToken)) is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string candidate =
                line.Trim();

            if (candidate.Length == 0)
            {
                continue;
            }

            // XML declarations, elements, comments and other XML markup
            // begin with '<'. Malformed XML still goes to the XML parser
            // and must not fall back to INI.
            if (candidate.StartsWith(
                    "<",
                    StringComparison.Ordinal))
            {
                return ConfigDocumentFormat.Xml;
            }

            // IniParser's default comment delimiter is ';'.
            // Preserve comment-only INI input as an INI document.
            if (candidate[0] == ';')
            {
                sawIniComment = true;
                continue;
            }

            // A hash-prefixed line is not treated as a recognized
            // comment by this dispatch policy. Do not silently accept
            // content using syntax that the configured INI parser
            // may not recognize.

            if (candidate.StartsWith(
                    "[",
                    StringComparison.Ordinal)
                || candidate.IndexOf('=') > 0)
            {
                return ConfigDocumentFormat.Ini;
            }

            throw new InvalidDataException(
                "The .config document does not contain recognized XML or INI syntax.");
        }

        if (sawIniComment)
        {
            return ConfigDocumentFormat.Ini;
        }

        throw new InvalidDataException(
            "The .config document is empty or contains only whitespace and does not contain recognized XML or INI syntax.");
    }

    private enum ConfigDocumentFormat
    {
        Xml,
        Ini
    }
}
