using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using System.Text;

namespace DeskVault.UI.Rendering.HtmlDocumentRendering;

/// <summary>
/// Reads, validates and composes bounded HTML preview content.
/// </summary>
/// <remarks>
/// Source content is accepted as strict UTF-8, with or without a UTF-8 BOM.
/// The complete HTML payload is measured as UTF-16 bytes before it is
/// returned for WebView2 navigation. The supplied source stream remains
/// owned by the caller.
/// </remarks>
internal static class HtmlDocumentContentPayloadBuilder
{
    /// <summary>
    /// Maximum number of source bytes accepted for an HTML preview.
    /// </summary>
    internal const int MaximumSourceBytes =
        1_048_576;

    /// <summary>
    /// Maximum number of UTF-16 payload bytes accepted for WebView2 navigation.
    /// </summary>
    internal const int MaximumComposedPayloadBytes =
        1_048_576;

    private const int ReadBufferSize =
        81_920;

    private const string SourceSizeExceededMessage =
        "The HTML source exceeds the 1 MiB preview limit.";

    private const string PayloadSizeExceededMessage =
        "The composed HTML preview payload exceeds the 1 MiB preview limit.";

    private const string UnsupportedEncodingMessage =
        "The HTML source uses an unsupported character encoding. UTF-8 is required.";

    private const string InvalidEncodingDataMessage =
        "The HTML source contains invalid UTF-8 data.";

    private const string UnsupportedMarkupMessage =
        "The HTML source contains markup that is not permitted by the preview policy.";

    private static readonly UTF8Encoding StrictUtf8Encoding =
        new(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true);

    private static readonly byte[] Utf8ByteOrderMark =
    [
        0xEF, 0xBB, 0xBF
    ];

    private static readonly byte[] Utf16LittleEndianByteOrderMark =
    [
        0xFF, 0xFE
    ];

    private static readonly byte[] Utf16BigEndianByteOrderMark =
    [
        0xFE, 0xFF
    ];

    private static readonly byte[] Utf32LittleEndianByteOrderMark =
    [
        0xFF, 0xFE, 0x00, 0x00
    ];

    private static readonly byte[] Utf32BigEndianByteOrderMark =
    [
        0x00, 0x00, 0xFE, 0xFF
    ];

    /// <summary>
    /// Reads and validates an HTML source stream and returns its complete
    /// WebView2-ready HTML payload.
    /// </summary>
    /// <param name="documentStream">
    /// The caller-owned source stream. It is neither disposed nor retained.
    /// </param>
    /// <param name="cancellationToken">
    /// A token used to cancel bounded source reading and payload creation.
    /// </param>
    /// <returns>
    /// The complete HTML document, including the preview security policy.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="documentStream"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the source stream cannot be read.
    /// </exception>
    /// <exception cref="InvalidDataException">
    /// Thrown when the source or composed payload exceeds its limit, the
    /// encoding is unsupported or invalid, or the markup violates the
    /// HTML preview policy.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the operation is cancelled.
    /// </exception>
    internal static async Task<string> BuildAsync(
        Stream documentStream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documentStream);

        if (!documentStream.CanRead)
        {
            throw new ArgumentException(
                "The HTML source stream must be readable.",
                nameof(documentStream));
        }

        byte[] sourceBytes =
            await ReadBoundedSourceAsync(
                documentStream,
                cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        string markup =
            DecodeSource(sourceBytes);

        cancellationToken.ThrowIfCancellationRequested();

        return CreatePayload(markup);
    }

    /// <summary>
    /// Validates source markup and composes the complete HTML document
    /// with the dedicated HTML preview Content Security Policy.
    /// </summary>
    /// <param name="markup">The decoded source HTML.</param>
    /// <returns>The complete HTML payload.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="markup"/> is null.
    /// </exception>
    /// <exception cref="InvalidDataException">
    /// Thrown when the markup declares an unsupported encoding, violates
    /// the embedded-resource policy, or exceeds the composed payload limit.
    /// </exception>
    internal static string CreatePayload(
        string markup)
    {
        ArgumentNullException.ThrowIfNull(markup);

        if (!AreEncodingDeclarationsSupported(markup))
        {
            throw new InvalidDataException(
                UnsupportedEncodingMessage);
        }

        if (!HtmlDocumentEmbeddedResourcePolicy.IsMarkupAllowed(markup))
        {
            throw new InvalidDataException(
                UnsupportedMarkupMessage);
        }

        string composedHtml =
            WrapHtmlDocument(markup);

        int payloadByteCount =
            Encoding.Unicode.GetByteCount(composedHtml);

        if (payloadByteCount > MaximumComposedPayloadBytes)
        {
            throw new InvalidDataException(
                PayloadSizeExceededMessage);
        }

        return composedHtml;
    }

    /// <summary>
    /// Reads no more than one byte beyond the configured source limit.
    /// </summary>
    /// <param name="documentStream">The caller-owned source stream.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The bounded source bytes.</returns>
    private static async Task<byte[]> ReadBoundedSourceAsync(
        Stream documentStream,
        CancellationToken cancellationToken)
    {
        using var contentBuffer =
            new MemoryStream();

        byte[] readBuffer =
            new byte[ReadBufferSize];

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int bufferedBytes =
                checked((int)contentBuffer.Length);

            int remainingBytes =
                MaximumSourceBytes - bufferedBytes;

            int requestedBytes =
                Math.Min(
                    readBuffer.Length,
                    remainingBytes + 1);

            int bytesRead =
                await documentStream.ReadAsync(
                    readBuffer.AsMemory(0, requestedBytes),
                    cancellationToken).ConfigureAwait(false);

            if (bytesRead == 0)
            {
                break;
            }

            if (bytesRead > remainingBytes)
            {
                throw new InvalidDataException(
                    SourceSizeExceededMessage);
            }

            contentBuffer.Write(
                readBuffer,
                0,
                bytesRead);
        }

        return contentBuffer.ToArray();
    }

    /// <summary>
    /// Decodes strict UTF-8 source bytes and rejects unsupported Unicode BOMs.
    /// </summary>
    /// <param name="sourceBytes">The bounded source bytes.</param>
    /// <returns>The decoded HTML markup.</returns>
    private static string DecodeSource(
        byte[] sourceBytes)
    {
        ReadOnlySpan<byte> source =
            sourceBytes;

        if (source.StartsWith(Utf32LittleEndianByteOrderMark)
            || source.StartsWith(Utf32BigEndianByteOrderMark)
            || source.StartsWith(Utf16LittleEndianByteOrderMark)
            || source.StartsWith(Utf16BigEndianByteOrderMark))
        {
            throw new InvalidDataException(
                UnsupportedEncodingMessage);
        }

        if (source.StartsWith(Utf8ByteOrderMark))
        {
            source =
                source[Utf8ByteOrderMark.Length..];
        }

        try
        {
            return StrictUtf8Encoding.GetString(source);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException(
                InvalidEncodingDataMessage,
                exception);
        }
    }

    /// <summary>
    /// Rejects HTML character-encoding declarations that conflict with
    /// the builder's strict UTF-8 source-decoding contract.
    /// </summary>
    /// <param name="markup">The decoded HTML markup.</param>
    /// <returns>
    /// <see langword="true"/> when all explicit encoding declarations
    /// identify UTF-8 or no encoding is declared.
    /// </returns>
    private static bool AreEncodingDeclarationsSupported(
        string markup)
    {
        var parser =
            new HtmlParser(
                new HtmlParserOptions
                {
                    IsScripting = false
                });

        IHtmlDocument document =
            parser.ParseDocument(markup);

        foreach (IElement metaElement in document.QuerySelectorAll("meta"))
        {
            string? charset =
                metaElement.GetAttribute("charset");

            if (charset is not null
                && !IsUtf8EncodingName(charset))
            {
                return false;
            }

            if (!string.Equals(
                    metaElement.GetAttribute("http-equiv"),
                    "content-type",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string? contentType =
                metaElement.GetAttribute("content");

            string? declaredCharset =
                GetDeclaredCharset(contentType);

            if (declaredCharset is not null
                && !IsUtf8EncodingName(declaredCharset))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Determines whether a charset label identifies UTF-8.
    /// </summary>
    private static bool IsUtf8EncodingName(
        string charset)
    {
        string normalizedCharset =
            charset.Trim();

        return string.Equals(
                   normalizedCharset,
                   "utf-8",
                   StringComparison.OrdinalIgnoreCase)
               || string.Equals(
                   normalizedCharset,
                   "utf8",
                   StringComparison.OrdinalIgnoreCase)
               || string.Equals(
                   normalizedCharset,
                   "unicode-1-1-utf-8",
                   StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Extracts a charset parameter from an HTML content-type declaration.
    /// </summary>
    private static string? GetDeclaredCharset(
        string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return null;
        }

        foreach (string parameter in contentType.Split(
                     ';',
                     StringSplitOptions.TrimEntries))
        {
            int separatorIndex =
                parameter.IndexOf('=');

            if (separatorIndex <= 0)
            {
                continue;
            }

            string parameterName =
                parameter[..separatorIndex].Trim();

            if (!string.Equals(
                    parameterName,
                    "charset",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return parameter[(separatorIndex + 1)..]
                .Trim()
                .Trim('"')
                .Trim('\'');
        }

        return null;
    }

    /// <summary>
    /// Inserts the host-owned preview policy and a default light color scheme
    /// into the parsed document head while preserving the source document
    /// structure and any existing color-scheme declaration.
    /// </summary>
    private static string WrapHtmlDocument(
        string markup)
    {
        var parser =
            new HtmlParser(
                new HtmlParserOptions
                {
                    IsScripting = false
                });

        IHtmlDocument document =
            parser.ParseDocument(markup);

        IElement head =
            document.QuerySelector("head")
            ?? throw new InvalidDataException(
                "The HTML source could not be composed into a document head.");

        IElement documentElement =
            document.QuerySelector("html")
            ?? throw new InvalidDataException(
                "The HTML source could not be composed into an HTML document.");

        string contentSecurityPolicy =
            HtmlDocumentRenderingSecurityPolicy
                .CreateContentSecurityPolicy();

        IElement charsetMeta =
            document.CreateElement("meta");

        charsetMeta.SetAttribute(
            "charset",
            "utf-8");

        IElement contentSecurityPolicyMeta =
            document.CreateElement("meta");

        contentSecurityPolicyMeta.SetAttribute(
            "http-equiv",
            "Content-Security-Policy");

        contentSecurityPolicyMeta.SetAttribute(
            "content",
            contentSecurityPolicy);

        IElement viewportMeta =
            document.CreateElement("meta");

        viewportMeta.SetAttribute(
            "name",
            "viewport");

        viewportMeta.SetAttribute(
            "content",
            "width=device-width, initial-scale=1.0");

        head.InsertBefore(
            contentSecurityPolicyMeta,
            head.FirstChild);

        head.InsertBefore(
            charsetMeta,
            contentSecurityPolicyMeta);

        head.AppendChild(viewportMeta);

        bool hasColorSchemeDeclaration =
            head.QuerySelectorAll("meta")
                .Any(meta =>
                    string.Equals(
                        meta.GetAttribute("name"),
                        "color-scheme",
                        StringComparison.OrdinalIgnoreCase));

        if (!hasColorSchemeDeclaration)
        {
            IElement colorSchemeMeta =
                document.CreateElement("meta");

            colorSchemeMeta.SetAttribute(
                "name",
                "color-scheme");

            colorSchemeMeta.SetAttribute(
                "content",
                "light");

            IElement? firstStyleElement =
                head.QuerySelector(
                    "style, link[rel~='stylesheet']");

            if (firstStyleElement is null)
            {
                head.AppendChild(colorSchemeMeta);
            }
            else
            {
                head.InsertBefore(
                    colorSchemeMeta,
                    firstStyleElement);
            }
        }

        IElement previewCanvasStyleElement =
        document.CreateElement("style");

        previewCanvasStyleElement.TextContent =
            "html { color-scheme: light; background-color: #fff; }";

        IElement? firstSourceStyleElement =
            head.QuerySelector("style, link[rel~='stylesheet']");

        if (firstSourceStyleElement is null)
        {
            head.AppendChild(previewCanvasStyleElement);
        }
        else
        {
            head.InsertBefore(
                previewCanvasStyleElement,
                firstSourceStyleElement);
        }

        return
            "<!DOCTYPE html>\n"
            + documentElement.OuterHtml;
    }
}
