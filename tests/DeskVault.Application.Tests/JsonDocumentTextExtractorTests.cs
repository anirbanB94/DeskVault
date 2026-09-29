using System.Text;
using DeskVault.Application.Configurations;
using DeskVault.Application.Documents.Extraction;
using DeskVault.Application.Documents.Extraction.JsonDocument;
using DeskVault.Application.Documents.Processing;

namespace DeskVault.Application.Tests;

public sealed class JsonDocumentTextExtractorTests
{
    [Theory]
    [InlineData("document.json")]
    [InlineData("document.JSON")]
    [InlineData("document.Json")]
    [InlineData("document.jSoN")]
    public void CanExtract_WhenFileIsJson_ReturnsTrue(
        string fileName)
    {
        // Arrange
        JsonDocumentTextExtractor extractor =
            new();

        // Act
        bool result =
            extractor.CanExtract(fileName);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("document.txt")]
    [InlineData("document.xml")]
    [InlineData("document.json.txt")]
    public void CanExtract_WhenFileIsNotJson_ReturnsFalse(
        string fileName)
    {
        // Arrange
        JsonDocumentTextExtractor extractor =
            new();

        // Act
        bool result =
            extractor.CanExtract(fileName);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ExtractAsync_WhenSimpleObject_ReturnsDeterministicText()
    {
        // Arrange
        const string json =
            """
            {
              "name": "DeskVault",
              "version": 1.0,
              "enabled": true
            }
            """;

        const string expected =
            """
            name: DeskVault
            version: 1.0
            enabled: true
            """;

        JsonDocumentTextExtractor extractor =
            new();

        // Act
        DocumentTextExtractionResult result =
            await ExtractAsync(
                extractor,
                json);

        // Assert
        Assert.Equal(
            expected,
            result.Text);
    }

    [Fact]
    public async Task ExtractAsync_WhenNestedObject_PreservesStructure()
    {
        // Arrange
        const string json =
            """
            {
              "database": {
                "provider": "SQLite",
                "encrypted": true
              }
            }
            """;

        const string expected =
            """
            database:
              provider: SQLite
              encrypted: true
            """;

        JsonDocumentTextExtractor extractor =
            new();

        // Act
        DocumentTextExtractionResult result =
            await ExtractAsync(
                extractor,
                json);

        // Assert
        Assert.Equal(
            expected,
            result.Text);
    }

    [Fact]
    public async Task ExtractAsync_WhenArray_PreservesArrayStructure()
    {
        // Arrange
        const string json =
            """
            {
              "tags": [
                "security",
                "documents"
              ]
            }
            """;

        const string expected =
            """
            tags:
              [0]: security
              [1]: documents
            """;

        JsonDocumentTextExtractor extractor =
            new();

        // Act
        DocumentTextExtractionResult result =
            await ExtractAsync(
                extractor,
                json);

        // Assert
        Assert.Equal(
            expected,
            result.Text);
    }

    [Fact]
    public async Task ExtractAsync_WhenNestedArrayObjects_PreservesStructure()
    {
        // Arrange
        const string json =
            """
            {
              "documents": [
                {
                  "name": "A",
                  "type": "txt"
                },
                {
                  "name": "B",
                  "type": "json"
                }
              ]
            }
            """;

        const string expected =
            """
            documents:
              [0]:
                name: A
                type: txt
              [1]:
                name: B
                type: json
            """;

        JsonDocumentTextExtractor extractor =
            new();

        // Act
        DocumentTextExtractionResult result =
            await ExtractAsync(
                extractor,
                json);

        // Assert
        Assert.Equal(
            expected,
            result.Text);
    }

    [Fact]
    public async Task ExtractAsync_WhenValueIsNull_PreservesNull()
    {
        // Arrange
        const string json =
            """
            {
              "description": null
            }
            """;

        const string expected =
            """
            description: null
            """;

        JsonDocumentTextExtractor extractor =
            new();

        // Act
        DocumentTextExtractionResult result =
            await ExtractAsync(
                extractor,
                json);

        // Assert
        Assert.Equal(
            expected,
            result.Text);
    }

    [Fact]
    public async Task ExtractAsync_WhenInputIsSame_ReturnsSameText()
    {
        // Arrange
        const string json =
            """
            {
              "name": "DeskVault",
              "items": [
                "one",
                "two"
              ]
            }
            """;

        JsonDocumentTextExtractor extractor =
            new();

        // Act
        DocumentTextExtractionResult first =
            await ExtractAsync(
                extractor,
                json);

        DocumentTextExtractionResult second =
            await ExtractAsync(
                extractor,
                json);

        // Assert
        Assert.Equal(
            first.Text,
            second.Text);
    }

    [Fact]
    public async Task ExtractAsync_WhenJsonIsMalformed_ThrowsJsonException()
    {
        // Arrange
        const string json =
            """
            {
              "name": "DeskVault"
            """;

        JsonDocumentTextExtractor extractor =
            new();

        // Act
        Task<DocumentTextExtractionResult> extractionTask =
            ExtractAsync(
                extractor,
                json);

        // Assert
        await Assert.ThrowsAnyAsync<System.Text.Json.JsonException>(
            () => extractionTask);
    }

    [Fact]
    public async Task ExtractAsync_WhenCancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        const string json =
            """
            {
              "name": "DeskVault"
            }
            """;

        JsonDocumentTextExtractor extractor =
            new();

        using CancellationTokenSource cancellationTokenSource =
            new();

        cancellationTokenSource.Cancel();

        // Act
        Task<DocumentTextExtractionResult> extractionTask =
            ExtractAsync(
                extractor,
                json,
                cancellationTokenSource.Token);

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => extractionTask);
    }

    [Fact]
    public async Task ExtractAsync_WhenProcessedTextExceedsLimit_ThrowsResourceLimitExceededException()
    {
        // Arrange
        const string json =
            """
            {
              "name": "DeskVault"
            }
            """;

        var extractor =
            new JsonDocumentTextExtractor(
                new DocumentProcessingOptions
                {
                    MaxProcessedTextBytes = 10
                });

        // Act
        Task<DocumentTextExtractionResult> extractionTask =
            ExtractAsync(
                extractor,
                json);

        // Assert
        ResourceLimitExceededException exception =
            await Assert.ThrowsAsync<ResourceLimitExceededException>(
                () => extractionTask);

        Assert.Equal(
            10,
            exception.LimitBytes);

        Assert.True(
            exception.AttemptedBytes >
            exception.LimitBytes);
    }

    [Fact]
    public async Task ExtractAsync_WhenProcessedTextIsWithinLimit_ReturnsCompleteText()
    {
        // Arrange
        const string json =
            """
            {
              "name": "DeskVault"
            }
            """;

        const string expected =
            """
            name: DeskVault
            """;

        long maximumProcessedTextBytes =
            Encoding.UTF8.GetByteCount(
                expected +
                Environment.NewLine);

        var extractor =
            new JsonDocumentTextExtractor(
                new DocumentProcessingOptions
                {
                    MaxProcessedTextBytes =
                        maximumProcessedTextBytes
                });

        // Act
        DocumentTextExtractionResult result =
            await ExtractAsync(
                extractor,
                json);

        // Assert
        Assert.Equal(
            expected,
            result.Text);
    }

    private static async Task<DocumentTextExtractionResult> ExtractAsync(
        JsonDocumentTextExtractor extractor,
        string json,
        CancellationToken cancellationToken = default)
    {
        // Arrange
        await using MemoryStream stream =
            new(
                Encoding.UTF8.GetBytes(
                    json));

        // Act
        return await extractor.ExtractAsync(
            stream,
            "document.json",
            cancellationToken);
    }
}
