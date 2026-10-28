using DeskVault.Application.Configurations;
using DeskVault.Application.Documents.Extraction;
using DeskVault.Application.Documents.Extraction.YamlDocument;
using DeskVault.Application.Documents.Processing;
using System.Text;

namespace DeskVault.Application.Tests;

public sealed class YamlResourceBoundaryTests
{
    [Fact]
    public async Task ExtractAsync_SourceBytesAtLimit_PreservesDocument()
    {
        // Arrange
        const string yaml = "name: value";

        var options = new DocumentProcessingOptions
        {
            MaxDecryptedDocumentBytes = Encoding.UTF8.GetByteCount(yaml)
        };

        // Act
        DocumentTextExtractionResult result =
            await ExtractAsync(yaml, options);

        // Assert
        Assert.Equal(yaml, result.Text);
    }

    [Fact]
    public async Task ExtractAsync_SourceBytesAboveLimit_ThrowsResourceLimitException()
    {
        // Arrange
        const string yaml = "name: value";

        var options = new DocumentProcessingOptions
        {
            MaxDecryptedDocumentBytes = 3
        };

        // Act
        Task<DocumentTextExtractionResult> extractionTask =
            ExtractAsync(yaml, options);

        // Assert
        ResourceLimitExceededException exception =
            await Assert.ThrowsAsync<ResourceLimitExceededException>(
                () => extractionTask);

        Assert.Equal(3L, exception.LimitBytes);
        Assert.Equal(4L, exception.AttemptedBytes);
    }

    [Fact]
    public async Task ExtractAsync_NestingDepthAtLimit_PreservesDocument()
    {
        // Arrange
        const string yaml =
            """
            outer:
              inner: value
            """;

        var options = new DocumentProcessingOptions
        {
            MaxParserNestingDepth = 2
        };

        // Act
        DocumentTextExtractionResult result =
            await ExtractAsync(yaml, options);

        // Assert
        Assert.Equal(
            "outer:" + Environment.NewLine +
            "  inner: value",
            result.Text);
    }

    [Fact]
    public async Task ExtractAsync_NestingDepthAboveLimit_ThrowsStructureLimitException()
    {
        // Arrange
        const string yaml =
            """
            outer:
              middle:
                inner: value
            """;

        var options = new DocumentProcessingOptions
        {
            MaxParserNestingDepth = 2
        };

        // Act
        Task<DocumentTextExtractionResult> extractionTask =
            ExtractAsync(yaml, options);

        // Assert
        DocumentStructureLimitExceededException exception =
            await Assert.ThrowsAsync<DocumentStructureLimitExceededException>(
                () => extractionTask);

        Assert.Equal("YAML nesting depth", exception.ResourceName);
        Assert.Equal(2L, exception.Limit);
        Assert.Equal(3L, exception.Attempted);
    }

    [Fact]
    public async Task ExtractAsync_NodeCountAtLimit_PreservesDocument()
    {
        // Arrange
        const string yaml = "a: b";

        var options = new DocumentProcessingOptions
        {
            MaxStructuredNodeCount = 3
        };

        // Act
        DocumentTextExtractionResult result =
            await ExtractAsync(yaml, options);

        // Assert
        Assert.Equal(yaml, result.Text);
    }

    [Fact]
    public async Task ExtractAsync_NodeCountAboveLimit_ThrowsStructureLimitException()
    {
        // Arrange
        const string yaml = "a: b";

        var options = new DocumentProcessingOptions
        {
            MaxStructuredNodeCount = 2
        };

        // Act
        Task<DocumentTextExtractionResult> extractionTask =
            ExtractAsync(yaml, options);

        // Assert
        DocumentStructureLimitExceededException exception =
            await Assert.ThrowsAsync<DocumentStructureLimitExceededException>(
                () => extractionTask);

        Assert.Equal("YAML structured nodes", exception.ResourceName);
        Assert.Equal(2L, exception.Limit);
        Assert.Equal(3L, exception.Attempted);
    }

    [Fact]
    public async Task ExtractAsync_RecursiveAlias_RejectsUnsafeExpansion()
    {
        // Arrange
        const string yaml =
            """
            &recursive
            - *recursive
            """;

        var options = new DocumentProcessingOptions();

        // Act
        Task<DocumentTextExtractionResult> extractionTask =
            ExtractAsync(yaml, options);

        // Assert
        await Assert.ThrowsAsync<InvalidDataException>(
            () => extractionTask);
    }

    private static async Task<DocumentTextExtractionResult> ExtractAsync(
        string yaml,
        DocumentProcessingOptions options)
    {
        // Arrange
        await using var stream = new MemoryStream(
            Encoding.UTF8.GetBytes(yaml));

        var extractor = new YamlDocumentTextExtractor(options);

        // Act
        return await extractor.ExtractAsync(
            stream,
            "document.yaml",
            TestContext.Current.CancellationToken);
    }
}
