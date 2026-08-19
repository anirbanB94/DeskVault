using System.Text;
using DeskVault.Application.Documents.Extraction;
using DeskVault.Application.Documents.Extraction.MarkdownDocument;

namespace DeskVault.Application.Tests;

public sealed class MarkdownDocumentTextExtractorTests
{
    private readonly MarkdownDocumentTextExtractor _extractor = new();

    [Fact]
    public void CanExtract_MarkdownFile_ReturnsTrue()
    {
        Assert.True(
            _extractor.CanExtract("README.md"));
    }

    [Theory]
    [InlineData("README.MD")]
    [InlineData("README.Md")]
    [InlineData("README.mD")]
    public void CanExtract_MarkdownExtension_IsCaseInsensitive(
        string fileName)
    {
        Assert.True(
            _extractor.CanExtract(fileName));
    }

    [Theory]
    [InlineData("document.txt")]
    [InlineData("document.csv")]
    [InlineData("document.pdf")]
    public void CanExtract_NonMarkdownFile_ReturnsFalse(
        string fileName)
    {
        Assert.False(
            _extractor.CanExtract(fileName));
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
                Encoding.UTF8.GetBytes(markdown));

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
                Encoding.UTF8.GetBytes(markdown));

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
                Encoding.UTF8.GetBytes("# Test"));

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _extractor.ExtractAsync(
                stream,
                "document.md",
                cancellationTokenSource.Token));
    }
}
