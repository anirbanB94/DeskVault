using DeskVault.Application.Configurations;
using DeskVault.Application.Documents;
using DeskVault.Application.Documents.Extraction;
using DeskVault.Application.Documents.Extraction.ConfigDocument;
using DeskVault.Application.Documents.Extraction.CSVDocument;
using DeskVault.Application.Documents.Extraction.IniDocument;
using DeskVault.Application.Documents.Extraction.JsonDocument;
using DeskVault.Application.Documents.Extraction.MarkdownDocument;
using DeskVault.Application.Documents.Extraction.TextDocument;
using DeskVault.Application.Documents.Extraction.XmlDocument;
using DeskVault.Application.Documents.Extraction.YamlDocument;
using Microsoft.Extensions.DependencyInjection;

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
                new CsvDocumentTextExtractor(),
                new JsonDocumentTextExtractor(),
                new ConfigDocumentTextExtractor(),
                new IniDocumentTextExtractor(),
                new XmlDocumentTextExtractor()
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

    [Theory]
    [InlineData("application.cs")]
    [InlineData("application.log")]
    public void Resolve_SourceCodeAndLogFiles_ReturnsTextExtractor(
        string fileName)
    {
        IDocumentTextExtractor extractor =
            CreateResolver().Resolve(fileName);

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

    [Fact]
    public void Resolve_JsonFile_ReturnsJsonExtractor()
    {
        IDocumentTextExtractor extractor =
            CreateResolver().Resolve("document.json");

        Assert.IsType<JsonDocumentTextExtractor>(
            extractor);
    }

    [Fact]
    public void Resolve_XmlFile_ReturnsXmlExtractor()
    {
        IDocumentTextExtractor extractor =
            CreateResolver().Resolve("document.xml");

        Assert.IsType<XmlDocumentTextExtractor>(
            extractor);
    }

    [Theory]
    [InlineData("document.config")]
    [InlineData("document.CONFIG")]
    [InlineData("document.Config")]
    public void Resolve_ConfigFile_ReturnsConfigExtractor(
        string fileName)
    {
        IDocumentTextExtractor extractor =
            CreateResolver().Resolve(fileName);

        Assert.IsType<ConfigDocumentTextExtractor>(
            extractor);
    }

    [Theory]
    [InlineData("document.ini")]
    [InlineData("document.INI")]
    public void Resolve_IniFile_ReturnsIniExtractor(
        string fileName)
    {
        IDocumentTextExtractor extractor =
            CreateResolver().Resolve(fileName);

        Assert.IsType<IniDocumentTextExtractor>(
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

    [Theory]
    [InlineData("document.JSON")]
    [InlineData("document.Json")]
    [InlineData("document.jSoN")]
    public void Resolve_JsonExtension_IsCaseInsensitive(
        string fileName)
    {
        IDocumentTextExtractor extractor =
            CreateResolver().Resolve(fileName);

        Assert.IsType<JsonDocumentTextExtractor>(
            extractor);
    }

    [Theory]
    [InlineData("document.XML")]
    [InlineData("document.Xml")]
    [InlineData("document.xMl")]
    public void Resolve_XmlExtension_IsCaseInsensitive(
        string fileName)
    {
        IDocumentTextExtractor extractor =
            CreateResolver().Resolve(fileName);

        Assert.IsType<XmlDocumentTextExtractor>(
            extractor);
    }

    [Theory]
    [InlineData("application.CS")]
    [InlineData("application.LoG")]
    [InlineData("script.PY")]
    public void Resolve_SourceCodeAndLogExtensions_IsCaseInsensitive(
        string fileName)
    {
        IDocumentTextExtractor extractor =
            CreateResolver().Resolve(fileName);

        Assert.IsType<TextDocumentTextExtractor>(
            extractor);
    }

    [Fact]
    public void RegisteredExtractors_MatchDeclaredExtractionCapabilities()
    {
        var services = new ServiceCollection();

        services.AddSingleton(new DocumentProcessingOptions());
        services.AddApplication();

        using ServiceProvider provider =
            services.BuildServiceProvider();

        DocumentTextExtractorResolver resolver =
            provider.GetRequiredService<DocumentTextExtractorResolver>();

        foreach (string extension in SupportedFileTypes.GetAll())
        {
            DocumentFormatCapability capability =
                SupportedFileTypes.GetForExtension(extension)
                ?? throw new InvalidOperationException(
                    $"No capability declaration exists for '{extension}'.");

            string fileName = $"document{extension}";
            string uppercaseFileName =
                $"document{extension.ToUpperInvariant()}";

            if (capability.TextExtraction
                == DocumentTextExtractionKind.None)
            {
                Assert.False(
                    resolver.CanResolve(fileName),
                    $"'{extension}' declares no extractor but one can resolve.");

                Assert.False(
                    resolver.CanResolve(uppercaseFileName),
                    $"Uppercase '{extension}' unexpectedly resolves an extractor.");

                continue;
            }

            Type expectedExtractorType =
                GetExpectedExtractorType(capability.TextExtraction);

            Assert.True(
                resolver.CanResolve(fileName),
                $"'{extension}' declares extraction but no extractor resolves.");

            Assert.Equal(
                expectedExtractorType,
                resolver.Resolve(fileName).GetType());

            Assert.True(
                resolver.CanResolve(uppercaseFileName),
                $"Uppercase '{extension}' should resolve an extractor.");

            Assert.Equal(
                expectedExtractorType,
                resolver.Resolve(uppercaseFileName).GetType());
        }
    }

    private static Type GetExpectedExtractorType(
        DocumentTextExtractionKind extractionKind)
    {
        return extractionKind switch
        {
            DocumentTextExtractionKind.PlainText =>
                typeof(TextDocumentTextExtractor),

            DocumentTextExtractionKind.Markdown =>
                typeof(MarkdownDocumentTextExtractor),

            DocumentTextExtractionKind.Csv =>
                typeof(CsvDocumentTextExtractor),

            DocumentTextExtractionKind.Json =>
                typeof(JsonDocumentTextExtractor),

            DocumentTextExtractionKind.Xml =>
                typeof(XmlDocumentTextExtractor),

            DocumentTextExtractionKind.Yaml =>
                typeof(YamlDocumentTextExtractor),

            DocumentTextExtractionKind.Ini =>
                typeof(IniDocumentTextExtractor),

            DocumentTextExtractionKind.Config =>
                typeof(ConfigDocumentTextExtractor),

            _ => throw new ArgumentOutOfRangeException(
                nameof(extractionKind),
                extractionKind,
                "The extraction kind does not identify an extractor.")
        };
    }
}
