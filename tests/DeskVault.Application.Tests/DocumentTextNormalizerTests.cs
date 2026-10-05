using DeskVault.Application.Configurations;
using DeskVault.Application.Documents.Extraction;
using DeskVault.Application.Documents.Normalization;
using DeskVault.Application.Documents.Processing;
using DeskVault.Application.Documents.Provenance;

namespace DeskVault.Application.Tests;

public sealed class DocumentTextNormalizerTests
{

    [Fact]
    public void RuleVersion_ReturnsStableNormalizerVersion()
    {
        // Arrange
        const string expectedVersion =
            "unicode-nfc-lf-v1";

        DocumentTextNormalizer normalizer =
            new();

        // Act
        string actualVersion =
            normalizer.RuleVersion;

        // Assert
        Assert.Equal(
            expectedVersion,
            actualVersion);
    }

    [Fact]
    public async Task NormalizeAsync_NfcEquivalentText_ReturnsNfcRepresentation()
    {
        string decomposed =
            "Jose\u0301";

        var extractionResult =
            new DocumentTextExtractionResult(
                decomposed);

        var normalizer =
            new DocumentTextNormalizer();

        DocumentTextNormalizationResult result =
            await normalizer.NormalizeAsync(
                extractionResult);

        Assert.Equal(
            "José",
            result.Text);

        Assert.Equal(
            result.Text.Normalize(),
            result.Text);
    }

    [Fact]
    public async Task NormalizeAsync_CrlfLineEndings_ConvertsToLf()
    {
        var extractionResult =
            new DocumentTextExtractionResult(
                "First\r\nSecond\r\nThird");

        var normalizer =
            new DocumentTextNormalizer();

        DocumentTextNormalizationResult result =
            await normalizer.NormalizeAsync(
                extractionResult);

        Assert.Equal(
            "First\nSecond\nThird",
            result.Text);
    }

    [Fact]
    public async Task NormalizeAsync_CrLineEndings_ConvertsToLf()
    {
        var extractionResult =
            new DocumentTextExtractionResult(
                "First\rSecond\rThird");

        var normalizer =
            new DocumentTextNormalizer();

        DocumentTextNormalizationResult result =
            await normalizer.NormalizeAsync(
                extractionResult);

        Assert.Equal(
            "First\nSecond\nThird",
            result.Text);
    }

    [Fact]
    public async Task NormalizeAsync_LfLineEndings_RemainsUnchanged()
    {
        const string text =
            "First\nSecond\nThird";

        var extractionResult =
            new DocumentTextExtractionResult(
                text);

        var normalizer =
            new DocumentTextNormalizer();

        DocumentTextNormalizationResult result =
            await normalizer.NormalizeAsync(
                extractionResult);

        Assert.Equal(
            text,
            result.Text);
    }

    [Fact]
    public async Task NormalizeAsync_TrailingWhitespace_PreservesWhitespace()
    {
        const string text =
            "First line   \nSecond line\t";

        var extractionResult =
            new DocumentTextExtractionResult(
                text);

        var normalizer =
            new DocumentTextNormalizer();

        DocumentTextNormalizationResult result =
            await normalizer.NormalizeAsync(
                extractionResult);

        Assert.Equal(
            text,
            result.Text);
    }

    [Fact]
    public async Task NormalizeAsync_RepeatedBlankLines_PreservesBlankLines()
    {
        const string text =
            "First\n\n\nSecond";

        var extractionResult =
            new DocumentTextExtractionResult(
                text);

        var normalizer =
            new DocumentTextNormalizer();

        DocumentTextNormalizationResult result =
            await normalizer.NormalizeAsync(
                extractionResult);

        Assert.Equal(
            text,
            result.Text);
    }

    [Fact]
    public async Task NormalizeAsync_OuterWhitespace_PreservesWhitespace()
    {
        const string text =
            "  \n  First line\nSecond line  \n  ";

        var extractionResult =
            new DocumentTextExtractionResult(
                text);

        var normalizer =
            new DocumentTextNormalizer();

        DocumentTextNormalizationResult result =
            await normalizer.NormalizeAsync(
                extractionResult);

        Assert.Equal(
            text,
            result.Text);
    }

    [Fact]
    public async Task NormalizeAsync_EmptyText_ReturnsEmptyText()
    {
        var extractionResult =
            new DocumentTextExtractionResult(
                string.Empty);

        var normalizer =
            new DocumentTextNormalizer();

        DocumentTextNormalizationResult result =
            await normalizer.NormalizeAsync(
                extractionResult);

        Assert.Equal(
            string.Empty,
            result.Text);
    }

    [Fact]
    public async Task NormalizeAsync_IsIdempotent()
    {
        var extractionResult =
            new DocumentTextExtractionResult(
                "José\r\n\r\nDeskVault  ");

        var normalizer =
            new DocumentTextNormalizer();

        DocumentTextNormalizationResult first =
            await normalizer.NormalizeAsync(
                extractionResult);

        DocumentTextNormalizationResult second =
            await normalizer.NormalizeAsync(
                new DocumentTextExtractionResult(
                    first.Text));

        Assert.Equal(
            first.Text,
            second.Text);
    }

    [Fact]
    public async Task NormalizeAsync_WhenCancellationRequested_ThrowsOperationCanceledException()
    {
        var extractionResult =
            new DocumentTextExtractionResult(
                "Cancellation test.");

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        var normalizer =
            new DocumentTextNormalizer();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                normalizer.NormalizeAsync(
                    extractionResult,
                    cancellationTokenSource.Token));
    }

    [Fact]
    public async Task NormalizeAsync_WhenNormalizedTextExceedsLimit_ThrowsResourceLimitExceededException()
    {
        const string text = "\u0958";

        var extractionResult =
            new DocumentTextExtractionResult(
                text);

        var options =
            new DocumentProcessingOptions
            {
                MaxProcessedTextBytes = 3
            };

        var normalizer =
            new DocumentTextNormalizer(
                options);

        ResourceLimitExceededException exception =
            await Assert.ThrowsAsync<ResourceLimitExceededException>(
                () =>
                    normalizer.NormalizeAsync(
                        extractionResult));

        Assert.Equal(
            3,
            exception.LimitBytes);

        Assert.Equal(
            6,
            exception.AttemptedBytes);
    }

    [Fact]
    public async Task NormalizeAsync_WhenNormalizedTextIsWithinLimit_ReturnsNormalizedText()
    {
        const string text = "\u0958";
        const string expected = "\u0915\u093c";

        var extractionResult =
            new DocumentTextExtractionResult(
                text);

        var options =
            new DocumentProcessingOptions
            {
                MaxProcessedTextBytes = 6
            };

        var normalizer =
            new DocumentTextNormalizer(
                options);

        DocumentTextNormalizationResult result =
            await normalizer.NormalizeAsync(
                extractionResult);

        Assert.Equal(
            expected,
            result.Text);
    }

    [Fact]
    public async Task NormalizeAsync_WhenProcessedTextLimitIsNonPositive_ThrowsArgumentOutOfRangeException()
    {
        var options =
            new DocumentProcessingOptions
            {
                MaxProcessedTextBytes = 0
            };

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new DocumentTextNormalizer(
                    options));
    }

    [Fact]
    public async Task NormalizeAsync_DirectTextMapping_PreservesSourceLocationMappingKind()
    {
        var extractionResult =
            new DocumentTextExtractionResult(
                "First line.\r\nSecond line.",
                DocumentSourceLocationMappingKind.DirectText);

        var normalizer =
            new DocumentTextNormalizer();

        DocumentTextNormalizationResult result =
            await normalizer.NormalizeAsync(
                extractionResult);

        Assert.Equal(
            DocumentSourceLocationMappingKind.DirectText,
            result.SourceLocationMappingKind);
    }

    [Fact]
    public async Task NormalizeAsync_UnknownMapping_RemainsUnknown()
    {
        var extractionResult =
            new DocumentTextExtractionResult(
                "Generated representation.",
                DocumentSourceLocationMappingKind.Unknown);

        var normalizer =
            new DocumentTextNormalizer();

        DocumentTextNormalizationResult result =
            await normalizer.NormalizeAsync(
                extractionResult);

        Assert.Equal(
            DocumentSourceLocationMappingKind.Unknown,
            result.SourceLocationMappingKind);
    }
}
