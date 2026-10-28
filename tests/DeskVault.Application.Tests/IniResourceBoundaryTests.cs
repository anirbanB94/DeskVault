using DeskVault.Application.Configurations;
using DeskVault.Application.Documents.Extraction;
using DeskVault.Application.Documents.Extraction.IniDocument;
using DeskVault.Application.Documents.Processing;
using System.Text;

namespace DeskVault.Application.Tests;

public sealed class IniResourceBoundaryTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenStructuredNodeCountIsNotPositive_ThrowsArgumentOutOfRangeException(
    int maximumNodeCount)
    {
        // Arrange
        var options = new DocumentProcessingOptions
        {
            MaxStructuredNodeCount = maximumNodeCount
        };

        // Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new IniDocumentTextExtractor(options));
    }

    [Fact]
    public async Task ExtractAsync_SourceBytesAtLimit_PreservesDocument()
    {
        // Arrange
        const string ini = "[database]\nprovider=SQLite";

        var options = new DocumentProcessingOptions
        {
            MaxDecryptedDocumentBytes = Encoding.UTF8.GetByteCount(ini)
        };

        // Act
        DocumentTextExtractionResult result =
            await ExtractAsync(ini, options);

        // Assert
        Assert.Equal(
            "[database]" + Environment.NewLine +
            "provider: SQLite",
            result.Text);
    }

    [Fact]
    public async Task ExtractAsync_SourceBytesAboveLimit_ThrowsResourceLimitException()
    {
        // Arrange
        const string ini = "[a]\nx=1";

        var options = new DocumentProcessingOptions
        {
            MaxDecryptedDocumentBytes = 6
        };

        // Act
        Task<DocumentTextExtractionResult> extractionTask =
            ExtractAsync(ini, options);

        // Assert
        ResourceLimitExceededException exception =
            await Assert.ThrowsAsync<ResourceLimitExceededException>(
                () => extractionTask);

        Assert.Equal(6L, exception.LimitBytes);
        Assert.Equal(7L, exception.AttemptedBytes);
    }

    [Fact]
    public async Task ExtractAsync_LineLengthAtLimit_PreservesDocument()
    {
        // Arrange
        const string ini = "[a]\nkey=12";

        var options = new DocumentProcessingOptions
        {
            MaxIniLineLength = 6
        };

        // Act
        DocumentTextExtractionResult result =
            await ExtractAsync(ini, options);

        // Assert
        Assert.Equal(
            "[a]" + Environment.NewLine +
            "key: 12",
            result.Text);
    }

    [Fact]
    public async Task ExtractAsync_LineLengthAboveLimit_ThrowsStructureLimitException()
    {
        // Arrange
        const string ini = "[a]\nkey=123";

        var options = new DocumentProcessingOptions
        {
            MaxIniLineLength = 6
        };

        // Act
        Task<DocumentTextExtractionResult> extractionTask =
            ExtractAsync(ini, options);

        // Assert
        DocumentStructureLimitExceededException exception =
            await Assert.ThrowsAsync<DocumentStructureLimitExceededException>(
                () => extractionTask);

        Assert.Equal("INI line length", exception.ResourceName);
        Assert.Equal(6L, exception.Limit);
        Assert.Equal(7L, exception.Attempted);
    }

    [Fact]
    public async Task ExtractAsync_EntryCountAtLimit_PreservesAllEntries()
    {
        // Arrange
        const string ini = "[a]\nx=1\ny=2";

        var options = new DocumentProcessingOptions
        {
            MaxIniEntries = 2
        };

        // Act
        DocumentTextExtractionResult result =
            await ExtractAsync(ini, options);

        // Assert
        Assert.Contains("x: 1", result.Text);
        Assert.Contains("y: 2", result.Text);
    }

    [Fact]
    public async Task ExtractAsync_EntryCountAboveLimit_ThrowsStructureLimitException()
    {
        // Arrange
        const string ini = "[a]\nx=1\ny=2";

        var options = new DocumentProcessingOptions
        {
            MaxIniEntries = 1
        };

        // Act
        Task<DocumentTextExtractionResult> extractionTask =
            ExtractAsync(ini, options);

        // Assert
        DocumentStructureLimitExceededException exception =
            await Assert.ThrowsAsync<DocumentStructureLimitExceededException>(
                () => extractionTask);

        Assert.Equal("INI entries", exception.ResourceName);
        Assert.Equal(1L, exception.Limit);
        Assert.Equal(2L, exception.Attempted);
    }

    [Fact]
    public async Task ExtractAsync_StructuredNodeCountAtLimit_PreservesSectionsAndEntries()
    {
        // Arrange
        const string ini =
            "[alpha]\nx=1\n[beta]\ny=2";

        var options = new DocumentProcessingOptions
        {
            MaxStructuredNodeCount = 4
        };

        // Act
        DocumentTextExtractionResult result =
            await ExtractAsync(ini, options);

        // Assert
        Assert.Contains("[alpha]", result.Text);
        Assert.Contains("x: 1", result.Text);
        Assert.Contains("[beta]", result.Text);
        Assert.Contains("y: 2", result.Text);
    }

    [Fact]
    public async Task ExtractAsync_SectionFanOutAboveStructuredNodeLimit_ThrowsStructureLimitException()
    {
        // Arrange
        const string ini =
            "[alpha]\nx=1\n[beta]\ny=2\n[gamma]";

        var options = new DocumentProcessingOptions
        {
            MaxStructuredNodeCount = 4
        };

        // Act
        Task<DocumentTextExtractionResult> extractionTask =
            ExtractAsync(ini, options);

        // Assert
        DocumentStructureLimitExceededException exception =
            await Assert.ThrowsAsync<DocumentStructureLimitExceededException>(
                () => extractionTask);

        Assert.Equal("INI structure nodes", exception.ResourceName);
        Assert.Equal(4L, exception.Limit);
        Assert.Equal(5L, exception.Attempted);
    }

    [Fact]
    public async Task ExtractAsync_CommentsAndSectionsDoNotCountAsEntries()
    {
        // Arrange
        const string ini =
            """
            ; configuration comment
            [database]
            provider=SQLite
            """;

        var options = new DocumentProcessingOptions
        {
            MaxIniEntries = 1
        };

        // Act
        DocumentTextExtractionResult result =
            await ExtractAsync(ini, options);

        // Assert
        Assert.Contains("provider: SQLite", result.Text);
    }

    private static async Task<DocumentTextExtractionResult> ExtractAsync(
        string ini,
        DocumentProcessingOptions options)
    {
        // Arrange
        await using var stream = new MemoryStream(
            Encoding.UTF8.GetBytes(ini));

        var extractor = new IniDocumentTextExtractor(options);

        // Act
        return await extractor.ExtractAsync(
            stream,
            "document.ini",
            TestContext.Current.CancellationToken);
    }
}
