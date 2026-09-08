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
    [InlineData("application.log")]
    [InlineData("program.c")]
    [InlineData("program.cpp")]
    [InlineData("header.h")]
    [InlineData("header.hpp")]
    [InlineData("application.cs")]
    [InlineData("application.java")]
    [InlineData("script.py")]
    [InlineData("application.js")]
    [InlineData("application.ts")]
    [InlineData("styles.css")]
    [InlineData("query.sql")]
    [InlineData("script.ps1")]
    public void CanExtract_SourceCodeAndLogExtensions_ReturnsTrue(
        string fileName)
    {
        bool result =
            _extractor.CanExtract(
                fileName);

        Assert.True(result);
    }

    [Theory]
    [InlineData("application.LOG")]
    [InlineData("program.CPP")]
    [InlineData("application.Cs")]
    [InlineData("script.PY")]
    [InlineData("application.TS")]
    [InlineData("query.SQL")]
    [InlineData("script.PS1")]
    public void CanExtract_SourceCodeAndLogExtensions_IsCaseInsensitive(
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
    public async Task ExtractAsync_ReadsSourceCodeAsText()
    {
        const string expectedText =
            """
            public class Example
            {
                public string GetValue()
                {
                    return "DeskVault";
                }
            }
            """;

        await using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(
                    expectedText));

        DocumentTextExtractionResult result =
            await _extractor.ExtractAsync(
                stream,
                "Example.cs");

        Assert.Equal(
            expectedText,
            result.Text);
    }

    [Fact]
    public async Task ExtractAsync_ReadsLogAsText()
    {
        const string expectedText =
            """
            2026-09-08 09:00:00 INFO Application started.
            2026-09-08 09:00:01 INFO Document processing started.
            2026-09-08 09:00:02 WARN Processing took longer than expected.
            """;

        await using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(
                    expectedText));

        DocumentTextExtractionResult result =
            await _extractor.ExtractAsync(
                stream,
                "application.log");

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
