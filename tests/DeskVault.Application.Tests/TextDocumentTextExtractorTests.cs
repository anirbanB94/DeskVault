using DeskVault.Application.Documents.Extraction.TextDocument;
using DeskVault.Application.Tests.TestInfrastructure;
using System.Text;

namespace DeskVault.Application.Tests;

public sealed class TextDocumentTextExtractorTests
{
    [Fact]
    public void CanExtract_TxtFile_ReturnsTrue()
    {
        var extractor =
            new TextDocumentTextExtractor();

        bool result =
            extractor.CanExtract("notes.txt");

        Assert.True(result);
    }

    [Theory]
    [InlineData("notes.TXT")]
    [InlineData("notes.Txt")]
    [InlineData("notes.tXt")]
    public void CanExtract_TxtExtension_IsCaseInsensitive(
        string fileName)
    {
        var extractor =
            new TextDocumentTextExtractor();

        bool result =
            extractor.CanExtract(fileName);

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
        var extractor =
            new TextDocumentTextExtractor();

        bool result =
            extractor.CanExtract(fileName);

        Assert.False(result);
    }

    [Fact]
    public async Task ExtractAsync_ReadsTextFromStream()
    {
        const string expectedText =
            "DeskVault text extraction test.";

        await using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(expectedText));

        var extractor =
            new TextDocumentTextExtractor();

        var result =
            await extractor.ExtractAsync(
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
                Encoding.UTF8.GetBytes(expectedText));

        var extractor =
            new TextDocumentTextExtractor();

        var result =
            await extractor.ExtractAsync(
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
                Encoding.UTF8.GetBytes(text));

        var extractor =
            new TextDocumentTextExtractor();

        await extractor.ExtractAsync(
            stream,
            "notes.txt");

        Assert.True(stream.CanRead);
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

        var extractor =
            new TextDocumentTextExtractor();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                extractor.ExtractAsync(
                    stream,
                    "notes.txt",
                    cancellationTokenSource.Token));
    }

    [Fact]
    public async Task ExtractAsync_InputStreamReadFailure_PropagatesException()
    {
        using var stream =
            new ThrowingReadStream();

        var extractor =
            new TextDocumentTextExtractor();

        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () =>
                    extractor.ExtractAsync(
                        stream,
                        "notes.txt"));

        Assert.Equal(
            "Simulated document read failure.",
            exception.Message);
    }
}
