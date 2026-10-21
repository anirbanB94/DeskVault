using System.Text;
using DeskVault.Application.Configurations;
using DeskVault.Application.Documents.Extraction;
using DeskVault.Application.Documents.Extraction.ConfigDocument;
using DeskVault.Application.Documents.Processing;

namespace DeskVault.Application.Tests;

public sealed class ConfigDocumentTextExtractorTests
{
    [Fact]
    public void RuleVersion_IncludesDispatchAndDelegateVersions()
    {
        ConfigDocumentTextExtractor extractor = new();

        Assert.Equal(
            "config-content-dispatch-v1" +
            "|xml=xml-extractor-v1" +
            "|ini=ini-extractor-v1",
            extractor.RuleVersion);
    }

    [Theory]
    [InlineData("document.config")]
    [InlineData("document.CONFIG")]
    [InlineData("document.Config")]
    public void CanExtract_WhenFileIsConfig_ReturnsTrue(
        string fileName)
    {
        ConfigDocumentTextExtractor extractor = new();

        Assert.True(
            extractor.CanExtract(fileName));
    }

    [Theory]
    [InlineData("document.ini")]
    [InlineData("document.xml")]
    [InlineData("document.txt")]
    [InlineData("document.config.bak")]
    public void CanExtract_WhenFileIsNotConfig_ReturnsFalse(
        string fileName)
    {
        ConfigDocumentTextExtractor extractor = new();

        Assert.False(
            extractor.CanExtract(fileName));
    }

    [Fact]
    public async Task ExtractAsync_WhenConfigContainsXml_UsesXmlSemantics()
    {
        const string config =
            """
            <?xml version="1.0"?>
            <configuration>
              <appSettings>
                <add key="Mode" value="Production" />
              </appSettings>
            </configuration>
            """;

        ConfigDocumentTextExtractor extractor = new();

        DocumentTextExtractionResult result =
            await ExtractAsync(
                extractor,
                config,
                TestContext.Current.CancellationToken);

        Assert.Contains("configuration", result.Text);
        Assert.Contains("Mode", result.Text);
        Assert.Contains("Production", result.Text);
    }

    [Fact]
    public async Task ExtractAsync_WhenConfigContainsIni_UsesIniSemantics()
    {
        const string config =
            """
            [appSettings]
            Mode=Production
            """;

        ConfigDocumentTextExtractor extractor = new();

        DocumentTextExtractionResult result =
            await ExtractAsync(
                extractor,
                config,
                TestContext.Current.CancellationToken);

        Assert.Contains("[appSettings]", result.Text);
        Assert.Contains("Mode: Production", result.Text);
    }

    [Fact]
    public async Task ExtractAsync_WhenConfigContainsOnlyIniComments_UsesIniSemantics()
    {
        const string config =
            """
            ; Environment-specific values are intentionally absent.
            ; Defaults are used for this document.
            """;

        ConfigDocumentTextExtractor extractor = new();

        DocumentTextExtractionResult result =
            await ExtractAsync(
                extractor,
                config,
                TestContext.Current.CancellationToken);

        Assert.Equal(
            string.Empty,
            result.Text);
    }

    [Fact]
    public async Task ExtractAsync_WhenXmlConfigHasBomAndLeadingWhitespace_UsesXmlSemantics()
    {
        const string config =
            " \r\n\t<configuration>" +
            "<environment>Production</environment>" +
            "</configuration>";

        byte[] preamble =
            Encoding.UTF8.GetPreamble();

        byte[] content =
            Encoding.UTF8.GetBytes(config);

        byte[] bytes =
            new byte[preamble.Length + content.Length];

        Buffer.BlockCopy(
            preamble,
            0,
            bytes,
            0,
            preamble.Length);

        Buffer.BlockCopy(
            content,
            0,
            bytes,
            preamble.Length,
            content.Length);

        ConfigDocumentTextExtractor extractor = new();

        DocumentTextExtractionResult result =
            await ExtractBytesAsync(
                extractor,
                bytes,
                TestContext.Current.CancellationToken);

        Assert.Contains(
            "Production",
            result.Text);
    }

    [Fact]
    public async Task ExtractAsync_WhenConfigContainsLeadingXmlComment_UsesXmlSemantics()
    {
        const string config =
            """
            <!-- Configuration metadata -->
            <configuration>
              <environment>Production</environment>
            </configuration>
            """;

        ConfigDocumentTextExtractor extractor = new();

        DocumentTextExtractionResult result =
            await ExtractAsync(
                extractor,
                config,
                TestContext.Current.CancellationToken);

        Assert.Contains(
            "Production",
            result.Text);
    }

    [Theory]
    [InlineData(
        "<configuration><appSettings></configuration>")]
    public async Task ExtractAsync_WhenXmlConfigIsMalformed_ThrowsXmlException(
        string config)
    {
        ConfigDocumentTextExtractor extractor = new();

        Task<DocumentTextExtractionResult> extractionTask =
            ExtractAsync(
                extractor,
                config,
                TestContext.Current.CancellationToken);

        await Assert.ThrowsAnyAsync<System.Xml.XmlException>(
            () => extractionTask);
    }

    [Fact]
    public async Task ExtractAsync_WhenIniConfigIsMalformed_ThrowsParsingException()
    {
        const string config =
            """
            [database
            provider=SQLite
            """;

        ConfigDocumentTextExtractor extractor = new();

        Task<DocumentTextExtractionResult> extractionTask =
            ExtractAsync(
                extractor,
                config,
                TestContext.Current.CancellationToken);

        await Assert.ThrowsAnyAsync<IniParser.Exceptions.ParsingException>(
            () => extractionTask);
    }

    [Theory]
    [InlineData("This content is neither XML nor INI.")]
    [InlineData("")]
    [InlineData(" \r\n \t")]
    [InlineData("# configuration comment")]
    public async Task ExtractAsync_WhenConfigSyntaxIsUnrecognized_ThrowsInvalidDataException(
        string config)
    {
        ConfigDocumentTextExtractor extractor = new();

        Task<DocumentTextExtractionResult> extractionTask =
            ExtractAsync(
                extractor,
                config,
                TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => extractionTask);
    }

    [Fact]
    public async Task ExtractAsync_WhenSeekableConfigExceedsDecryptedDocumentLimit_ThrowsResourceLimitExceededException()
    {
        var options =
            new DocumentProcessingOptions
            {
                MaxDecryptedDocumentBytes = 4
            };

        ConfigDocumentTextExtractor extractor =
            new(options);

        Task<DocumentTextExtractionResult> extractionTask =
            ExtractAsync(
                extractor,
                "[appSettings]\nMode=Production",
                TestContext.Current.CancellationToken);

        ResourceLimitExceededException exception =
            await Assert.ThrowsAsync<ResourceLimitExceededException>(
                () => extractionTask);

        Assert.Equal(
            4,
            exception.LimitBytes);

        Assert.True(
            exception.AttemptedBytes > exception.LimitBytes);
    }

    [Fact]
    public async Task ExtractAsync_WhenNonSeekableConfigExceedsDecryptedDocumentLimit_ThrowsResourceLimitExceededException()
    {
        var options =
            new DocumentProcessingOptions
            {
                MaxDecryptedDocumentBytes = 4
            };

        ConfigDocumentTextExtractor extractor =
            new(options);

        await using var stream =
            new NonSeekableReadStream(
                new MemoryStream(
                    Encoding.UTF8.GetBytes(
                        "[appSettings]\nMode=Production")));

        Task<DocumentTextExtractionResult> extractionTask =
            extractor.ExtractAsync(
                stream,
                "document.config",
                TestContext.Current.CancellationToken);

        ResourceLimitExceededException exception =
            await Assert.ThrowsAsync<ResourceLimitExceededException>(
                () => extractionTask);

        Assert.Equal(
            4,
            exception.LimitBytes);

        Assert.True(
            exception.AttemptedBytes > exception.LimitBytes);
    }

    [Fact]
    public async Task ExtractAsync_WhenProcessedTextExceedsLimit_ThrowsResourceLimitExceededException()
    {
        var options =
            new DocumentProcessingOptions
            {
                MaxProcessedTextBytes = 1
            };

        ConfigDocumentTextExtractor extractor =
            new(options);

        Task<DocumentTextExtractionResult> extractionTask =
            ExtractAsync(
                extractor,
                """
                [appSettings]
                Mode=Production
                """,
                TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<ResourceLimitExceededException>(
            () => extractionTask);
    }

    [Fact]
    public async Task ExtractAsync_WhenCancellationRequested_ThrowsOperationCanceledException()
    {
        ConfigDocumentTextExtractor extractor = new();

        using CancellationTokenSource cancellationTokenSource =
            new();

        cancellationTokenSource.Cancel();

        Task<DocumentTextExtractionResult> extractionTask =
            ExtractAsync(
                extractor,
                "<configuration />",
                cancellationTokenSource.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => extractionTask);
    }

    [Fact]
    public async Task ExtractAsync_WhenNonSeekableStreamContainsIni_UsesIniSemantics()
    {
        const string config =
            """
            [appSettings]
            Mode=Production
            """;

        ConfigDocumentTextExtractor extractor = new();

        await using var stream =
            new NonSeekableReadStream(
                new MemoryStream(
                    Encoding.UTF8.GetBytes(config)));

        DocumentTextExtractionResult result =
            await extractor.ExtractAsync(
                stream,
                "document.config",
                TestContext.Current.CancellationToken);

        Assert.Contains(
            "[appSettings]",
            result.Text);

        Assert.Contains(
            "Mode: Production",
            result.Text);
    }

    private static async Task<DocumentTextExtractionResult> ExtractAsync(
        ConfigDocumentTextExtractor extractor,
        string config,
        CancellationToken cancellationToken = default)
    {
        await using MemoryStream stream =
            new(
                Encoding.UTF8.GetBytes(config));

        return await extractor.ExtractAsync(
            stream,
            "document.config",
            cancellationToken);
    }

    private static async Task<DocumentTextExtractionResult> ExtractBytesAsync(
        ConfigDocumentTextExtractor extractor,
        byte[] configBytes,
        CancellationToken cancellationToken = default)
    {
        await using MemoryStream stream =
            new(configBytes);

        return await extractor.ExtractAsync(
            stream,
            "document.config",
            cancellationToken);
    }

    private sealed class NonSeekableReadStream : Stream
    {
        private readonly Stream _innerStream;

        public NonSeekableReadStream(
            Stream innerStream)
        {
            ArgumentNullException.ThrowIfNull(innerStream);

            _innerStream = innerStream;
        }

        public override bool CanRead =>
            _innerStream.CanRead;

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
            return _innerStream.Read(
                buffer,
                offset,
                count);
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            return _innerStream.ReadAsync(
                buffer,
                cancellationToken);
        }

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            return _innerStream.ReadAsync(
                buffer,
                offset,
                count,
                cancellationToken);
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _innerStream.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
