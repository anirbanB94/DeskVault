using DeskVault.Application.Documents.Parsing.Csv;

namespace DeskVault.Application.Tests;

public sealed class CsvParsingOptionsTests
{
    [Fact]
    public void Options_DefaultPreviewRowLimit_Is10000()
    {
        CsvParsingOptions options =
            new();

        Assert.Equal(
            10_000,
            options.MaxRows);
    }

    [Fact]
    public void Options_CanOverridePreviewRowLimit()
    {
        CsvParsingOptions options =
            new()
            {
                MaxRows = 5_000
            };

        Assert.Equal(
            5_000,
            options.MaxRows);
    }

    [Fact]
    public void Options_CanDisableRowLimit()
    {
        CsvParsingOptions options =
            new()
            {
                MaxRows = null
            };

        Assert.Null(
            options.MaxRows);
    }
}
