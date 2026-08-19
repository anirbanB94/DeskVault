using DeskVault.Application.Documents.Extraction;
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
                    new MarkdownDocumentTextExtractor()
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
                    new MarkdownDocumentTextExtractor()
                });

        IDocumentTextExtractor extractor =
            resolver.Resolve("README.md");

        Assert.IsType<MarkdownDocumentTextExtractor>(
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
                    new MarkdownDocumentTextExtractor()
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
                    new MarkdownDocumentTextExtractor()
                });

        IDocumentTextExtractor extractor =
            resolver.Resolve(fileName);

        Assert.IsType<MarkdownDocumentTextExtractor>(
            extractor);
    }
}
