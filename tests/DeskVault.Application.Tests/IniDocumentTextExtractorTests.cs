using System.Text;
using DeskVault.Application.Documents.Extraction;
using DeskVault.Application.Documents.Extraction.IniDocument;
using IniParser.Exceptions;

namespace DeskVault.Application.Tests;

public sealed class IniDocumentTextExtractorTests
{
    [Theory]
    [InlineData("document.ini")]
    [InlineData("document.INI")]
    [InlineData("document.Ini")]
    public void CanExtract_WhenFileIsIni_ReturnsTrue(string fileName)
    {
        // Arrange
        IniDocumentTextExtractor extractor = new();

        // Act
        bool result = extractor.CanExtract(fileName);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("document.config")]
    [InlineData("document.CONFIG")]
    [InlineData("document.Config")]
    public void CanExtract_WhenFileIsConfig_ReturnsTrue(string fileName)
    {
        // Arrange
        IniDocumentTextExtractor extractor = new();

        // Act
        bool result = extractor.CanExtract(fileName);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("document.txt")]
    [InlineData("document.json")]
    [InlineData("document.ini.txt")]
    public void CanExtract_WhenFileIsNotIniOrConfig_ReturnsFalse(
        string fileName)
    {
        // Arrange
        IniDocumentTextExtractor extractor = new();

        // Act
        bool result = extractor.CanExtract(fileName);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ExtractAsync_WhenSimpleSection_ReturnsStructuredText()
    {
        // Arrange
        const string ini =
            """
            [database]
            provider=SQLite
            encrypted=true
            """;

        const string expected =
            """
            [database]
            provider: SQLite
            encrypted: true
            """;

        IniDocumentTextExtractor extractor = new();

        // Act
        DocumentTextExtractionResult result =
            await ExtractAsync(extractor, ini);

        // Assert
        Assert.Equal(expected, result.Text);
    }

    [Fact]
    public async Task ExtractAsync_WhenGlobalKeysPresent_PreservesGlobalSection()
    {
        // Arrange
        const string ini =
            """
            application=DeskVault
            version=1.0

            [database]
            provider=SQLite
            """;

        const string expected =
            """
            [global]
            application: DeskVault
            version: 1.0

            [database]
            provider: SQLite
            """;

        IniDocumentTextExtractor extractor = new();

        // Act
        DocumentTextExtractionResult result =
            await ExtractAsync(extractor, ini);

        // Assert
        Assert.Equal(expected, result.Text);
    }

    [Fact]
    public async Task ExtractAsync_WhenMultipleSections_PreservesSectionAndKeyOrder()
    {
        // Arrange
        const string ini =
            """
            [z]
            third=3

            [a]
            first=1

            [m]
            second=2
            """;

        const string expected =
            """
            [z]
            third: 3

            [a]
            first: 1

            [m]
            second: 2
            """;

        IniDocumentTextExtractor extractor = new();

        // Act
        DocumentTextExtractionResult result =
            await ExtractAsync(extractor, ini);

        // Assert
        Assert.Equal(expected, result.Text);
    }

    [Fact]
    public async Task ExtractAsync_WhenEmptyValue_PreservesEmptyValue()
    {
        // Arrange
        const string ini =
            """
            [database]
            connection=
            """;

        const string expected =
            """
            [database]
            connection:
            """;

        IniDocumentTextExtractor extractor = new();

        // Act
        DocumentTextExtractionResult result =
            await ExtractAsync(extractor, ini);

        // Assert
        Assert.Equal(expected, result.Text);
    }

    [Fact]
    public async Task ExtractAsync_WhenQuotedValues_PreservesValueText()
    {
        // Arrange
        const string ini =
            """
            [database]
            connection="Data Source=desk.db"
            path='C:\DeskVault'
            """;

        const string expected =
            """
            [database]
            connection: "Data Source=desk.db"
            path: 'C:\DeskVault'
            """;

        IniDocumentTextExtractor extractor = new();

        // Act
        DocumentTextExtractionResult result =
            await ExtractAsync(extractor, ini);

        // Assert
        Assert.Equal(expected, result.Text);
    }

    [Fact]
    public async Task ExtractAsync_WhenInputIsSame_ReturnsSameText()
    {
        // Arrange
        const string ini =
            """
            [database]
            provider=SQLite
            encrypted=true
            """;

        IniDocumentTextExtractor extractor = new();

        // Act
        DocumentTextExtractionResult first =
            await ExtractAsync(extractor, ini);

        DocumentTextExtractionResult second =
            await ExtractAsync(extractor, ini);

        // Assert
        Assert.Equal(first.Text, second.Text);
    }

    [Fact]
    public async Task ExtractAsync_WhenDuplicateKey_ThrowsParsingException()
    {
        // Arrange
        const string ini =
            """
            [database]
            provider=SQLite
            provider=PostgreSQL
            """;

        IniDocumentTextExtractor extractor = new();

        // Act
        Task<DocumentTextExtractionResult> extractionTask =
            ExtractAsync(extractor, ini);

        // Assert
        await Assert.ThrowsAnyAsync<ParsingException>(
            () => extractionTask);
    }

    [Fact]
    public async Task ExtractAsync_WhenDuplicateSection_ThrowsParsingException()
    {
        // Arrange
        const string ini =
            """
            [database]
            provider=SQLite

            [database]
            encrypted=true
            """;

        IniDocumentTextExtractor extractor = new();

        // Act
        Task<DocumentTextExtractionResult> extractionTask =
            ExtractAsync(extractor, ini);

        // Assert
        await Assert.ThrowsAnyAsync<ParsingException>(
            () => extractionTask);
    }

    [Fact]
    public async Task ExtractAsync_WhenMalformedInput_ThrowsParsingException()
    {
        // Arrange
        const string ini =
            """
            [database
            provider=SQLite
            """;

        IniDocumentTextExtractor extractor = new();

        // Act
        Task<DocumentTextExtractionResult> extractionTask =
            ExtractAsync(extractor, ini);

        // Assert
        await Assert.ThrowsAnyAsync<ParsingException>(
            () => extractionTask);
    }

    [Fact]
    public async Task ExtractAsync_WhenCancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        const string ini =
            """
            [database]
            provider=SQLite
            """;

        IniDocumentTextExtractor extractor = new();

        using CancellationTokenSource cancellationTokenSource = new();
        cancellationTokenSource.Cancel();

        // Act
        Task<DocumentTextExtractionResult> extractionTask =
            ExtractAsync(
                extractor,
                ini,
                cancellationTokenSource.Token);

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => extractionTask);
    }

    private static async Task<DocumentTextExtractionResult> ExtractAsync(
        IniDocumentTextExtractor extractor,
        string ini,
        CancellationToken cancellationToken = default)
    {
        // Arrange
        await using MemoryStream stream =
            new(Encoding.UTF8.GetBytes(ini));

        // Act
        return await extractor.ExtractAsync(
            stream,
            "document.ini",
            cancellationToken);
    }
}
