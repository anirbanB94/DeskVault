using DeskVault.Application.Configurations;
using DeskVault.Application.Documents.Chunking;
using DeskVault.Application.Documents.Extraction;
using DeskVault.Application.Documents.Extraction.JsonDocument;
using DeskVault.Application.Documents.Extraction.MarkdownDocument;
using DeskVault.Application.Documents.Extraction.TextDocument;
using DeskVault.Application.Documents.Normalization;
using DeskVault.Application.Documents.Provenance;
using System.Text;

namespace DeskVault.Application.Tests;

public sealed class DocumentTextSourceLocationPipelineTests
{
    [Fact]
    public async Task TextPipeline_PropagatesDirectSourceLocationToChunks()
    {
        const string sourceText =
            "First paragraph.\n\nSecond paragraph.";

        await using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(
                    sourceText));

        var extractor =
            new TextDocumentTextExtractor();

        DocumentTextExtractionResult extractionResult =
            await extractor.ExtractAsync(
                stream,
                "document.txt");

        Assert.Equal(
            DocumentSourceLocationMappingKind.DirectText,
            extractionResult.SourceLocationMappingKind);

        var normalizer =
            new DocumentTextNormalizer();

        DocumentTextNormalizationResult normalizationResult =
            await normalizer.NormalizeAsync(
                extractionResult);

        Assert.Equal(
            DocumentSourceLocationMappingKind.DirectText,
            normalizationResult.SourceLocationMappingKind);

        var chunker =
            new DocumentTextChunker(
                maxChunkSize: "Second paragraph.".Length);

        IReadOnlyList<DocumentChunk> chunks =
            await chunker.ChunkAsync(
                normalizationResult);

        Assert.Equal(
            2,
            chunks.Count);

        Assert.Equal(
            new DocumentSourceLocation(
                1,
                1),
            chunks[0].SourceLocation);

        Assert.Equal(
            new DocumentSourceLocation(
                3,
                3),
            chunks[1].SourceLocation);
    }

    [Fact]
    public async Task MarkdownPipeline_PropagatesDirectSourceLocationToChunks()
    {
        const string sourceText =
            "# DeskVault\n\n" +
            "Enterprise knowledge search.";

        await using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(
                    sourceText));

        var extractor =
            new MarkdownDocumentTextExtractor();

        DocumentTextExtractionResult extractionResult =
            await extractor.ExtractAsync(
                stream,
                "document.md");

        Assert.Equal(
            DocumentSourceLocationMappingKind.DirectText,
            extractionResult.SourceLocationMappingKind);

        var normalizer =
            new DocumentTextNormalizer(
                new DocumentProcessingOptions());

        DocumentTextNormalizationResult normalizationResult =
            await normalizer.NormalizeAsync(
                extractionResult);

        Assert.Equal(
            DocumentSourceLocationMappingKind.DirectText,
            normalizationResult.SourceLocationMappingKind);

        var chunker =
            new DocumentTextChunker(
                maxChunkSize: sourceText.Length);

        IReadOnlyList<DocumentChunk> chunks =
            await chunker.ChunkAsync(
                normalizationResult);

        DocumentChunk chunk =
            Assert.Single(chunks);

        Assert.Equal(
            new DocumentSourceLocation(
                1,
                3),
            chunk.SourceLocation);
    }

    [Fact]
    public async Task JsonPipeline_DoesNotFabricateSourceLocation()
    {
        const string sourceText =
            """
            {
              "name": "DeskVault",
              "purpose": "enterprise knowledge search"
            }
            """;

        await using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(
                    sourceText));

        var extractor =
            new JsonDocumentTextExtractor();

        DocumentTextExtractionResult extractionResult =
            await extractor.ExtractAsync(
                stream,
                "document.json");

        Assert.Equal(
            DocumentSourceLocationMappingKind.Unknown,
            extractionResult.SourceLocationMappingKind);

        var normalizer =
            new DocumentTextNormalizer();

        DocumentTextNormalizationResult normalizationResult =
            await normalizer.NormalizeAsync(
                extractionResult);

        Assert.Equal(
            DocumentSourceLocationMappingKind.Unknown,
            normalizationResult.SourceLocationMappingKind);

        var chunker =
            new DocumentTextChunker(
                maxChunkSize: 1000);

        IReadOnlyList<DocumentChunk> chunks =
            await chunker.ChunkAsync(
                normalizationResult);

        Assert.NotEmpty(chunks);

        Assert.All(
            chunks,
            chunk =>
                Assert.Null(
                    chunk.SourceLocation));
    }

    [Fact]
    public async Task TextPipeline_CrlfNormalization_PreservesSourceLineLocation()
    {
        const string sourceText =
            "First line.\r\n" +
            "Second line.\r\n" +
            "Third line.";

        await using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(
                    sourceText));

        var extractor =
            new TextDocumentTextExtractor();

        DocumentTextExtractionResult extractionResult =
            await extractor.ExtractAsync(
                stream,
                "document.txt");

        var normalizer =
            new DocumentTextNormalizer();

        DocumentTextNormalizationResult normalizationResult =
            await normalizer.NormalizeAsync(
                extractionResult);

        Assert.Equal(
            "First line.\nSecond line.\nThird line.",
            normalizationResult.Text);

        Assert.Equal(
            DocumentSourceLocationMappingKind.DirectText,
            normalizationResult.SourceLocationMappingKind);

        var chunker =
            new DocumentTextChunker(
                maxChunkSize: 1000);

        IReadOnlyList<DocumentChunk> chunks =
            await chunker.ChunkAsync(
                normalizationResult);

        DocumentChunk chunk =
            Assert.Single(chunks);

        Assert.Equal(
            new DocumentSourceLocation(
                1,
                3),
            chunk.SourceLocation);
    }
}
