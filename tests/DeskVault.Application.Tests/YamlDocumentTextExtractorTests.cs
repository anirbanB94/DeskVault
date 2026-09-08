using System.Text;
using DeskVault.Application.Documents.Extraction;
using DeskVault.Application.Documents.Extraction.YamlDocument;

namespace DeskVault.Application.Tests;

public sealed class YamlDocumentTextExtractorTests
{
    [Theory]
    [InlineData("document.yaml")]
    [InlineData("document.YAML")]
    [InlineData("document.Yaml")]
    [InlineData("document.yMl")]
    public void CanExtract_WhenFileIsYaml_ReturnsTrue(string fileName)
    {
        // Arrange
        YamlDocumentTextExtractor extractor = new();

        // Act
        bool result = extractor.CanExtract(fileName);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("document.yml")]
    [InlineData("document.YML")]
    [InlineData("document.Yml")]
    [InlineData("document.yMl")]
    public void CanExtract_WhenFileIsYml_ReturnsTrue(string fileName)
    {
        // Arrange
        YamlDocumentTextExtractor extractor = new();

        // Act
        bool result = extractor.CanExtract(fileName);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("document.txt")]
    [InlineData("document.json")]
    [InlineData("document.yaml.txt")]
    public void CanExtract_WhenFileIsNotYaml_ReturnsFalse(string fileName)
    {
        // Arrange
        YamlDocumentTextExtractor extractor = new();

        // Act
        bool result = extractor.CanExtract(fileName);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ExtractAsync_WhenSimpleMapping_ReturnsDeterministicText()
    {
        // Arrange
        const string yaml =
            """
            name: DeskVault
            version: 1.0
            enabled: true
            """;

        const string expected =
            """
            name: DeskVault
            version: 1.0
            enabled: true
            """;

        YamlDocumentTextExtractor extractor = new();

        // Act
        DocumentTextExtractionResult result =
            await ExtractAsync(extractor, yaml);

        // Assert
        Assert.Equal(expected, result.Text);
    }

    [Fact]
    public async Task ExtractAsync_WhenNestedMapping_PreservesStructure()
    {
        // Arrange
        const string yaml =
            """
            database:
              provider: SQLite
              encrypted: true
            """;

        const string expected =
            """
            database:
              provider: SQLite
              encrypted: true
            """;

        YamlDocumentTextExtractor extractor = new();

        // Act
        DocumentTextExtractionResult result =
            await ExtractAsync(extractor, yaml);

        // Assert
        Assert.Equal(expected, result.Text);
    }

    [Fact]
    public async Task ExtractAsync_WhenSequence_PreservesSequenceStructure()
    {
        // Arrange
        const string yaml =
            """
            features:
              - search
              - extraction
            """;

        const string expected =
            """
            features:
              [0]: search
              [1]: extraction
            """;

        YamlDocumentTextExtractor extractor = new();

        // Act
        DocumentTextExtractionResult result =
            await ExtractAsync(extractor, yaml);

        // Assert
        Assert.Equal(expected, result.Text);
    }

    [Fact]
    public async Task ExtractAsync_WhenNestedMappingAndSequence_PreservesStructure()
    {
        // Arrange
        const string yaml =
            """
            documents:
              - name: A
                type: txt
              - name: B
                type: json
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

        YamlDocumentTextExtractor extractor = new();

        // Act
        DocumentTextExtractionResult result =
            await ExtractAsync(extractor, yaml);

        // Assert
        Assert.Equal(expected, result.Text);
    }

    [Fact]
    public async Task ExtractAsync_WhenEmptyCollections_PreservesCollections()
    {
        // Arrange
        const string yaml =
            """
            settings: {}
            features: []
            """;

        const string expected =
            """
            settings:
            features:
            """;

        YamlDocumentTextExtractor extractor = new();

        // Act
        DocumentTextExtractionResult result =
            await ExtractAsync(extractor, yaml);

        // Assert
        Assert.Equal(expected, result.Text);
    }

    [Fact]
    public async Task ExtractAsync_WhenMultipleDocuments_PreservesDocumentOrder()
    {
        // Arrange
        const string yaml =
            """
            ---
            name: first
            ---
            name: second
            """;

        const string expected =
            """
            [document 0]:
              name: first
            [document 1]:
              name: second
            """;

        YamlDocumentTextExtractor extractor = new();

        // Act
        DocumentTextExtractionResult result =
            await ExtractAsync(extractor, yaml);

        // Assert
        Assert.Equal(expected, result.Text);
    }

    [Fact]
    public async Task ExtractAsync_WhenScalarRoot_ReturnsScalarText()
    {
        // Arrange
        const string yaml = "DeskVault";

        const string expected = "DeskVault";

        YamlDocumentTextExtractor extractor = new();

        // Act
        DocumentTextExtractionResult result =
            await ExtractAsync(extractor, yaml);

        // Assert
        Assert.Equal(expected, result.Text);
    }

    [Fact]
    public async Task ExtractAsync_WhenNullScalar_PreservesNull()
    {
        // Arrange
        const string yaml =
            """
            description: null
            """;

        const string expected =
            """
            description: null
            """;

        YamlDocumentTextExtractor extractor = new();

        // Act
        DocumentTextExtractionResult result =
            await ExtractAsync(extractor, yaml);

        // Assert
        Assert.Equal(expected, result.Text);
    }

    [Fact]
    public async Task ExtractAsync_WhenAnchorsAndAliasesArePresent_PreservesMeaningfulStructure()
    {
        // Arrange
        const string yaml =
            """
            defaults: &defaults
              provider: SQLite
              encrypted: true

            database:
              <<: *defaults
            """;

        const string expected =
            """
            defaults:
              provider: SQLite
              encrypted: true
            database:
              <<:
                provider: SQLite
                encrypted: true
            """;

        YamlDocumentTextExtractor extractor = new();

        // Act
        DocumentTextExtractionResult result =
            await ExtractAsync(extractor, yaml);

        // Assert
        Assert.Equal(expected, result.Text);
    }

    [Fact]
    public async Task ExtractAsync_WhenInputIsSame_ReturnsSameText()
    {
        // Arrange
        const string yaml =
            """
            name: DeskVault
            features:
              - search
              - extraction
            """;

        YamlDocumentTextExtractor extractor = new();

        // Act
        DocumentTextExtractionResult first =
            await ExtractAsync(extractor, yaml);

        DocumentTextExtractionResult second =
            await ExtractAsync(extractor, yaml);

        // Assert
        Assert.Equal(first.Text, second.Text);
    }

    [Fact]
    public async Task ExtractAsync_WhenYamlIsMalformed_ThrowsYamlException()
    {
        // Arrange
        const string yaml =
            """
            database:
              provider: SQLite
               encrypted: true
            """;

        YamlDocumentTextExtractor extractor = new();

        // Act
        Task<DocumentTextExtractionResult> extractionTask =
            ExtractAsync(extractor, yaml);

        // Assert
        await Assert.ThrowsAnyAsync<YamlDotNet.Core.YamlException>(
            () => extractionTask);
    }

    [Fact]
    public async Task ExtractAsync_WhenCancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        const string yaml =
            """
            name: DeskVault
            """;

        YamlDocumentTextExtractor extractor = new();

        using CancellationTokenSource cancellationTokenSource = new();
        cancellationTokenSource.Cancel();

        // Act
        Task<DocumentTextExtractionResult> extractionTask =
            ExtractAsync(
                extractor,
                yaml,
                cancellationTokenSource.Token);

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => extractionTask);
    }

    private static async Task<DocumentTextExtractionResult> ExtractAsync(
        YamlDocumentTextExtractor extractor,
        string yaml,
        CancellationToken cancellationToken = default)
    {
        // Arrange
        await using MemoryStream stream =
            new(Encoding.UTF8.GetBytes(yaml));

        // Act
        return await extractor.ExtractAsync(
            stream,
            "document.yaml",
            cancellationToken);
    }
}
