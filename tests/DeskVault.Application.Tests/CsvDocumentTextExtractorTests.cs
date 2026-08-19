using DeskVault.Application.Documents.Extraction;
using DeskVault.Application.Documents.Extraction.CSVDocument;
using System.Text;

namespace DeskVault.Application.Tests;

public sealed class CsvDocumentTextExtractorTests
{
    [Fact]
    public async Task ExtractAsync_NormalCsv_ReturnsHeaderAwareText()
    {
        const string csv =
            """
            Id,Name,Department
            1001,Alice Johnson,Engineering
            1002,Bob Smith,Design
            """;

        DocumentTextExtractionResult result =
            await ExtractAsync(csv);

        Assert.Equal(
            """
            Id: 1001
            Name: Alice Johnson
            Department: Engineering

            Id: 1002
            Name: Bob Smith
            Department: Design
            """,
            result.Text);
    }

    [Fact]
    public async Task ExtractAsync_EmptyValues_PreservesColumnLabels()
    {
        const string csv =
            """
            Id,Owner,Department,Status
            DV-3001,Alice Johnson,,Active
            DV-3002,,Finance,Pending
            """;

        DocumentTextExtractionResult result =
            await ExtractAsync(csv);

        Assert.Equal(
            """
            Id: DV-3001
            Owner: Alice Johnson
            Department: 
            Status: Active

            Id: DV-3002
            Owner: 
            Department: Finance
            Status: Pending
            """,
            result.Text);
    }

    [Fact]
    public async Task ExtractAsync_QuotedFields_PreservesParsedValues()
    {
        const string csv =
            """
            Id,Owner,Notes
            DV-2001,"Smith, John","Requires review, approval pending"
            """;

        DocumentTextExtractionResult result =
            await ExtractAsync(csv);

        Assert.Equal(
            """
            Id: DV-2001
            Owner: Smith, John
            Notes: Requires review, approval pending
            """,
            result.Text);
    }

    [Fact]
    public async Task ExtractAsync_UnevenRows_UsesParserMaterializedValues()
    {
        const string csv =
            """
            Id,Owner,Department,Status
            DV-4001,Alice Johnson,Engineering
            DV-4002,Bob Smith,Design,Active
            """;

        DocumentTextExtractionResult result =
            await ExtractAsync(csv);

        Assert.Equal(
            """
            Id: DV-4001
            Owner: Alice Johnson
            Department: Engineering
            Status: 

            Id: DV-4002
            Owner: Bob Smith
            Department: Design
            Status: Active
            """,
            result.Text);
    }

    [Fact]
    public async Task ExtractAsync_BlankHeader_UsesParserGeneratedColumnName()
    {
        const string csv =
            """
            Id,,Status
            DV-7001,Alice,Active
            """;

        DocumentTextExtractionResult result =
            await ExtractAsync(csv);

        Assert.Equal(
            """
            Id: DV-7001
            Unnamed Column 2: Alice
            Status: Active
            """,
            result.Text);
    }

    [Fact]
    public async Task ExtractAsync_DoesNotApplyPreviewRowLimit()
    {
        const string csv =
            """
            Id,Name
            1,Alice
            2,Bob
            3,Charlie
            """;

        DocumentTextExtractionResult result =
            await ExtractAsync(csv);

        Assert.Contains(
            "Id: 1",
            result.Text);

        Assert.Contains(
            "Id: 2",
            result.Text);

        Assert.Contains(
            "Id: 3",
            result.Text);
    }

    [Fact]
    public async Task ExtractAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        const string csv =
            """
            Id,Name
            1,Alice
            """;

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () =>
                ExtractAsync(
                    csv,
                    cancellationTokenSource.Token));
    }

    [Theory]
    [InlineData("data.csv")]
    [InlineData("data.CSV")]
    [InlineData("data.Csv")]
    public void CanExtract_CsvExtension_IsCaseInsensitive(
        string fileName)
    {
        var extractor =
            new CsvDocumentTextExtractor();

        Assert.True(
            extractor.CanExtract(fileName));
    }

    [Theory]
    [InlineData("data.txt")]
    [InlineData("data.md")]
    [InlineData("data.pdf")]
    public void CanExtract_NonCsvExtension_ReturnsFalse(
        string fileName)
    {
        var extractor =
            new CsvDocumentTextExtractor();

        Assert.False(
            extractor.CanExtract(fileName));
    }

    private static async Task<DocumentTextExtractionResult> ExtractAsync(
        string csv,
        CancellationToken cancellationToken = default)
    {
        using var stream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(csv));

        var extractor =
            new CsvDocumentTextExtractor();

        return await extractor.ExtractAsync(
            stream,
            "document.csv",
            cancellationToken);
    }
}
