using AngleSharp.Html.Parser;
using DeskVault.UI.Rendering.HtmlDocumentRendering;
using System.Text;

namespace DeskVault.UI.Tests;

/// <summary>
/// Verifies bounded HTML source reading, encoding validation,
/// security-policy composition and final UTF-16 payload limits.
/// </summary>
public sealed class HtmlDocumentContentPayloadBuilderTests
{
    /// <summary>
    /// Verifies that supported static markup, inline styling and
    /// approved embedded images are retained in the secured payload.
    /// </summary>
    [Fact]
    public async Task BuildAsync_WithSupportedMarkup_ComposesSecuredPayload()
    {
        // Arrange
        const string markup =
            """
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset="utf-8" />
                <title>HTML preview title</title>
                <style>
                    body { color: #222222; }
                </style>
            </head>
            <body>
                <h1>HTML preview</h1>
                <p style="font-weight: 600;">Static content</p>
                <img
                    alt="Embedded image"
                    src="data:image/png;base64,iVBORw0KGgo=" />
            </body>
            </html>
            """;

        using var sourceStream =
            new NonSeekableReadStream(
                Encoding.UTF8.GetBytes(markup));

        // Act
        string payload =
            await HtmlDocumentContentPayloadBuilder.BuildAsync(
                sourceStream,
                TestContext.Current.CancellationToken);

        // Assert
        int policyIndex =
            payload.IndexOf(
                "Content-Security-Policy",
                StringComparison.Ordinal);

        int sourceIndex =
            payload.IndexOf(
                "<h1>HTML preview</h1>",
                StringComparison.Ordinal);

        Assert.True(policyIndex >= 0);
        Assert.True(sourceIndex > policyIndex);

        Assert.Contains("default-src 'none'", payload);
        Assert.Contains("script-src 'none'", payload);
        Assert.Contains("style-src 'unsafe-inline'", payload);
        Assert.Contains("img-src data:", payload);
        Assert.Contains("data:image/png;base64,iVBORw0KGgo=", payload);
        Assert.Contains("Static content", payload);

        var parsedDocument =
            new HtmlParser(
                new HtmlParserOptions
                {
                    IsScripting = false
                })
            .ParseDocument(payload);

        Assert.Equal(
            "HTML preview title",
            parsedDocument.Title);

        Assert.NotNull(
            parsedDocument.QuerySelector("head > style"));

        Assert.NotNull(
            parsedDocument.QuerySelector("body > h1"));

        Assert.NotNull(
            parsedDocument.QuerySelector(
                "head > meta[http-equiv='Content-Security-Policy']"));

        Assert.Null(
            parsedDocument.QuerySelector("body > head"));

        Assert.True(
            Encoding.Unicode.GetByteCount(payload)
            <= HtmlDocumentContentPayloadBuilder.MaximumComposedPayloadBytes);

        Assert.True(sourceStream.CanRead);

        Assert.Equal(
            Encoding.UTF8.GetByteCount(markup),
            sourceStream.BytesRead);
    }

    /// <summary>
    /// Verifies that HTML without an explicit color-scheme declaration
    /// receives a light color-scheme default before its source styles.
    /// </summary>
    [Fact]
    public void CreatePayload_WhenSourceDoesNotDeclareColorScheme_AddsLightDefault()
    {
        // Arrange
        const string markup =
            """
            <!DOCTYPE html>
            <html>
            <head>
                <title>Light theme default</title>
                <style>
                    body { color: #222222; }
                </style>
            </head>
            <body>
                <p>Static content</p>
            </body>
            </html>
            """;

        // Act
        string payload =
            HtmlDocumentContentPayloadBuilder.CreatePayload(markup);

        var parsedDocument =
            new HtmlParser(
                new HtmlParserOptions
                {
                    IsScripting = false
                })
            .ParseDocument(payload);

        // Assert
        var colorSchemeMeta =
            parsedDocument.QuerySelector(
                "head > meta[name='color-scheme']");

        Assert.NotNull(colorSchemeMeta);

        Assert.Equal(
            "light",
            colorSchemeMeta.GetAttribute("content"));

        Assert.NotNull(
            parsedDocument.QuerySelector(
                "head > meta[name='color-scheme'] + style"));
    }

    /// <summary>
    /// Verifies that an explicit source color-scheme declaration is
    /// preserved without adding a conflicting default declaration.
    /// </summary>
    [Fact]
    public void CreatePayload_WhenSourceDeclaresColorScheme_PreservesDeclaration()
    {
        // Arrange
        const string markup =
            """
            <!DOCTYPE html>
            <html>
            <head>
                <meta name="color-scheme" content="dark" />
                <title>Dark theme</title>
                <style>
                    body { background: #222222; color: #ffffff; }
                </style>
            </head>
            <body>
                <p>Static content</p>
            </body>
            </html>
            """;

        // Act
        string payload =
            HtmlDocumentContentPayloadBuilder.CreatePayload(markup);

        var parsedDocument =
            new HtmlParser(
                new HtmlParserOptions
                {
                    IsScripting = false
                })
            .ParseDocument(payload);

        // Assert
        var colorSchemeMetaElements =
            parsedDocument.QuerySelectorAll(
                "head > meta[name='color-scheme']");

        var colorSchemeMeta =
            Assert.Single(colorSchemeMetaElements);

        Assert.Equal(
            "dark",
            colorSchemeMeta.GetAttribute("content"));
    }

    /// <summary>
    /// Verifies that a source exactly at the raw byte limit is read
    /// completely and rejected only when its composed payload exceeds
    /// the separate UTF-16 payload limit.
    /// </summary>
    [Fact]
    public async Task BuildAsync_WhenSourceIsAtRawByteLimit_EnforcesComposedPayloadLimit()
    {
        // Arrange
        byte[] sourceBytes =
            Encoding.UTF8.GetBytes(
                new string('a', HtmlDocumentContentPayloadBuilder.MaximumSourceBytes));

        using var sourceStream =
            new NonSeekableReadStream(sourceBytes);

        // Act
        InvalidDataException exception =
            await Assert.ThrowsAsync<InvalidDataException>(
                () => HtmlDocumentContentPayloadBuilder.BuildAsync(
                    sourceStream,
                    TestContext.Current.CancellationToken));

        // Assert
        Assert.Contains("composed HTML preview payload", exception.Message);

        Assert.Equal(
            HtmlDocumentContentPayloadBuilder.MaximumSourceBytes,
            sourceStream.BytesRead);

        Assert.True(sourceStream.CanRead);
    }

    /// <summary>
    /// Verifies that oversized source input is rejected after reading
    /// no more than one byte beyond the configured raw source limit.
    /// </summary>
    [Fact]
    public async Task BuildAsync_WhenSourceExceedsRawByteLimit_RejectsWithoutReadingUnboundedInput()
    {
        // Arrange
        byte[] sourceBytes =
            new byte[HtmlDocumentContentPayloadBuilder.MaximumSourceBytes + 4096];

        Array.Fill(sourceBytes, (byte)'a');

        using var sourceStream =
            new NonSeekableReadStream(sourceBytes);

        // Act
        InvalidDataException exception =
            await Assert.ThrowsAsync<InvalidDataException>(
                () => HtmlDocumentContentPayloadBuilder.BuildAsync(
                    sourceStream,
                    TestContext.Current.CancellationToken));

        // Assert
        Assert.Contains("HTML source exceeds", exception.Message);

        Assert.Equal(
            HtmlDocumentContentPayloadBuilder.MaximumSourceBytes + 1,
            sourceStream.BytesRead);

        Assert.True(sourceStream.CanRead);
    }

    /// <summary>
    /// Verifies that embedded PNG data contributes to the complete UTF-16
    /// payload limit even when the raw HTML source remains below its own limit.
    /// </summary>
    [Fact]
    public async Task BuildAsync_WhenEmbeddedImageDataExceedsComposedPayloadLimit_RejectsCompletePayload()
    {
        // Arrange
        byte[] embeddedImageBytes =
            new byte[400_000];

        byte[] pngSignature =
        [
            0x89, 0x50, 0x4E, 0x47,
            0x0D, 0x0A, 0x1A, 0x0A
        ];

        pngSignature.CopyTo(embeddedImageBytes, 0);

        string embeddedImageDataUri =
            "data:image/png;base64,"
            + Convert.ToBase64String(embeddedImageBytes);

        string markup =
            "<!DOCTYPE html><html><body><img alt=\"Embedded\" src=\""
            + embeddedImageDataUri
            + "\" /></body></html>";

        byte[] sourceBytes =
            Encoding.UTF8.GetBytes(markup);

        Assert.True(
            sourceBytes.Length < HtmlDocumentContentPayloadBuilder.MaximumSourceBytes,
            "The fixture must remain below the raw source limit.");

        using var sourceStream =
            new NonSeekableReadStream(sourceBytes);

        // Act
        InvalidDataException exception =
            await Assert.ThrowsAsync<InvalidDataException>(
                () => HtmlDocumentContentPayloadBuilder.BuildAsync(
                    sourceStream,
                    TestContext.Current.CancellationToken));

        // Assert
        Assert.Contains("composed HTML preview payload", exception.Message);
        Assert.Equal(sourceBytes.Length, sourceStream.BytesRead);
        Assert.True(sourceStream.CanRead);
    }

    /// <summary>
    /// Verifies that a complete composed payload at exactly 1 MiB
    /// of UTF-16 data is accepted.
    /// </summary>
    [Fact]
    public async Task BuildAsync_WhenComposedPayloadIsAtLimit_AcceptsCompletePayload()
    {
        // Arrange
        string emptyPayload =
            HtmlDocumentContentPayloadBuilder.CreatePayload(
                string.Empty);

        int wrapperByteCount =
            Encoding.Unicode.GetByteCount(emptyPayload);

        int remainingPayloadBytes =
            HtmlDocumentContentPayloadBuilder.MaximumComposedPayloadBytes
            - wrapperByteCount;

        Assert.Equal(0, remainingPayloadBytes % 2);

        int markupCharacterCount =
            remainingPayloadBytes / 2;

        string markup =
            new('a', markupCharacterCount);

        using var sourceStream =
            new NonSeekableReadStream(
                Encoding.UTF8.GetBytes(markup));

        // Act
        string payload =
            await HtmlDocumentContentPayloadBuilder.BuildAsync(
                sourceStream,
                TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            HtmlDocumentContentPayloadBuilder.MaximumComposedPayloadBytes,
            Encoding.Unicode.GetByteCount(payload));

        Assert.Contains(markup, payload);
        Assert.True(sourceStream.CanRead);
    }

    /// <summary>
    /// Verifies that a composed payload one UTF-16 code unit beyond
    /// the configured limit is rejected rather than truncated.
    /// </summary>
    [Fact]
    public async Task BuildAsync_WhenComposedPayloadExceedsLimit_RejectsWithoutTruncation()
    {
        // Arrange
        string emptyPayload =
            HtmlDocumentContentPayloadBuilder.CreatePayload(
                string.Empty);

        int wrapperByteCount =
            Encoding.Unicode.GetByteCount(emptyPayload);

        int markupCharacterCount =
            (HtmlDocumentContentPayloadBuilder.MaximumComposedPayloadBytes
             - wrapperByteCount) / 2;

        string markup =
            new('a', markupCharacterCount + 1);

        using var sourceStream =
            new NonSeekableReadStream(
                Encoding.UTF8.GetBytes(markup));

        // Act
        InvalidDataException exception =
            await Assert.ThrowsAsync<InvalidDataException>(
                () => HtmlDocumentContentPayloadBuilder.BuildAsync(
                    sourceStream,
                    TestContext.Current.CancellationToken));

        // Assert
        Assert.Contains("composed HTML preview payload", exception.Message);
        Assert.True(sourceStream.CanRead);
    }

    /// <summary>
    /// Verifies that invalid UTF-8 byte sequences are rejected
    /// through the controlled invalid-data failure.
    /// </summary>
    [Fact]
    public async Task BuildAsync_WhenSourceContainsInvalidUtf8_RejectsContent()
    {
        // Arrange
        byte[] sourceBytes =
        [
            0x3C, 0x70, 0x3E,
            0xC3, 0x28,
            0x3C, 0x2F, 0x70, 0x3E
        ];

        using var sourceStream =
            new NonSeekableReadStream(sourceBytes);

        // Act
        InvalidDataException exception =
            await Assert.ThrowsAsync<InvalidDataException>(
                () => HtmlDocumentContentPayloadBuilder.BuildAsync(
                    sourceStream,
                    TestContext.Current.CancellationToken));

        // Assert
        Assert.Contains("invalid UTF-8", exception.Message);
        Assert.True(sourceStream.CanRead);
    }

    /// <summary>
    /// Verifies that UTF-16 source content is rejected because
    /// the HTML preview source contract accepts UTF-8 only.
    /// </summary>
    [Fact]
    public async Task BuildAsync_WhenSourceUsesUtf16_RejectsUnsupportedEncoding()
    {
        // Arrange
        byte[] sourceBytes =
        [
            0xFF, 0xFE,
            0x3C, 0x00,
            0x70, 0x00,
            0x3E, 0x00
        ];

        using var sourceStream =
            new NonSeekableReadStream(sourceBytes);

        // Act
        InvalidDataException exception =
            await Assert.ThrowsAsync<InvalidDataException>(
                () => HtmlDocumentContentPayloadBuilder.BuildAsync(
                    sourceStream,
                    TestContext.Current.CancellationToken));

        // Assert
        Assert.Contains("UTF-8 is required", exception.Message);
        Assert.True(sourceStream.CanRead);
    }

    /// <summary>
    /// Verifies that an HTML charset declaration for an unsupported
    /// encoding is rejected even when the source bytes are valid UTF-8.
    /// </summary>
    [Theory]
    [InlineData("<meta charset=\"windows-1252\"><p>Content</p>")]
    [InlineData("<meta http-equiv=\"content-type\" content=\"text/html; charset=iso-8859-1\"><p>Content</p>")]
    public async Task BuildAsync_WhenMarkupDeclaresUnsupportedEncoding_RejectsContent(
        string markup)
    {
        // Arrange
        using var sourceStream =
            new NonSeekableReadStream(
                Encoding.UTF8.GetBytes(markup));

        // Act
        InvalidDataException exception =
            await Assert.ThrowsAsync<InvalidDataException>(
                () => HtmlDocumentContentPayloadBuilder.BuildAsync(
                    sourceStream,
                    TestContext.Current.CancellationToken));

        // Assert
        Assert.Contains("unsupported character encoding", exception.Message);
        Assert.True(sourceStream.CanRead);
    }

    /// <summary>
    /// Verifies that markup containing an external resource reference
    /// is rejected by the existing embedded-resource policy.
    /// </summary>
    [Fact]
    public async Task BuildAsync_WhenMarkupContainsExternalResource_RejectsContent()
    {
        // Arrange
        const string markup =
            """
            <html>
            <body>
                <img src="https://example.invalid/image.png" />
            </body>
            </html>
            """;

        using var sourceStream =
            new NonSeekableReadStream(
                Encoding.UTF8.GetBytes(markup));

        // Act
        InvalidDataException exception =
            await Assert.ThrowsAsync<InvalidDataException>(
                () => HtmlDocumentContentPayloadBuilder.BuildAsync(
                    sourceStream,
                    TestContext.Current.CancellationToken));

        // Assert
        Assert.Contains("not permitted", exception.Message);
        Assert.True(sourceStream.CanRead);
    }

    /// <summary>
    /// Verifies that cancellation is observed before source content
    /// is consumed.
    /// </summary>
    [Fact]
    public async Task BuildAsync_WhenCancellationIsRequested_StopsReading()
    {
        // Arrange
        using var sourceStream =
            new NonSeekableReadStream(
                Encoding.UTF8.GetBytes("<p>Content</p>"));

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        // Act
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => HtmlDocumentContentPayloadBuilder.BuildAsync(
                sourceStream,
                cancellationTokenSource.Token));

        // Assert
        Assert.Equal(0, sourceStream.BytesRead);
        Assert.True(sourceStream.CanRead);
    }

    /// <summary>
    /// Provides readable test data without exposing seek or length support.
    /// </summary>
    private sealed class NonSeekableReadStream : Stream
    {
        private readonly byte[] _sourceBytes;

        private int _position;

        private bool _disposed;

        /// <summary>
        /// Initializes a non-seekable stream over the supplied bytes.
        /// </summary>
        internal NonSeekableReadStream(
            byte[] sourceBytes)
        {
            ArgumentNullException.ThrowIfNull(sourceBytes);

            _sourceBytes =
                sourceBytes;
        }

        /// <summary>
        /// Gets the number of source bytes consumed by read operations.
        /// </summary>
        internal int BytesRead { get; private set; }

        public override bool CanRead =>
            !_disposed;

        public override bool CanSeek =>
            false;

        public override bool CanWrite =>
            false;

        public override long Length =>
            throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(
            byte[] buffer,
            int offset,
            int count)
        {
            ObjectDisposedException.ThrowIf(
                _disposed,
                this);

            ArgumentNullException.ThrowIfNull(buffer);
            ArgumentOutOfRangeException.ThrowIfNegative(offset);
            ArgumentOutOfRangeException.ThrowIfNegative(count);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(
                offset + count,
                buffer.Length);

            int bytesToRead =
                Math.Min(
                    count,
                    _sourceBytes.Length - _position);

            _sourceBytes.AsSpan(
                    _position,
                    bytesToRead)
                .CopyTo(
                    buffer.AsSpan(
                        offset,
                        bytesToRead));

            _position += bytesToRead;
            BytesRead += bytesToRead;

            return bytesToRead;
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ObjectDisposedException.ThrowIf(
                _disposed,
                this);

            int bytesToRead =
                Math.Min(
                    buffer.Length,
                    _sourceBytes.Length - _position);

            _sourceBytes.AsMemory(
                    _position,
                    bytesToRead)
                .CopyTo(buffer);

            _position += bytesToRead;
            BytesRead += bytesToRead;

            return ValueTask.FromResult(bytesToRead);
        }

        public override long Seek(
            long offset,
            SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(
            long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(
            byte[] buffer,
            int offset,
            int count)
        {
            throw new NotSupportedException();
        }

        protected override void Dispose(
            bool disposing)
        {
            _disposed = true;

            base.Dispose(disposing);
        }
    }
}
