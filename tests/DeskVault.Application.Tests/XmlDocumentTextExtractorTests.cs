using System.Text;
using DeskVault.Application.Documents.Extraction;
using DeskVault.Application.Documents.Extraction.XmlDocument;

namespace DeskVault.Application.Tests;

public sealed class XmlDocumentTextExtractorTests
{
    [Theory]
    [InlineData("document.xml")]
    [InlineData("document.XML")]
    [InlineData("document.Xml")]
    [InlineData("document.xMl")]
    public void CanExtract_WhenFileIsXml_ReturnsTrue(string fileName)
    {
        // Arrange
        XmlDocumentTextExtractor extractor = new();

        // Act
        bool result = extractor.CanExtract(fileName);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("document.txt")]
    [InlineData("document.json")]
    [InlineData("document.xml.txt")]
    public void CanExtract_WhenFileIsNotXml_ReturnsFalse(string fileName)
    {
        // Arrange
        XmlDocumentTextExtractor extractor = new();

        // Act
        bool result = extractor.CanExtract(fileName);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ExtractAsync_WhenSimpleDocument_ReturnsDeterministicText()
    {
        // Arrange
        const string xml =
            """
            <configuration>
              <environment>Production</environment>
              <enabled>true</enabled>
            </configuration>
            """;

        const string expected =
            """
            configuration:
              environment: Production
              enabled: true
            """;

        XmlDocumentTextExtractor extractor = new();

        // Act
        DocumentTextExtractionResult result =
            await ExtractAsync(extractor, xml);

        // Assert
        Assert.Equal(expected, result.Text);
    }

    [Fact]
    public async Task ExtractAsync_WhenNestedElements_PreservesStructure()
    {
        // Arrange
        const string xml =
            """
            <configuration>
              <database>
                <provider>SQLite</provider>
                <encrypted>true</encrypted>
              </database>
            </configuration>
            """;

        const string expected =
            """
            configuration:
              database:
                provider: SQLite
                encrypted: true
            """;

        XmlDocumentTextExtractor extractor = new();

        // Act
        DocumentTextExtractionResult result =
            await ExtractAsync(extractor, xml);

        // Assert
        Assert.Equal(expected, result.Text);
    }

    [Fact]
    public async Task ExtractAsync_WhenAttributesArePresent_PreservesAttributes()
    {
        // Arrange
        const string xml =
            """
            <database provider="SQLite" encrypted="true">
              <name>DeskVault</name>
            </database>
            """;

        const string expected =
            """
            database:
              @provider: SQLite
              @encrypted: true
              name: DeskVault
            """;

        XmlDocumentTextExtractor extractor = new();

        // Act
        DocumentTextExtractionResult result =
            await ExtractAsync(extractor, xml);

        // Assert
        Assert.Equal(expected, result.Text);
    }

    [Fact]
    public async Task ExtractAsync_WhenRepeatedElements_PreservesElementOrder()
    {
        // Arrange
        const string xml =
            """
            <configuration>
              <tag>security</tag>
              <tag>documents</tag>
              <tag>search</tag>
            </configuration>
            """;

        const string expected =
            """
            configuration:
              tag: security
              tag: documents
              tag: search
            """;

        XmlDocumentTextExtractor extractor = new();

        // Act
        DocumentTextExtractionResult result =
            await ExtractAsync(extractor, xml);

        // Assert
        Assert.Equal(expected, result.Text);
    }

    [Fact]
    public async Task ExtractAsync_WhenEmptyElement_PreservesElement()
    {
        // Arrange
        const string xml =
            """
            <configuration>
              <description />
            </configuration>
            """;

        const string expected =
            """
            configuration:
              description:
            """;

        XmlDocumentTextExtractor extractor = new();

        // Act
        DocumentTextExtractionResult result =
            await ExtractAsync(extractor, xml);

        // Assert
        Assert.Equal(expected, result.Text);
    }

    [Fact]
    public async Task ExtractAsync_WhenInputIsSame_ReturnsSameText()
    {
        // Arrange
        const string xml =
            """
            <configuration>
              <database provider="SQLite">
                <encrypted>true</encrypted>
              </database>
            </configuration>
            """;

        XmlDocumentTextExtractor extractor = new();

        // Act
        DocumentTextExtractionResult first =
            await ExtractAsync(extractor, xml);

        DocumentTextExtractionResult second =
            await ExtractAsync(extractor, xml);

        // Assert
        Assert.Equal(first.Text, second.Text);
    }

    [Fact]
    public async Task ExtractAsync_WhenXmlIsMalformed_ThrowsXmlException()
    {
        // Arrange
        const string xml =
            """
            <configuration>
              <database>
            """;

        XmlDocumentTextExtractor extractor = new();

        // Act
        Task<DocumentTextExtractionResult> extractionTask =
            ExtractAsync(extractor, xml);

        // Assert
        await Assert.ThrowsAnyAsync<System.Xml.XmlException>(
            () => extractionTask);
    }

    [Fact]
    public async Task ExtractAsync_WhenCancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        const string xml =
            """
            <configuration>
              <name>DeskVault</name>
            </configuration>
            """;

        XmlDocumentTextExtractor extractor = new();

        using CancellationTokenSource cancellationTokenSource = new();
        cancellationTokenSource.Cancel();

        // Act
        Task<DocumentTextExtractionResult> extractionTask =
            ExtractAsync(
                extractor,
                xml,
                cancellationTokenSource.Token);

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => extractionTask);
    }

    private static async Task<DocumentTextExtractionResult> ExtractAsync(
        XmlDocumentTextExtractor extractor,
        string xml,
        CancellationToken cancellationToken = default)
    {
        // Arrange
        await using MemoryStream stream =
            new(Encoding.UTF8.GetBytes(xml));

        // Act
        return await extractor.ExtractAsync(
            stream,
            "document.xml",
            cancellationToken);
    }
}
