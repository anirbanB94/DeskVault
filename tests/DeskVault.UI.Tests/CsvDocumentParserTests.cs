using DeskVault.UI.Rendering.CsvDocumentRendering;
using Microsoft.Extensions.Options;

namespace DeskVault.UI.Tests;

public sealed class CsvDocumentParserTests
{
    [Fact]
    public async Task ParseAsync_NormalCsv_PreservesColumnsAndRows()
    {
        const string csv =
            """
            Id,Name,Department
            1001,Alice Johnson,Engineering
            1002,Bob Smith,Design
            """;

        CsvDocument document =
            await ParseAsync(csv);

        Assert.Equal(3, document.Columns.Count);

        Assert.Equal(
            new CsvDocumentColumn(0, "Id"),
            document.Columns[0]);

        Assert.Equal(
            new CsvDocumentColumn(1, "Name"),
            document.Columns[1]);

        Assert.Equal(
            new CsvDocumentColumn(2, "Department"),
            document.Columns[2]);

        Assert.Equal(2, document.Rows.Count);

        Assert.Equal(
            new[] { "1001", "Alice Johnson", "Engineering" },
            document.Rows[0]);

        Assert.Equal(
            new[] { "1002", "Bob Smith", "Design" },
            document.Rows[1]);

        Assert.Empty(document.Warnings);
        Assert.False(document.HasMoreRows);
    }

    [Fact]
    public async Task ParseAsync_QuotedFieldsContainingCommas_PreservesFieldAsSingleValue()
    {
        const string csv =
            """
            Id,Owner,Notes
            DV-2001,"Smith, John","Requires review, approval pending"
            DV-2002,"Patel, Diana","Completed, archived"
            """;

        CsvDocument document =
            await ParseAsync(csv);

        Assert.Equal(3, document.Columns.Count);
        Assert.Equal(2, document.Rows.Count);

        Assert.Equal(
            "Smith, John",
            document.Rows[0][1]);

        Assert.Equal(
            "Requires review, approval pending",
            document.Rows[0][2]);

        Assert.Equal(
            "Patel, Diana",
            document.Rows[1][1]);

        Assert.Equal(
            "Completed, archived",
            document.Rows[1][2]);

        Assert.Empty(document.Warnings);
        Assert.False(document.HasMoreRows);
    }

    [Fact]
    public async Task ParseAsync_EmptyFields_PreservesEmptyValues()
    {
        const string csv =
            """
            Id,Owner,Department,Status
            DV-3001,Alice Johnson,,Active
            DV-3002,,Finance,Pending
            DV-3003,Charlie Brown,Engineering,
            """;

        CsvDocument document =
            await ParseAsync(csv);

        Assert.Equal(4, document.Columns.Count);
        Assert.Equal(3, document.Rows.Count);

        Assert.Equal(
            string.Empty,
            document.Rows[0][2]);

        Assert.Equal(
            string.Empty,
            document.Rows[1][1]);

        Assert.Equal(
            string.Empty,
            document.Rows[2][3]);

        Assert.Empty(document.Warnings);
        Assert.False(document.HasMoreRows);
    }

    [Fact]
    public async Task ParseAsync_MissingFields_PadsRowAndCreatesWarning()
    {
        const string csv =
            """
            Id,Owner,Department,Status
            DV-4001,Alice Johnson,Engineering
            DV-4002,Bob Smith,Design,Active
            """;

        CsvDocument document =
            await ParseAsync(csv);

        Assert.Equal(4, document.Columns.Count);
        Assert.Equal(2, document.Rows.Count);

        Assert.Equal(
            string.Empty,
            document.Rows[0][3]);

        Assert.Equal(
            "Active",
            document.Rows[1][3]);

        Assert.Single(document.Warnings);

        Assert.Equal(
            2,
            document.Warnings[0].RowNumber);

        Assert.Equal(
            "Row 2 contains 3 field(s), but the header contains 4 column(s).",
            document.Warnings[0].Message);

        Assert.False(document.HasMoreRows);
    }

    [Fact]
    public async Task ParseAsync_ExtraFields_PreservesExtraValueAndCreatesWarning()
    {
        const string csv =
            """
            Id,Owner,Department
            DV-5001,Alice Johnson,Engineering,Unexpected value
            DV-5002,Bob Smith,Design
            """;

        CsvDocument document =
            await ParseAsync(csv);

        Assert.Equal(4, document.Columns.Count);

        Assert.Equal(
            "Unnamed Column 4",
            document.Columns[3].Header);

        Assert.Equal(2, document.Rows.Count);

        Assert.Equal(
            "Unexpected value",
            document.Rows[0][3]);

        Assert.Equal(
            string.Empty,
            document.Rows[1][3]);

        Assert.Single(document.Warnings);

        Assert.Equal(
            2,
            document.Warnings[0].RowNumber);

        Assert.Equal(
            "Row 2 contains 4 field(s), but the header contains 3 column(s).",
            document.Warnings[0].Message);

        Assert.False(document.HasMoreRows);
    }

    [Fact]
    public async Task ParseAsync_MultipleUnevenRows_CreatesWarningForEachAffectedRow()
    {
        const string csv =
            """
            Id,Owner,Department,Status,Notes
            DV-6001,Alice Johnson,Engineering,Active
            DV-6002,Bob Smith,Design,Active,Complete
            DV-6003,Charlie Brown,Finance
            DV-6004,Diana Patel,Operations,Inactive,Archived
            """;

        CsvDocument document =
            await ParseAsync(csv);

        Assert.Equal(5, document.Columns.Count);
        Assert.Equal(4, document.Rows.Count);

        Assert.Equal(2, document.Warnings.Count);

        Assert.Equal(
            2,
            document.Warnings[0].RowNumber);

        Assert.Equal(
            4,
            document.Warnings[1].RowNumber);

        Assert.Equal(
            "Row 2 contains 4 field(s), but the header contains 5 column(s).",
            document.Warnings[0].Message);

        Assert.Equal(
            "Row 4 contains 3 field(s), but the header contains 5 column(s).",
            document.Warnings[1].Message);

        Assert.Equal(
            string.Empty,
            document.Rows[0][4]);

        Assert.Equal(
            "Complete",
            document.Rows[1][4]);

        Assert.Equal(
            string.Empty,
            document.Rows[2][3]);

        Assert.Equal(
            "Archived",
            document.Rows[3][4]);

        Assert.False(document.HasMoreRows);
    }

    [Fact]
    public async Task ParseAsync_BlankHeader_CreatesUnnamedColumn()
    {
        const string csv =
            """
            Id,,Status
            DV-7001,Alice,Active
            """;

        CsvDocument document =
            await ParseAsync(csv);

        Assert.Equal(3, document.Columns.Count);

        Assert.Equal(
            "Id",
            document.Columns[0].Header);

        Assert.Equal(
            "Unnamed Column 2",
            document.Columns[1].Header);

        Assert.Equal(
            "Status",
            document.Columns[2].Header);

        Assert.Equal(
            "Alice",
            document.Rows[0][1]);

        Assert.Empty(document.Warnings);
        Assert.False(document.HasMoreRows);
    }

    [Fact]
    public async Task ParseAsync_EmptyDocument_ReturnsEmptyDocument()
    {
        const string csv = "";

        CsvDocument document =
            await ParseAsync(csv);

        Assert.Empty(document.Columns);
        Assert.Empty(document.Rows);
        Assert.Empty(document.Warnings);
        Assert.False(document.HasMoreRows);
    }

    [Fact]
    public async Task ParseAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        const string csv =
            """
            Id,Name
            1,Alice
            2,Bob
            """;

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () =>
                ParseAsync(
                    csv,
                    cancellationTokenSource.Token));
    }

    private static async Task<CsvDocument> ParseAsync(
        string csv,
        CancellationToken cancellationToken = default)
    {
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

        return await parser.ParseAsync(
            stream,
            cancellationToken);
    }
}
