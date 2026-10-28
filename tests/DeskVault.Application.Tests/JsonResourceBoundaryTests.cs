using DeskVault.Application.Configurations;
using DeskVault.Application.Documents.Extraction;
using DeskVault.Application.Documents.Extraction.JsonDocument;
using DeskVault.Application.Documents.Processing;
using System.Text;

namespace DeskVault.Application.Tests;

public sealed class JsonResourceBoundaryTests
{
    [Theory]
    [InlineData("{\"a\":1}", 3)]
    [InlineData("{\"a\":1}", 4)]
    public async Task ExtractAsync_NodeCountAtOrBelowLimit_PreservesDocument(
        string json,
        int maximumNodeCount)
    {
        // Arrange
        var options = new DocumentProcessingOptions
        {
            MaxStructuredNodeCount = maximumNodeCount
        };

        // Act
        DocumentTextExtractionResult result =
            await ExtractAsync(json, options);

        // Assert
        Assert.Equal("a: 1", result.Text);
    }

    [Fact]
    public async Task ExtractAsync_NodeCountAboveLimit_ThrowsStructureLimitException()
    {
        // Arrange
        const string json = """{"a":1,"b":2}""";

        var options = new DocumentProcessingOptions
        {
            MaxStructuredNodeCount = 4
        };

        // Act
        Task<DocumentTextExtractionResult> extractionTask =
            ExtractAsync(json, options);

        // Assert
        DocumentStructureLimitExceededException exception =
            await Assert.ThrowsAsync<DocumentStructureLimitExceededException>(
                () => extractionTask);

        Assert.Equal("JSON structured nodes", exception.ResourceName);
        Assert.Equal(4L, exception.Limit);
        Assert.Equal(5L, exception.Attempted);
    }

    [Fact]
    public async Task ExtractAsync_NestingDepthAtLimit_PreservesDocument()
    {
        // Arrange
        const string json = """{"outer":{"inner":1}}""";

        var options = new DocumentProcessingOptions
        {
            MaxParserNestingDepth = 2
        };

        // Act
        DocumentTextExtractionResult result =
            await ExtractAsync(json, options);

        // Assert
        Assert.Equal(
            "outer:" + Environment.NewLine +
            "  inner: 1",
            result.Text);
    }

    [Fact]
    public async Task ExtractAsync_NestingDepthAboveLimit_ThrowsStructureLimitException()
    {
        // Arrange
        const string json = """{"outer":{"middle":{"inner":1}}}""";

        var options = new DocumentProcessingOptions
        {
            MaxParserNestingDepth = 2
        };

        // Act
        Task<DocumentTextExtractionResult> extractionTask =
            ExtractAsync(json, options);

        // Assert
        DocumentStructureLimitExceededException exception =
            await Assert.ThrowsAsync<DocumentStructureLimitExceededException>(
                () => extractionTask);

        Assert.Equal("JSON nesting depth", exception.ResourceName);
        Assert.Equal(2L, exception.Limit);
        Assert.True(exception.Attempted > exception.Limit);
    }

    [Fact]
    public async Task ExtractAsync_SourceBytesAtLimit_PreservesDocument()
    {
        // Arrange
        const string json = "{}";

        var options = new DocumentProcessingOptions
        {
            MaxDecryptedDocumentBytes = Encoding.UTF8.GetByteCount(json)
        };

        // Act
        DocumentTextExtractionResult result =
            await ExtractAsync(json, options);

        // Assert
        Assert.Equal(string.Empty, result.Text);
    }

    [Fact]
    public async Task ExtractAsync_SourceBytesAboveLimit_ThrowsResourceLimitException()
    {
        // Arrange
        const string json = "{} ";

        var options = new DocumentProcessingOptions
        {
            MaxDecryptedDocumentBytes = 2
        };

        // Act
        Task<DocumentTextExtractionResult> extractionTask =
            ExtractAsync(json, options);

        // Assert
        ResourceLimitExceededException exception =
            await Assert.ThrowsAsync<ResourceLimitExceededException>(
                () => extractionTask);

        Assert.Equal(2L, exception.LimitBytes);
        Assert.Equal(3L, exception.AttemptedBytes);
    }

    [Fact]
    public async Task ExtractAsync_MalformedJson_ThrowsJsonException()
    {
        // Arrange
        const string json = """{"a":""";

        var options = new DocumentProcessingOptions();

        // Act
        Task<DocumentTextExtractionResult> extractionTask =
            ExtractAsync(json, options);

        // Assert
        await Assert.ThrowsAnyAsync<System.Text.Json.JsonException>(
            () => extractionTask);
    }

    private static async Task<DocumentTextExtractionResult> ExtractAsync(
        string json,
        DocumentProcessingOptions options)
    {
        // Arrange
        await using var stream = new MemoryStream(
            Encoding.UTF8.GetBytes(json));

        var extractor = new JsonDocumentTextExtractor(options);

        // Act
        return await extractor.ExtractAsync(
            stream,
            "document.json",
            TestContext.Current.CancellationToken);
    }
}
