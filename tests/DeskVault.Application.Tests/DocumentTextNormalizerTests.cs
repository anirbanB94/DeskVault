using DeskVault.Application.Documents.Extraction;
using DeskVault.Application.Documents.Normalization;

namespace DeskVault.Application.Tests;

public sealed class DocumentTextNormalizerTests
{
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
            new DocumentTextExtractionResult(text);

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
            new DocumentTextExtractionResult(text);

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
            new DocumentTextExtractionResult(text);

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
            new DocumentTextExtractionResult(text);

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
}
