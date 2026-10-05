using DeskVault.Application.Documents.Content;
using DeskVault.Application.Documents.Extraction;
using DeskVault.Application.Documents.Normalization;
using DeskVault.Application.Documents.Provenance;

namespace DeskVault.Application.Tests;

public sealed class DocumentTextNormalizerStructuredContentTests
{
    [Fact]
    public async Task NormalizeAsync_StructuredContent_PreservesStructureWarningsAndProjection()
    {
        // Arrange
        DocumentContent content =
            CreateStructuredContent();

        DocumentTextExtractionResult extractionResult =
            new(
                content.SearchableText,
                Content: content);

        DocumentTextNormalizer normalizer =
            new();

        // Act
        DocumentTextNormalizationResult result =
            await normalizer.NormalizeAsync(
                extractionResult);

        // Assert
        Assert.NotNull(
            result.Content);

        DocumentContent normalizedContent =
            result.Content!;

        Assert.Equal(
            2,
            normalizedContent.Units.Count);

        Assert.Equal(
            "José\nBanerjee",
            normalizedContent.Units[0].Fields[1].Value);

        Assert.Single(
            normalizedContent.Warnings);

        Assert.Equal(
            "CSV row contained fewer fields than the header.",
            normalizedContent.Warnings[0].Message);

        Assert.Equal(
            normalizedContent.SearchableText,
            result.Text);
    }

    [Fact]
    public async Task NormalizeAsync_StructuredContent_NormalizesUnicodeAndLineEndings()
    {
        // Arrange
        DocumentContent content =
            new(
            [
                new DocumentContentUnit(
                    order: 0,
                    kind: DocumentContentUnitKind.TableRow,
                    fields:
                    [
                        new DocumentContentField(
                            "Name",
                            "Jose\u0301\r\nBanerjee")
                    ])
            ]);

        DocumentTextExtractionResult extractionResult =
            new(
                content.SearchableText,
                Content: content);

        DocumentTextNormalizer normalizer =
            new();

        // Act
        DocumentTextNormalizationResult result =
            await normalizer.NormalizeAsync(
                extractionResult);

        // Assert
        Assert.NotNull(
            result.Content);

        DocumentContentUnit row =
            result.Content!.Units[0];

        Assert.Equal(
            "José\nBanerjee",
            row.Fields[0].Value);

        Assert.Equal(
            "Name: José\nBanerjee",
            result.Text);
    }

    [Fact]
    public async Task NormalizeAsync_TextOnlyExtraction_RemainsTextOnly()
    {
        // Arrange
        const string text =
            "Plain\r\ntext.";

        DocumentTextExtractionResult extractionResult =
            new(text);

        DocumentTextNormalizer normalizer =
            new();

        // Act
        DocumentTextNormalizationResult result =
            await normalizer.NormalizeAsync(
                extractionResult);

        // Assert
        Assert.Null(
            result.Content);

        Assert.Equal(
            "Plain\ntext.",
            result.Text);
    }

    [Fact]
    public async Task NormalizeAsync_StructuredContent_PreservesSourceLocationMapping()
    {
        // Arrange
        DocumentContent content =
            CreateStructuredContent();

        DocumentTextExtractionResult extractionResult =
            new(
                content.SearchableText,
                DocumentSourceLocationMappingKind.DirectText,
                content);

        DocumentTextNormalizer normalizer =
            new();

        // Act
        DocumentTextNormalizationResult result =
            await normalizer.NormalizeAsync(
                extractionResult);

        // Assert
        Assert.Equal(
            DocumentSourceLocationMappingKind.DirectText,
            result.SourceLocationMappingKind);

        Assert.NotNull(
            result.Content);
    }

    private static DocumentContent CreateStructuredContent()
    {
        return new DocumentContent(
        [
            CreateTableRow(
                0,
                CreateField("Id", "1001"),
                CreateField(
                    "Name",
                    "Jose\u0301\r\nBanerjee")),

            CreateTableRow(
                1,
                CreateField("Id", "1002"),
                CreateField(
                    "Name",
                    "Alice Johnson"))
        ],
        [
            new DocumentContentWarning(
                "CSV row contained fewer fields than the header.")
        ]);
    }

    private static DocumentContentUnit CreateTableRow(
        int order,
        params DocumentContentField[] fields)
    {
        return new DocumentContentUnit(
            order,
            DocumentContentUnitKind.TableRow,
            fields: fields);
    }

    private static DocumentContentField CreateField(
        string name,
        string value)
    {
        return new DocumentContentField(
            name,
            value);
    }
}
