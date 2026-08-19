using DeskVault.UI.Rendering.CsvDocumentRendering;
using Microsoft.Extensions.Options;

namespace DeskVault.UI.Tests;

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

        using var stream =
            new MemoryStream(
                System.Text.Encoding.UTF8.GetBytes(csv));

        var parser =
            new CsvDocumentParser(
                Options.Create(
                    new CsvParsingOptions
                    {
                        MaxRows = 3
                    }));

        CsvDocument document =
            await parser.ParseAsync(stream);

        Assert.Equal(3, document.Rows.Count);

        Assert.Equal(
            "1",
            document.Rows[0][0]);

        Assert.Equal(
            "3",
            document.Rows[2][0]);

        Assert.Equal(
            "Alice",
            document.Rows[0][1]);

        Assert.Equal(
            "Charlie",
            document.Rows[2][1]);

        Assert.True(
            document.HasMoreRows);
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

        using var stream =
            new MemoryStream(
                System.Text.Encoding.UTF8.GetBytes(csv));

        var parser =
            new CsvDocumentParser(
                Options.Create(
                    new CsvParsingOptions
                    {
                        MaxRows = 10
                    }));

        CsvDocument document =
            await parser.ParseAsync(stream);

        Assert.Equal(
            3,
            document.Rows.Count);

        Assert.False(
            document.HasMoreRows);

        Assert.Empty(
            document.Warnings);
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

        using var stream =
            new MemoryStream(
                System.Text.Encoding.UTF8.GetBytes(csv));

        var parser =
            new CsvDocumentParser(
                Options.Create(
                    new CsvParsingOptions
                    {
                        MaxRows = null
                    }));

        CsvDocument document =
            await parser.ParseAsync(stream);

        Assert.Equal(
            3,
            document.Rows.Count);

        Assert.False(
            document.HasMoreRows);
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

        using var stream =
            new MemoryStream(
                System.Text.Encoding.UTF8.GetBytes(csv));

        var parser =
            new CsvDocumentParser(
                Options.Create(
                    new CsvParsingOptions
                    {
                        MaxRows = 3
                    }));

        CsvDocument document =
            await parser.ParseAsync(stream);

        Assert.Equal(
            3,
            document.Rows.Count);

        Assert.False(
            document.HasMoreRows);
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

        using var stream =
            new MemoryStream(
                System.Text.Encoding.UTF8.GetBytes(csv));

        var parser =
            new CsvDocumentParser(
                Options.Create(
                    new CsvParsingOptions
                    {
                        MaxRows = 0
                    }));

        CsvDocument document =
            await parser.ParseAsync(stream);

        Assert.Empty(
            document.Rows);

        Assert.True(
            document.HasMoreRows);

        Assert.Equal(
            2,
            document.Columns.Count);
    }
}
