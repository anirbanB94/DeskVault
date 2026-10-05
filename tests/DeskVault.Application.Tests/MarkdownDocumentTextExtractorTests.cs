using DeskVault.Application.Configurations;
using DeskVault.Application.Documents.Extraction;
using DeskVault.Application.Documents.Extraction.MarkdownDocument;
using DeskVault.Application.Documents.Processing;
using DeskVault.Application.Documents.Provenance;
using DeskVault.Application.Tests.TestInfrastructure;
using System.Text;

namespace DeskVault.Application.Tests;

public sealed class MarkdownDocumentTextExtractorTests
{
    private readonly MarkdownDocumentTextExtractor _extractor = new();

    [Fact]
    public void RuleVersion_ReturnsStableMarkdownExtractorVersion()
    {
        // Arrange
        const string expectedVersion =
            "markdown-extractor-v1";

        MarkdownDocumentTextExtractor extractor =
            new();

        // Act
        string actualVersion =
            extractor.RuleVersion;

        // Assert
        Assert.Equal(
            expectedVersion,
            actualVersion);
    }

    [Fact]
    public void CanExtract_MarkdownFile_ReturnsTrue()
    {
        Assert.True(
            _extractor.CanExtract(
                "README.md"));
    }

    [Theory]
    [InlineData("README.MD")]
    [InlineData("README.Md")]
    [InlineData("README.mD")]
    public void CanExtract_MarkdownExtension_IsCaseInsensitive(
        string fileName)
    {
        Assert.True(
            _extractor.CanExtract(
                fileName));
    }

    [Theory]
    [InlineData("document.txt")]
    [InlineData("document.csv")]
    [InlineData("document.pdf")]
    public void CanExtract_NonMarkdownFile_ReturnsFalse(
        string fileName)
    {
        Assert.False(
            _extractor.CanExtract(
                fileName));
    }

    [Fact]
    public async Task ExtractAsync_ReturnsMarkdownSourceText()
    {
        const string markdown =
            "# DeskVault\n\n" +
            "This is **important**.\n\n" +
            "- One\n" +
            "- Two";

        using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(
                    markdown));

        DocumentTextExtractionResult result =
            await _extractor.ExtractAsync(
                stream,
                "README.md");

        Assert.Equal(
            markdown,
            result.Text);
    }

    [Fact]
    public async Task ExtractAsync_PreservesMarkdownSyntax()
    {
        const string markdown =
            "## Heading\n\n" +
            "[link](https://example.com)\n\n" +
            "`code`";

        using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(
                    markdown));

        DocumentTextExtractionResult result =
            await _extractor.ExtractAsync(
                stream,
                "document.md");

        Assert.Equal(
            markdown,
            result.Text);
    }

    [Fact]
    public async Task ExtractAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(
                    "# Test"));

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                _extractor.ExtractAsync(
                    stream,
                    "document.md",
                    cancellationTokenSource.Token));
    }

    [Fact]
    public async Task ExtractAsync_LeavesInputStreamOpen()
    {
        const string markdown =
            "# DeskVault\n\n" +
            "Stream lifetime test.";

        await using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(
                    markdown));

        await _extractor.ExtractAsync(
            stream,
            "document.md");

        Assert.True(
            stream.CanRead);
    }

    [Fact]
    public async Task ExtractAsync_InputStreamReadFailure_PropagatesException()
    {
        using var stream =
            new ThrowingReadStream();

        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () =>
                    _extractor.ExtractAsync(
                        stream,
                        "document.md"));

        Assert.Equal(
            "Simulated document read failure.",
            exception.Message);
    }

    [Fact]
    public async Task ExtractAsync_WhenProcessedTextExceedsLimit_ThrowsResourceLimitExceededException()
    {
        var extractor =
            new MarkdownDocumentTextExtractor(
                new DocumentProcessingOptions
                {
                    MaxProcessedTextBytes = 5
                });

        await using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes("123456"));

        ResourceLimitExceededException exception =
            await Assert.ThrowsAsync<ResourceLimitExceededException>(
                () =>
                    extractor.ExtractAsync(
                        stream,
                        "document.md"));

        Assert.Equal(
            5,
            exception.LimitBytes);

        Assert.Equal(
            6,
            exception.AttemptedBytes);
    }

    [Fact]
    public async Task ExtractAsync_WhenProcessedTextIsWithinLimit_ReturnsCompleteMarkdown()
    {
        const string expectedMarkdown =
            "# DeskVault\n\nBounded markdown extraction.";

        var extractor =
            new MarkdownDocumentTextExtractor(
                new DocumentProcessingOptions
                {
                    MaxProcessedTextBytes =
                        Encoding.UTF8.GetByteCount(expectedMarkdown)
                });

        await using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(expectedMarkdown));

        DocumentTextExtractionResult result =
            await extractor.ExtractAsync(
                stream,
                "document.md");

        Assert.Equal(
            expectedMarkdown,
            result.Text);
    }

    [Fact]
    public async Task ExtractAsync_RawMarkdown_ReturnsDirectSourceLocationMapping()
    {
        const string markdown =
            "# DeskVault\n\n" +
            "Second paragraph.";

        await using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(
                    markdown));

        DocumentTextExtractionResult result =
            await _extractor.ExtractAsync(
                stream,
                "README.md");

        Assert.Equal(
            DocumentSourceLocationMappingKind.DirectText,
            result.SourceLocationMappingKind);
    }
}
