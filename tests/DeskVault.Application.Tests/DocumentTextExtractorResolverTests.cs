using DeskVault.Application.Documents.Extraction;
using DeskVault.Application.Documents.Extraction.CSVDocument;
using DeskVault.Application.Documents.Extraction.MarkdownDocument;
using DeskVault.Application.Documents.Extraction.TextDocument;

namespace DeskVault.Application.Tests;

public sealed class DocumentTextExtractorResolverTests
{
    [Fact]
    public void Resolve_TxtFile_ReturnsTextExtractor()
    {
        var resolver =
            new DocumentTextExtractorResolver(
                new IDocumentTextExtractor[]
                {
                    new TextDocumentTextExtractor(),
                    new MarkdownDocumentTextExtractor(),
                    new CsvDocumentTextExtractor()
                });

        IDocumentTextExtractor extractor =
            resolver.Resolve("notes.txt");

        Assert.IsType<TextDocumentTextExtractor>(
            extractor);
    }

    [Fact]
    public void Resolve_MarkdownFile_ReturnsMarkdownExtractor()
    {
        var resolver =
            new DocumentTextExtractorResolver(
                new IDocumentTextExtractor[]
                {
                    new TextDocumentTextExtractor(),
                    new MarkdownDocumentTextExtractor(),
                    new CsvDocumentTextExtractor()
                });

        IDocumentTextExtractor extractor =
            resolver.Resolve("README.md");

        Assert.IsType<MarkdownDocumentTextExtractor>(
            extractor);
    }

    [Fact]
    public void Resolve_CsvFile_ReturnsCsvExtractor()
    {
        var resolver =
            new DocumentTextExtractorResolver(
                new IDocumentTextExtractor[]
                {
                    new TextDocumentTextExtractor(),
                    new MarkdownDocumentTextExtractor(),
                    new CsvDocumentTextExtractor()
                });

        IDocumentTextExtractor extractor =
            resolver.Resolve("data.csv");

        Assert.IsType<CsvDocumentTextExtractor>(
            extractor);
    }

    [Theory]
    [InlineData("document.pdf")]
    [InlineData("document.docx")]
    [InlineData("document.xyz")]
    public void Resolve_UnsupportedFile_ThrowsNotSupportedException(
        string fileName)
    {
        var resolver =
            new DocumentTextExtractorResolver(
                new IDocumentTextExtractor[]
                {
                    new TextDocumentTextExtractor(),
                    new MarkdownDocumentTextExtractor(),
                    new CsvDocumentTextExtractor()
                });

        NotSupportedException exception =
            Assert.Throws<NotSupportedException>(
                () => resolver.Resolve(fileName));

        Assert.Equal(
            $"No document text extractor is available for '{fileName}'.",
            exception.Message);
    }

    [Theory]
    [InlineData("README.MD")]
    [InlineData("README.Md")]
    [InlineData("README.mD")]
    public void Resolve_MarkdownExtension_IsCaseInsensitive(
        string fileName)
    {
        var resolver =
            new DocumentTextExtractorResolver(
                new IDocumentTextExtractor[]
                {
                    new TextDocumentTextExtractor(),
                    new MarkdownDocumentTextExtractor(),
                    new CsvDocumentTextExtractor()
                });

        IDocumentTextExtractor extractor =
            resolver.Resolve(fileName);

        Assert.IsType<MarkdownDocumentTextExtractor>(
            extractor);
    }

    [Theory]
    [InlineData("data.CSV")]
    [InlineData("data.Csv")]
    [InlineData("data.cSv")]
    public void Resolve_CsvExtension_IsCaseInsensitive(
        string fileName)
    {
        var resolver =
            new DocumentTextExtractorResolver(
                new IDocumentTextExtractor[]
                {
                    new TextDocumentTextExtractor(),
                    new MarkdownDocumentTextExtractor(),
                    new CsvDocumentTextExtractor()
                });

        IDocumentTextExtractor extractor =
            resolver.Resolve(fileName);

        Assert.IsType<CsvDocumentTextExtractor>(
            extractor);
    }
}
