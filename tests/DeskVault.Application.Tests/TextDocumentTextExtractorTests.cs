using DeskVault.Application.Documents.Extraction;
using DeskVault.Application.Documents.Extraction.TextDocument;
using DeskVault.Application.Tests.TestInfrastructure;
using System.Text;

namespace DeskVault.Application.Tests;

public sealed class TextDocumentTextExtractorTests
{
    private readonly TextDocumentTextExtractor _extractor = new();

    [Fact]
    public void CanExtract_TxtFile_ReturnsTrue()
    {
        bool result =
            _extractor.CanExtract(
                "notes.txt");

        Assert.True(result);
    }

    [Theory]
    [InlineData("notes.TXT")]
    [InlineData("notes.Txt")]
    [InlineData("notes.tXt")]
    public void CanExtract_TxtExtension_IsCaseInsensitive(
        string fileName)
    {
        bool result =
            _extractor.CanExtract(
                fileName);

        Assert.True(result);
    }

    [Theory]
    [InlineData("document.pdf")]
    [InlineData("document.md")]
    [InlineData("document.csv")]
    [InlineData("document.docx")]
    public void CanExtract_UnsupportedExtension_ReturnsFalse(
        string fileName)
    {
        bool result =
            _extractor.CanExtract(
                fileName);

        Assert.False(result);
    }

    [Fact]
    public async Task ExtractAsync_ReadsTextFromStream()
    {
        const string expectedText =
            "DeskVault text extraction test.";

        await using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(
                    expectedText));

        DocumentTextExtractionResult result =
            await _extractor.ExtractAsync(
                stream,
                "notes.txt");

        Assert.Equal(
            expectedText,
            result.Text);
    }

    [Fact]
    public async Task ExtractAsync_PreservesMultilineText()
    {
        const string expectedText =
            """
            First line.
            Second line.

            Fourth line.
            """;

        await using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(
                    expectedText));

        DocumentTextExtractionResult result =
            await _extractor.ExtractAsync(
                stream,
                "notes.txt");

        Assert.Equal(
            expectedText,
            result.Text);
    }

    [Fact]
    public async Task ExtractAsync_LeavesInputStreamOpen()
    {
        const string text =
            "Stream lifetime test.";

        await using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(
                    text));

        await _extractor.ExtractAsync(
            stream,
            "notes.txt");

        Assert.True(
            stream.CanRead);
    }

    [Fact]
    public async Task ExtractAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        await using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(
                    "Cancellation test."));

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                _extractor.ExtractAsync(
                    stream,
                    "notes.txt",
                    cancellationTokenSource.Token));
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
                        "notes.txt"));

        Assert.Equal(
            "Simulated document read failure.",
            exception.Message);
    }
}
