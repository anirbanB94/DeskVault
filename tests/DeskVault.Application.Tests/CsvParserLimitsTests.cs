using DeskVault.Application.Documents.Parsing.Csv;

namespace DeskVault.Application.Tests;

public sealed class CsvParserLimitsTests
{
    [Fact]
    public async Task ParseAsync_MaxRows_LimitsMaterializedRowsAndReportsMoreRows()
    {
        const string csv =
            """
            Id,Name
            1,Alice
            2,Bob
            3,Charlie
            4,Diana
            5,Ethan
            """;

        CsvDocument document =
            await ParseAsync(
                csv,
                maxRows: 3);

        Assert.Equal(3, document.Rows.Count);

        Assert.Equal(
            ["1", "2", "3"],
            document.Rows.Select(row => row[0]).ToArray());

        Assert.Equal(
            ["Alice", "Bob", "Charlie"],
            document.Rows.Select(row => row[1]).ToArray());

        Assert.True(document.HasMoreRows);
    }

    [Fact]
    public async Task ParseAsync_MaxRowsGreaterThanDocumentSize_PreservesAllRowsAndReportsNoMoreRows()
    {
        const string csv =
            """
            Id,Name
            1,Alice
            2,Bob
            3,Charlie
            """;

        CsvDocument document =
            await ParseAsync(
                csv,
                maxRows: 10);

        Assert.Equal(3, document.Rows.Count);
        Assert.False(document.HasMoreRows);
        Assert.Empty(document.Warnings);
    }

    [Fact]
    public async Task ParseAsync_NullMaxRows_PreservesAllRowsAndReportsNoMoreRows()
    {
        const string csv =
            """
            Id,Name
            1,Alice
            2,Bob
            3,Charlie
            """;

        CsvDocument document =
            await ParseAsync(csv);

        Assert.Equal(3, document.Rows.Count);
        Assert.False(document.HasMoreRows);
    }

    [Fact]
    public async Task ParseAsync_MaxRowsExactlyMatchesDocumentSize_ReportsNoMoreRows()
    {
        const string csv =
            """
            Id,Name
            1,Alice
            2,Bob
            3,Charlie
            """;

        CsvDocument document =
            await ParseAsync(
                csv,
                maxRows: 3);

        Assert.Equal(3, document.Rows.Count);
        Assert.False(document.HasMoreRows);
    }

    [Fact]
    public async Task ParseAsync_MaxRowsZero_MaterializesNoRowsAndReportsMoreRows()
    {
        const string csv =
            """
            Id,Name
            1,Alice
            2,Bob
            """;

        CsvDocument document =
            await ParseAsync(
                csv,
                maxRows: 0);

        Assert.Empty(document.Rows);
        Assert.True(document.HasMoreRows);
        Assert.Equal(2, document.Columns.Count);
    }

    private static async Task<CsvDocument> ParseAsync(
        string csv,
        int? maxRows = null)
    {
        await using var stream =
            new MemoryStream(
                System.Text.Encoding.UTF8.GetBytes(csv));

        var parser =
            new CsvDocumentParser(
                new CsvParsingOptions
                {
                    MaxRows = maxRows
                });

        return await parser.ParseAsync(stream);
    }
}
