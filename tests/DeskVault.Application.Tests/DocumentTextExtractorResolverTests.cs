using DeskVault.Application.Documents.Extraction;
using DeskVault.Application.Documents.Extraction.CSVDocument;
using DeskVault.Application.Documents.Extraction.MarkdownDocument;
using DeskVault.Application.Documents.Extraction.TextDocument;

namespace DeskVault.Application.Tests;

public sealed class DocumentTextExtractorResolverTests
{
    private static DocumentTextExtractorResolver CreateResolver()
    {
        return new DocumentTextExtractorResolver(
            new IDocumentTextExtractor[]
            {
                new TextDocumentTextExtractor(),
                new MarkdownDocumentTextExtractor(),
                new CsvDocumentTextExtractor()
            });
    }

    [Fact]
    public void Resolve_TxtFile_ReturnsTextExtractor()
    {
        IDocumentTextExtractor extractor =
            CreateResolver().Resolve("notes.txt");

        Assert.IsType<TextDocumentTextExtractor>(
            extractor);
    }

    [Fact]
    public void Resolve_MarkdownFile_ReturnsMarkdownExtractor()
    {
        IDocumentTextExtractor extractor =
            CreateResolver().Resolve("README.md");

        Assert.IsType<MarkdownDocumentTextExtractor>(
            extractor);
    }

    [Fact]
    public void Resolve_CsvFile_ReturnsCsvExtractor()
    {
        IDocumentTextExtractor extractor =
            CreateResolver().Resolve("data.csv");

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
        NotSupportedException exception =
            Assert.Throws<NotSupportedException>(
                () => CreateResolver().Resolve(fileName));

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
        IDocumentTextExtractor extractor =
            CreateResolver().Resolve(fileName);

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
        IDocumentTextExtractor extractor =
            CreateResolver().Resolve(fileName);

        Assert.IsType<CsvDocumentTextExtractor>(
            extractor);
    }
}
