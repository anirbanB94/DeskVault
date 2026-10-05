using DeskVault.Application.Documents.Chunking;
using DeskVault.Application.Documents.Processing;

namespace DeskVault.Application.Tests;

public sealed class DocumentRuleVersionTests
{
    private const string ExtractorRuleVersion =
        "text-extractor-v1";

    private const string NormalizerRuleVersion =
        "unicode-nfc-lf-v1";

    private const string ChunkingAlgorithmVersion =
        "paragraph-chunker-v1";

    private const int MaxChunkSize =
        4000;

    [Fact]
    public void CreateProcessingRuleVersion_SameRules_ProducesSameVersion()
    {
        // Arrange
        string extractorRuleVersion =
            ExtractorRuleVersion;

        string normalizerRuleVersion =
            NormalizerRuleVersion;

        // Act
        DocumentProcessingRuleVersion first =
            DocumentRuleVersionFactory.CreateProcessingRuleVersion(
                extractorRuleVersion,
                normalizerRuleVersion);

        DocumentProcessingRuleVersion second =
            DocumentRuleVersionFactory.CreateProcessingRuleVersion(
                extractorRuleVersion,
                normalizerRuleVersion);

        // Assert
        Assert.Equal(
            first,
            second);

        Assert.False(
            string.IsNullOrWhiteSpace(
                first.Value));
    }

    [Fact]
    public void CreateProcessingRuleVersion_DifferentExtractorRules_ProducesDifferentVersion()
    {
        // Arrange
        const string firstExtractorRuleVersion =
            ExtractorRuleVersion;

        const string secondExtractorRuleVersion =
            "markdown-extractor-v1";

        // Act
        DocumentProcessingRuleVersion first =
            DocumentRuleVersionFactory.CreateProcessingRuleVersion(
                firstExtractorRuleVersion,
                NormalizerRuleVersion);

        DocumentProcessingRuleVersion second =
            DocumentRuleVersionFactory.CreateProcessingRuleVersion(
                secondExtractorRuleVersion,
                NormalizerRuleVersion);

        // Assert
        Assert.NotEqual(
            first,
            second);
    }

    [Fact]
    public void CreateProcessingRuleVersion_DifferentNormalizationRules_ProducesDifferentVersion()
    {
        // Arrange
        const string firstNormalizerRuleVersion =
            NormalizerRuleVersion;

        const string secondNormalizerRuleVersion =
            "unicode-nfc-lf-v2";

        // Act
        DocumentProcessingRuleVersion first =
            DocumentRuleVersionFactory.CreateProcessingRuleVersion(
                ExtractorRuleVersion,
                firstNormalizerRuleVersion);

        DocumentProcessingRuleVersion second =
            DocumentRuleVersionFactory.CreateProcessingRuleVersion(
                ExtractorRuleVersion,
                secondNormalizerRuleVersion);

        // Assert
        Assert.NotEqual(
            first,
            second);
    }

    [Fact]
    public void CreateChunkingRuleVersion_SameRules_ProducesSameVersion()
    {
        // Arrange
        string chunkingAlgorithmVersion =
            ChunkingAlgorithmVersion;

        int maxChunkSize =
            MaxChunkSize;

        // Act
        DocumentChunkingRuleVersion first =
            DocumentRuleVersionFactory.CreateChunkingRuleVersion(
                chunkingAlgorithmVersion,
                maxChunkSize);

        DocumentChunkingRuleVersion second =
            DocumentRuleVersionFactory.CreateChunkingRuleVersion(
                chunkingAlgorithmVersion,
                maxChunkSize);

        // Assert
        Assert.Equal(
            first,
            second);

        Assert.False(
            string.IsNullOrWhiteSpace(
                first.Value));
    }

    [Fact]
    public void CreateChunkingRuleVersion_DifferentAlgorithmVersions_ProducesDifferentVersion()
    {
        // Arrange
        const string firstChunkingAlgorithmVersion =
            ChunkingAlgorithmVersion;

        const string secondChunkingAlgorithmVersion =
            "paragraph-chunker-v2";

        // Act
        DocumentChunkingRuleVersion first =
            DocumentRuleVersionFactory.CreateChunkingRuleVersion(
                firstChunkingAlgorithmVersion,
                MaxChunkSize);

        DocumentChunkingRuleVersion second =
            DocumentRuleVersionFactory.CreateChunkingRuleVersion(
                secondChunkingAlgorithmVersion,
                MaxChunkSize);

        // Assert
        Assert.NotEqual(
            first,
            second);
    }

    [Fact]
    public void CreateChunkingRuleVersion_DifferentChunkSize_ProducesDifferentVersion()
    {
        // Arrange
        const int firstMaxChunkSize =
            MaxChunkSize;

        const int secondMaxChunkSize =
            2000;

        // Act
        DocumentChunkingRuleVersion first =
            DocumentRuleVersionFactory.CreateChunkingRuleVersion(
                ChunkingAlgorithmVersion,
                firstMaxChunkSize);

        DocumentChunkingRuleVersion second =
            DocumentRuleVersionFactory.CreateChunkingRuleVersion(
                ChunkingAlgorithmVersion,
                secondMaxChunkSize);

        // Assert
        Assert.NotEqual(
            first,
            second);
    }

    [Fact]
    public void ProcessingRuleVersion_AndChunkingRuleVersion_UseDistinctContracts()
    {
        // Arrange
        DocumentProcessingRuleVersion processingVersion =
            DocumentRuleVersionFactory.CreateProcessingRuleVersion(
                ExtractorRuleVersion,
                NormalizerRuleVersion);

        DocumentChunkingRuleVersion chunkingVersion =
            DocumentRuleVersionFactory.CreateChunkingRuleVersion(
                ChunkingAlgorithmVersion,
                MaxChunkSize);

        // Act
        string processingValue =
            processingVersion.Value;

        string chunkingValue =
            chunkingVersion.Value;

        // Assert
        Assert.False(
            string.IsNullOrWhiteSpace(
                processingValue));

        Assert.False(
            string.IsNullOrWhiteSpace(
                chunkingValue));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void DocumentProcessingRuleVersion_EmptyValue_IsRejected(
        string value)
    {
        // Arrange
        string invalidValue =
            value;

        // Act
        Action act =
            () => new DocumentProcessingRuleVersion(
                invalidValue);

        // Assert
        Assert.Throws<ArgumentException>(
            act);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void DocumentChunkingRuleVersion_EmptyValue_IsRejected(
        string value)
    {
        // Arrange
        string invalidValue =
            value;

        // Act
        Action act =
            () => new DocumentChunkingRuleVersion(
                invalidValue);

        // Assert
        Assert.Throws<ArgumentException>(
            act);
    }

    [Fact]
    public void CreateProcessingRuleVersion_EmptyExtractorRuleVersion_IsRejected()
    {
        // Arrange
        const string extractorRuleVersion = "";

        // Act
        Action act =
            () => DocumentRuleVersionFactory.CreateProcessingRuleVersion(
                extractorRuleVersion,
                NormalizerRuleVersion);

        // Assert
        Assert.Throws<ArgumentException>(
            act);
    }

    [Fact]
    public void CreateProcessingRuleVersion_EmptyNormalizerRuleVersion_IsRejected()
    {
        // Arrange
        const string normalizerRuleVersion = "";

        // Act
        Action act =
            () => DocumentRuleVersionFactory.CreateProcessingRuleVersion(
                ExtractorRuleVersion,
                normalizerRuleVersion);

        // Assert
        Assert.Throws<ArgumentException>(
            act);
    }

    [Fact]
    public void CreateChunkingRuleVersion_EmptyAlgorithmVersion_IsRejected()
    {
        // Arrange
        const string algorithmVersion = "";

        // Act
        Action act =
            () => DocumentRuleVersionFactory.CreateChunkingRuleVersion(
                algorithmVersion,
                MaxChunkSize);

        // Assert
        Assert.Throws<ArgumentException>(
            act);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CreateChunkingRuleVersion_NonPositiveChunkSize_IsRejected(
        int maxChunkSize)
    {
        // Arrange
        int invalidChunkSize =
            maxChunkSize;

        // Act
        Action act =
            () => DocumentRuleVersionFactory.CreateChunkingRuleVersion(
                ChunkingAlgorithmVersion,
                invalidChunkSize);

        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(
            act);
    }
}
