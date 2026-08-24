using DeskVault.Application.Documents.Extraction;
using DeskVault.Application.Documents.Extraction.MarkdownDocument;
using DeskVault.Application.Documents.Extraction.TextDocument;
using DeskVault.Application.Documents.Parsing.Csv;
using DeskVault.UI.Rendering;
using DeskVault.UI.Rendering.CsvDocumentRendering;
using DeskVault.UI.Rendering.MarkdownDocumentRendering;
using DeskVault.UI.Rendering.TextDocumentRendering;
using Microsoft.Extensions.Options;
using Moq;

namespace DeskVault.UI.Tests;

public sealed class DocumentContentRendererResolverTests
{
    [Fact]
    public void Resolve_TxtFile_ReturnsTextRenderer()
    {
        var resolver =
            CreateResolver(
                CreateTextRenderer());

        IDocumentContentRenderer renderer =
            resolver.Resolve("notes.txt");

        Assert.IsType<TextDocumentContentRenderer>(
            renderer);
    }

    [Fact]
    public void Resolve_MarkdownFile_ReturnsMarkdownRenderer()
    {
        var resolver =
            CreateResolver(
                CreateMarkdownRenderer());

        IDocumentContentRenderer renderer =
            resolver.Resolve("README.md");

        Assert.IsType<MarkdownDocumentContentRenderer>(
            renderer);
    }

    [Fact]
    public void Resolve_CsvFile_ReturnsCsvRenderer()
    {
        var resolver =
            CreateResolver(
                CreateCsvRenderer());

        IDocumentContentRenderer renderer =
            resolver.Resolve("data.csv");

        Assert.IsType<CsvDocumentContentRenderer>(
            renderer);
    }

    [Theory]
    [InlineData("notes.TXT")]
    [InlineData("notes.Txt")]
    [InlineData("notes.tXt")]
    public void Resolve_TxtExtension_IsCaseInsensitive(
        string fileName)
    {
        var resolver =
            CreateResolver(
                CreateTextRenderer());

        IDocumentContentRenderer renderer =
            resolver.Resolve(fileName);

        Assert.IsType<TextDocumentContentRenderer>(
            renderer);
    }

    [Theory]
    [InlineData("README.MD")]
    [InlineData("README.Md")]
    [InlineData("README.mD")]
    public void Resolve_MarkdownExtension_IsCaseInsensitive(
        string fileName)
    {
        var resolver =
            CreateResolver(
                CreateMarkdownRenderer());

        IDocumentContentRenderer renderer =
            resolver.Resolve(fileName);

        Assert.IsType<MarkdownDocumentContentRenderer>(
            renderer);
    }

    [Theory]
    [InlineData("data.CSV")]
    [InlineData("data.Csv")]
    [InlineData("data.cSv")]
    public void Resolve_CsvExtension_IsCaseInsensitive(
        string fileName)
    {
        var resolver =
            CreateResolver(
                CreateCsvRenderer());

        IDocumentContentRenderer renderer =
            resolver.Resolve(fileName);

        Assert.IsType<CsvDocumentContentRenderer>(
            renderer);
    }

    [Theory]
    [InlineData("document.pdf")]
    [InlineData("document.docx")]
    [InlineData("document.xlsx")]
    [InlineData("document.xyz")]
    public void Resolve_UnsupportedExtension_ThrowsNotSupportedException(
        string fileName)
    {
        var resolver =
            CreateResolver(
                CreateTextRenderer(),
                CreateMarkdownRenderer(),
                CreateCsvRenderer());

        NotSupportedException exception =
            Assert.Throws<NotSupportedException>(
                () => resolver.Resolve(fileName));

        Assert.Equal(
            $"No document renderer is available for '{fileName}'.",
            exception.Message);
    }

    [Fact]
    public void Resolve_EmptyFileName_ThrowsNotSupportedException()
    {
        var resolver =
            CreateResolver(
                CreateTextRenderer(),
                CreateMarkdownRenderer(),
                CreateCsvRenderer());

        NotSupportedException exception =
            Assert.Throws<NotSupportedException>(
                () => resolver.Resolve(string.Empty));

        Assert.Equal(
            "No document renderer is available for ''.",
            exception.Message);
    }

    [Fact]
    public void Resolve_WhenMultipleRenderersCanRender_SelectsHighestPriority()
    {
        var lowPriorityRenderer =
            new Mock<IDocumentContentRenderer>();

        lowPriorityRenderer
            .SetupGet(x => x.Priority)
            .Returns(0);

        lowPriorityRenderer
            .Setup(x => x.CanRender("document.test"))
            .Returns(true);

        var highPriorityRenderer =
            new Mock<IDocumentContentRenderer>();

        highPriorityRenderer
            .SetupGet(x => x.Priority)
            .Returns(10);

        highPriorityRenderer
            .Setup(x => x.CanRender("document.test"))
            .Returns(true);

        var resolver =
            CreateResolver(
                lowPriorityRenderer.Object,
                highPriorityRenderer.Object);

        IDocumentContentRenderer renderer =
            resolver.Resolve("document.test");

        Assert.Same(
            highPriorityRenderer.Object,
            renderer);
    }

    private static DocumentContentRendererResolver CreateResolver(
        params IDocumentContentRenderer[] renderers)
    {
        return new DocumentContentRendererResolver(
            renderers);
    }

    private static TextDocumentContentRenderer CreateTextRenderer()
    {
        return new TextDocumentContentRenderer(
            CreateTextExtractorResolver());
    }

    private static MarkdownDocumentContentRenderer CreateMarkdownRenderer()
    {
        return new MarkdownDocumentContentRenderer(
            Options.Create(
                new MarkdownRenderingOptions()),
            CreateTextExtractorResolver());
    }

    private static CsvDocumentContentRenderer CreateCsvRenderer()
    {
        return new CsvDocumentContentRenderer(
            new CsvDocumentParser(
                new CsvParsingOptions()));
    }

    private static DocumentTextExtractorResolver CreateTextExtractorResolver()
    {
        return new DocumentTextExtractorResolver(
            [
                new TextDocumentTextExtractor(),
                new MarkdownDocumentTextExtractor()
            ]);
    }
}
