using DeskVault.Application.Configurations;
using DeskVault.Application.Documents.Extraction;
using DeskVault.Application.Documents.Extraction.XmlDocument;
using DeskVault.Application.Documents.Processing;
using System.Text;
using System.Xml;

namespace DeskVault.Application.Tests;

public sealed class XmlResourceBoundaryTests
{
    [Fact]
    public async Task ExtractAsync_SourceBytesAtLimit_PreservesDocument()
    {
        // Arrange
        const string xml = "<root/>";

        var options = new DocumentProcessingOptions
        {
            MaxDecryptedDocumentBytes = Encoding.UTF8.GetByteCount(xml)
        };

        // Act
        DocumentTextExtractionResult result =
            await ExtractAsync(xml, options);

        // Assert
        Assert.Equal("root:", result.Text);
    }

    [Fact]
    public async Task ExtractAsync_SourceBytesAboveLimit_ThrowsResourceLimitException()
    {
        // Arrange
        const string xml = "<root />";

        var options = new DocumentProcessingOptions
        {
            MaxDecryptedDocumentBytes = 7
        };

        // Act
        Task<DocumentTextExtractionResult> extractionTask =
            ExtractAsync(xml, options);

        // Assert
        ResourceLimitExceededException exception =
            await Assert.ThrowsAsync<ResourceLimitExceededException>(
                () => extractionTask);

        Assert.Equal(7L, exception.LimitBytes);
        Assert.Equal(8L, exception.AttemptedBytes);
    }

    [Fact]
    public async Task ExtractAsync_NestingDepthAtLimit_PreservesDocument()
    {
        // Arrange
        const string xml = "<root><child /></root>";

        var options = new DocumentProcessingOptions
        {
            MaxParserNestingDepth = 2
        };

        // Act
        DocumentTextExtractionResult result =
            await ExtractAsync(xml, options);

        // Assert
        Assert.Equal(
            "root:" + Environment.NewLine +
            "  child:",
            result.Text);
    }

    [Fact]
    public async Task ExtractAsync_NestingDepthAboveLimit_ThrowsStructureLimitException()
    {
        // Arrange
        const string xml = "<root><child><leaf /></child></root>";

        var options = new DocumentProcessingOptions
        {
            MaxParserNestingDepth = 2
        };

        // Act
        Task<DocumentTextExtractionResult> extractionTask =
            ExtractAsync(xml, options);

        // Assert
        DocumentStructureLimitExceededException exception =
            await Assert.ThrowsAsync<DocumentStructureLimitExceededException>(
                () => extractionTask);

        Assert.Equal("XML nesting depth", exception.ResourceName);
        Assert.Equal(2L, exception.Limit);
        Assert.Equal(3L, exception.Attempted);
    }

    [Fact]
    public async Task ExtractAsync_NodeCountAtLimit_PreservesDocument()
    {
        // Arrange
        const string xml = "<root />";

        var options = new DocumentProcessingOptions
        {
            MaxStructuredNodeCount = 1
        };

        // Act
        DocumentTextExtractionResult result =
            await ExtractAsync(xml, options);

        // Assert
        Assert.Equal("root:", result.Text);
    }

    [Fact]
    public async Task ExtractAsync_NodeCountAboveLimit_ThrowsStructureLimitException()
    {
        // Arrange
        const string xml = "<root><child /></root>";

        var options = new DocumentProcessingOptions
        {
            MaxStructuredNodeCount = 1
        };

        // Act
        Task<DocumentTextExtractionResult> extractionTask =
            ExtractAsync(xml, options);

        // Assert
        DocumentStructureLimitExceededException exception =
            await Assert.ThrowsAsync<DocumentStructureLimitExceededException>(
                () => extractionTask);

        Assert.Equal("XML structured nodes", exception.ResourceName);
        Assert.Equal(1L, exception.Limit);
        Assert.Equal(2L, exception.Attempted);
    }

    [Fact]
    public async Task ExtractAsync_DtdAndEntityDeclaration_RejectsDocument()
    {
        // Arrange
        const string xml =
            """
            <!DOCTYPE root [<!ENTITY secret "expanded">]>
            <root>&secret;</root>
            """;

        var options = new DocumentProcessingOptions();

        // Act
        Task<DocumentTextExtractionResult> extractionTask =
            ExtractAsync(xml, options);

        // Assert
        await Assert.ThrowsAnyAsync<XmlException>(
            () => extractionTask);
    }

    [Fact]
    public async Task ExtractAsync_MalformedXml_ThrowsXmlException()
    {
        // Arrange
        const string xml = "<root><child>";

        var options = new DocumentProcessingOptions();

        // Act
        Task<DocumentTextExtractionResult> extractionTask =
            ExtractAsync(xml, options);

        // Assert
        await Assert.ThrowsAnyAsync<XmlException>(
            () => extractionTask);
    }

    private static async Task<DocumentTextExtractionResult> ExtractAsync(
        string xml,
        DocumentProcessingOptions options)
    {
        // Arrange
        await using var stream = new MemoryStream(
            Encoding.UTF8.GetBytes(xml));

        var extractor = new XmlDocumentTextExtractor(options);

        // Act
        return await extractor.ExtractAsync(
            stream,
            "document.xml",
            TestContext.Current.CancellationToken);
    }
}
