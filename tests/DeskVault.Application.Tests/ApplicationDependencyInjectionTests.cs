using DeskVault.Application.Configurations;
using DeskVault.Application.Documents.Chunking;
using DeskVault.Application.Documents.Extraction;
using DeskVault.Application.Documents.Extraction.CSVDocument;
using DeskVault.Application.Documents.Extraction.IniDocument;
using DeskVault.Application.Documents.Extraction.JsonDocument;
using DeskVault.Application.Documents.Extraction.MarkdownDocument;
using DeskVault.Application.Documents.Extraction.TextDocument;
using DeskVault.Application.Documents.Extraction.XmlDocument;
using DeskVault.Application.Documents.Extraction.YamlDocument;
using DeskVault.Application.Documents.Normalization;
using DeskVault.Application.Documents.Queries.SearchDocuments;
using DeskVault.Application.Documents.Processing;
using DeskVault.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System.Text;

namespace DeskVault.Application.Tests;

public sealed class ApplicationDependencyInjectionTests
{
    [Fact]
    public void AddApplication_RegistersDocumentProcessingPipeline()
    {
        var services =
            new ServiceCollection();

        services.AddSingleton(
            new DocumentProcessingOptions());

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

        services.AddSingleton(
            new DocumentProcessingOptions());

        services.AddApplication();

        using ServiceProvider serviceProvider =
            services.BuildServiceProvider();

        Assert.IsType<DocumentTextNormalizer>(
            serviceProvider.GetRequiredService<IDocumentTextNormalizer>());

        Assert.IsType<DocumentTextChunker>(
            serviceProvider.GetRequiredService<IDocumentTextChunker>());

        Assert.IsType<SearchDocumentsRanker>(
            serviceProvider.GetRequiredService<ISearchDocumentsRanker>());

        var extractors =
            serviceProvider
                .GetServices<IDocumentTextExtractor>()
                .ToList();

        Assert.Equal(
            7,
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

        Assert.Contains(
            extractors,
            extractor => extractor is YamlDocumentTextExtractor);

        Assert.Contains(
            extractors,
            extractor => extractor is IniDocumentTextExtractor);
    }

    [Fact]
    public async Task AddApplication_PropagatesConfiguredProcessedTextLimitToAllExtractors()
    {
        const long configuredLimit = 1;

        var services =
            new ServiceCollection();

        services.AddSingleton(
            new DocumentProcessingOptions
            {
                MaxProcessedTextBytes =
                    configuredLimit
            });

        services.AddApplication();

        using ServiceProvider serviceProvider =
            services.BuildServiceProvider();

        var extractors =
            serviceProvider
                .GetServices<IDocumentTextExtractor>()
                .ToList();

        var extractorInputs =
            new Dictionary<Type, (string FileName, string Content)>
            {
                [typeof(TextDocumentTextExtractor)] =
                    ("document.txt", "DeskVault text"),

                [typeof(MarkdownDocumentTextExtractor)] =
                    ("document.md", "# DeskVault heading"),

                [typeof(CsvDocumentTextExtractor)] =
                    ("document.csv", "Name,Value\nDeskVault,Test"),

                [typeof(JsonDocumentTextExtractor)] =
                    ("document.json", "{\"name\":\"DeskVault\"}"),

                [typeof(XmlDocumentTextExtractor)] =
                    ("document.xml", "<root><name>DeskVault</name></root>"),

                [typeof(YamlDocumentTextExtractor)] =
                    ("document.yaml", "name: DeskVault"),

                [typeof(IniDocumentTextExtractor)] =
                    ("document.ini", "[General]\nName=DeskVault")
            };

        foreach (IDocumentTextExtractor extractor in extractors)
        {
            (string fileName, string content) =
                extractorInputs[extractor.GetType()];

            await using var stream =
                new MemoryStream(
                    Encoding.UTF8.GetBytes(content));

            ResourceLimitExceededException exception =
                await Assert.ThrowsAsync<ResourceLimitExceededException>(
                    () =>
                        extractor.ExtractAsync(
                            stream,
                            fileName));

            Assert.Equal(
                configuredLimit,
                exception.LimitBytes);
        }
    }
}
