using DeskVault.Application.Documents.Chunking;
using DeskVault.Application.Documents.Extraction;
using DeskVault.Application.Documents.Extraction.CSVDocument;
using DeskVault.Application.Documents.Extraction.JsonDocument;
using DeskVault.Application.Documents.Extraction.MarkdownDocument;
using DeskVault.Application.Documents.Extraction.TextDocument;
using DeskVault.Application.Documents.Extraction.XmlDocument;
using DeskVault.Application.Documents.Normalization;
using Microsoft.Extensions.DependencyInjection;

namespace DeskVault.Application.Tests;

public sealed class ApplicationDependencyInjectionTests
{
    [Fact]
    public void AddApplication_RegistersDocumentProcessingPipeline()
    {
        var services =
            new ServiceCollection();

        services.AddApplication();

        using ServiceProvider serviceProvider =
            services.BuildServiceProvider();

        Assert.NotNull(
            serviceProvider.GetRequiredService<IDocumentTextNormalizer>());

        Assert.NotNull(
            serviceProvider.GetRequiredService<IDocumentTextChunker>());

        Assert.NotNull(
            serviceProvider.GetRequiredService<DocumentTextExtractorResolver>());
    }

    [Fact]
    public void AddApplication_RegistersExpectedConcreteImplementations()
    {
        var services =
            new ServiceCollection();

        services.AddApplication();

        using ServiceProvider serviceProvider =
            services.BuildServiceProvider();

        Assert.IsType<DocumentTextNormalizer>(
            serviceProvider.GetRequiredService<IDocumentTextNormalizer>());

        Assert.IsType<DocumentTextChunker>(
            serviceProvider.GetRequiredService<IDocumentTextChunker>());

        var extractors =
            serviceProvider
                .GetServices<IDocumentTextExtractor>()
                .ToList();

        Assert.Equal(
            5,
            extractors.Count);

        Assert.Contains(
            extractors,
            extractor => extractor is TextDocumentTextExtractor);

        Assert.Contains(
            extractors,
            extractor => extractor is MarkdownDocumentTextExtractor);

        Assert.Contains(
            extractors,
            extractor => extractor is CsvDocumentTextExtractor);

        Assert.Contains(
            extractors,
            extractor => extractor is JsonDocumentTextExtractor);

        Assert.Contains(
            extractors,
            extractor => extractor is XmlDocumentTextExtractor);
    }
}
