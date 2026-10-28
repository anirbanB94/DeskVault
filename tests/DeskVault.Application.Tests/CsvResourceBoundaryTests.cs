
using CsvHelper;
using DeskVault.Application.Configurations;
using DeskVault.Application.Documents.Extraction;
using DeskVault.Application.Documents.Extraction.CSVDocument;
using DeskVault.Application.Documents.Parsing.Csv;
using DeskVault.Application.Documents.Processing;
using System.Text;

namespace DeskVault.Application.Tests;

public sealed class CsvResourceBoundaryTests
{
    [Theory]
    [InlineData("abc", 4)]  // Below the limit.
    [InlineData("abcd", 4)] // At the limit.
    public async Task ParseAsync_FieldLengthAtOrBelowLimit_PreservesCompleteValue(
        string fieldValue,
        int maximumFieldLength)
    {
        // Arrange
        string csv = CreateCsv("Id", fieldValue);

        var options = new DocumentProcessingOptions
        {
            MaxCsvFieldLength = maximumFieldLength
        };

        // Act
        CsvDocument document = await ParseAsync(csv, options);

        // Assert
        IReadOnlyList<string> row = Assert.Single(document.Rows);

        Assert.Equal(fieldValue, row[0]);
        Assert.False(document.HasMoreRows);
    }

    [Theory]
    [InlineData("abcde")]
    [InlineData("\"abcde\"")]
    public async Task ParseAsync_FieldLengthAboveLimit_ThrowsStructureLimitException(
        string fieldValue)
    {
        // Arrange
        string csv = CreateCsv("Id", fieldValue);

        var options = new DocumentProcessingOptions
        {
            MaxCsvFieldLength = 4
        };

        // Act
        Task<CsvDocument> parseTask = ParseAsync(csv, options);

        // Assert
        DocumentStructureLimitExceededException exception =
            await Assert.ThrowsAsync<DocumentStructureLimitExceededException>(
                () => parseTask);

        Assert.Equal("CSV field length", exception.ResourceName);
        Assert.Equal(4L, exception.Limit);
        Assert.Equal(5L, exception.Attempted);
    }

    [Theory]
    [InlineData(2)] // At the limit.
    [InlineData(3)] // Below the limit.
    public async Task ParseAsync_ColumnCountAtOrBelowLimit_PreservesCompleteDocument(
        int maximumColumns)
    {
        // Arrange
        string csv = CreateCsv("A,B", "1,2");

        var options = new DocumentProcessingOptions
        {
            MaxCsvColumns = maximumColumns
        };

        // Act
        CsvDocument document = await ParseAsync(csv, options);

        // Assert
        Assert.Equal(2, document.Columns.Count);

        IReadOnlyList<string> row = Assert.Single(document.Rows);

        Assert.Equal(new[] { "1", "2" }, row);
        Assert.False(document.HasMoreRows);
    }

    [Fact]
    public async Task ParseAsync_ColumnCountAboveLimit_ThrowsStructureLimitException()
    {
        // Arrange
        string csv = CreateCsv("A,B,C", "1,2,3");

        var options = new DocumentProcessingOptions
        {
            MaxCsvColumns = 2
        };

        // Act
        Task<CsvDocument> parseTask = ParseAsync(csv, options);

        // Assert
        DocumentStructureLimitExceededException exception =
            await Assert.ThrowsAsync<DocumentStructureLimitExceededException>(
                () => parseTask);

        Assert.Equal("CSV columns", exception.ResourceName);
        Assert.Equal(2L, exception.Limit);
        Assert.Equal(3L, exception.Attempted);
    }

    [Theory]
    [InlineData(4)] // At the limit.
    [InlineData(5)] // Below the limit.
    public async Task ParseAsync_TotalCellsAtOrBelowLimit_PreservesCompleteDocument(
        int maximumCells)
    {
        // Arrange
        string csv = CreateCsv("A,B", "1,2");

        var options = new DocumentProcessingOptions
        {
            MaxCsvCells = maximumCells
        };

        // Act
        CsvDocument document = await ParseAsync(csv, options);

        // Assert
        Assert.Equal(2, document.Columns.Count);

        IReadOnlyList<string> row = Assert.Single(document.Rows);

        Assert.Equal(new[] { "1", "2" }, row);
        Assert.False(document.HasMoreRows);
    }

    [Fact]
    public async Task ParseAsync_TotalCellsAboveLimit_ThrowsStructureLimitException()
    {
        // Arrange
        string csv = CreateCsv("A,B", "1,2");

        var options = new DocumentProcessingOptions
        {
            MaxCsvCells = 3
        };

        // Act
        Task<CsvDocument> parseTask = ParseAsync(csv, options);

        // Assert
        DocumentStructureLimitExceededException exception =
            await Assert.ThrowsAsync<DocumentStructureLimitExceededException>(
                () => parseTask);

        Assert.Equal("CSV cells", exception.ResourceName);
        Assert.Equal(3L, exception.Limit);
        Assert.Equal(4L, exception.Attempted);
    }

    [Fact]
    public async Task ExtractAsync_SourceBytesAtLimit_PreservesCompleteDocument()
    {
        // Arrange
        const string csv = "Id\n1";

        var options = new DocumentProcessingOptions
        {
            MaxDecryptedDocumentBytes =
                Encoding.UTF8.GetByteCount(csv)
        };

        // Act
        DocumentTextExtractionResult result =
            await ExtractAsync(csv, options);

        // Assert
        Assert.Equal("Id: 1", result.Text);
    }

    [Fact]
    public async Task ExtractAsync_SourceBytesAboveLimit_ThrowsResourceLimitException()
    {
        // Arrange
        const string csv = "Id\n12";
        const int maximumBytes = 4;

        var options = new DocumentProcessingOptions
        {
            MaxDecryptedDocumentBytes = maximumBytes
        };

        // Act
        Task<DocumentTextExtractionResult> extractionTask =
            ExtractAsync(csv, options);

        // Assert
        ResourceLimitExceededException exception =
            await Assert.ThrowsAsync<ResourceLimitExceededException>(
                () => extractionTask);

        Assert.Equal(4L, exception.LimitBytes);
        Assert.Equal(5L, exception.AttemptedBytes);
    }

    [Theory]
    [InlineData(2)] // At the limit.
    [InlineData(3)] // Below the limit.
    public async Task ExtractAsync_RowCountAtOrBelowLimit_ReturnsCompleteText(
        int maximumRows)
    {
        // Arrange
        string csv = CreateCsv("Id", "1", "2");

        var options = new DocumentProcessingOptions
        {
            MaxCsvRows = maximumRows
        };

        string expectedText = CreateCsv(
            "Id: 1",
            string.Empty,
            "Id: 2");

        // Act
        DocumentTextExtractionResult result =
            await ExtractAsync(csv, options);

        // Assert
        Assert.Equal(expectedText, result.Text);
    }

    [Fact]
    public async Task ExtractAsync_RowCountAboveLimit_RejectsPartialRepresentation()
    {
        // Arrange
        string csv = CreateCsv("Id", "1", "2", "3");

        var options = new DocumentProcessingOptions
        {
            MaxCsvRows = 2
        };

        // Act
        Task<DocumentTextExtractionResult> extractionTask =
            ExtractAsync(csv, options);

        // Assert
        DocumentStructureLimitExceededException exception =
            await Assert.ThrowsAsync<DocumentStructureLimitExceededException>(
                () => extractionTask);

        Assert.Equal("CSV data rows", exception.ResourceName);
        Assert.Equal(2L, exception.Limit);
        Assert.Equal(3L, exception.Attempted);
    }

    [Fact]
    public async Task ParseAsync_FieldLengthCounterResetsForEachRecord()
    {
        // Arrange
        const string csv = "a,12345\n12345,b";

        var processingOptions = new DocumentProcessingOptions
        {
            MaxCsvFieldLength = 5
        };

        var parser = new CsvDocumentParser(
            new CsvParsingOptions
            {
                MaxRows = null
            },
            processingOptions: processingOptions);

        await using var stream = new MemoryStream(
            System.Text.Encoding.UTF8.GetBytes(csv));

        // Act
        CsvDocument document = await parser.ParseAsync(
            stream,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, document.Columns.Count);
        Assert.Single(document.Rows);
        Assert.Equal("12345", document.Rows[0][0]);
        Assert.Equal("b", document.Rows[0][1]);
        Assert.False(document.HasMoreRows);
    }

    private static string CreateCsv(params string[] records)
    {
        return string.Join(Environment.NewLine, records);
    }

    private static async Task<CsvDocument> ParseAsync(
        string csv,
        DocumentProcessingOptions options)
    {
        // Arrange
        await using var stream = CreateStream(csv);

        var parser = new CsvDocumentParser(
            new CsvParsingOptions
            {
                MaxRows = null
            },
            options);

        // Act
        return await parser.ParseAsync(
            stream,
            TestContext.Current.CancellationToken);
    }

    private static async Task<DocumentTextExtractionResult> ExtractAsync(
        string csv,
        DocumentProcessingOptions options)
    {
        // Arrange
        await using var stream = CreateStream(csv);

        var extractor = new CsvDocumentTextExtractor(options);

        // Act
        return await extractor.ExtractAsync(
            stream,
            "document.csv",
            TestContext.Current.CancellationToken);
    }

    private static MemoryStream CreateStream(string csv)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(csv));
    }
}
