using System.Text;
using DeskVault.Application.Documents.Content;
using DeskVault.Application.Documents.Extraction;
using DeskVault.Application.Documents.Extraction.CSVDocument;

namespace DeskVault.Application.Tests;

public sealed class CsvDocumentTextExtractorStructuredContentTests
{
    private const string DocumentFileName =
        "document.csv";

    [Fact]
    public async Task ExtractAsync_NormalCsv_PreservesStructuredRowsAndProjection()
    {
        // Arrange
        const string csv =
            """
            Id,Name,Department
            1001,Alice Johnson,Engineering
            1002,Bob Smith,Design
            """;

        // Act
        DocumentTextExtractionResult result =
            await ExtractAsync(csv);

        // Assert
        Assert.NotNull(
            result.Content);

        DocumentContent content =
            result.Content!;

        Assert.Equal(
            2,
            content.Units.Count);

        Assert.Equal(
            DocumentContentUnitKind.TableRow,
            content.Units[0].Kind);

        Assert.Collection(
            content.Units[0].Fields,
            field =>
            {
                Assert.Equal(
                    "Id",
                    field.Name);

                Assert.Equal(
                    "1001",
                    field.Value);
            },
            field =>
            {
                Assert.Equal(
                    "Name",
                    field.Name);

                Assert.Equal(
                    "Alice Johnson",
                    field.Value);
            },
            field =>
            {
                Assert.Equal(
                    "Department",
                    field.Name);

                Assert.Equal(
                    "Engineering",
                    field.Value);
            });

        Assert.Equal(
            1,
            content.Units[1].Order);

        Assert.Equal(
            content.SearchableText,
            result.Text.ReplaceLineEndings("\n"));
    }

    [Fact]
    public async Task ExtractAsync_UnevenRows_PreservesStructuralWarning()
    {
        // Arrange
        const string csv =
            """
            Id,Owner,Department,Status
            DV-4001,Alice Johnson,Engineering
            DV-4002,Bob Smith,Design,Active
            """;

        // Act
        DocumentTextExtractionResult result =
            await ExtractAsync(csv);

        // Assert
        Assert.NotNull(
            result.Content);

        DocumentContent content =
            result.Content!;

        Assert.Single(
            content.Warnings);

        Assert.Equal(
            "Row 2 contains 3 field(s), but the header contains 4 column(s).",
            content.Warnings[0].Message);

        Assert.Equal(
            string.Empty,
            content.Units[0].Fields[3].Value);

        Assert.DoesNotContain(
            content.Warnings[0].Message,
            content.SearchableText);
    }

    [Fact]
    public async Task ExtractAsync_BlankHeader_PreservesParserGeneratedColumnName()
    {
        // Arrange
        const string csv =
            """
            Id,,Status
            DV-7001,Alice,Active
            """;

        // Act
        DocumentTextExtractionResult result =
            await ExtractAsync(csv);

        // Assert
        Assert.NotNull(
            result.Content);

        DocumentContentUnit row =
            result.Content!.Units[0];

        Assert.Equal(
            "Unnamed Column 2",
            row.Fields[1].Name);

        Assert.Equal(
            "Alice",
            row.Fields[1].Value);
    }

    private static async Task<DocumentTextExtractionResult> ExtractAsync(
        string csv)
    {
        await using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(csv));

        CsvDocumentTextExtractor extractor =
            new();

        return await extractor.ExtractAsync(
            stream,
            DocumentFileName);
    }
}
