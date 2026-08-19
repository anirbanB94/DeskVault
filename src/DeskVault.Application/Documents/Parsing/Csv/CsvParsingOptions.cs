namespace DeskVault.Application.Documents.Parsing.Csv;

public sealed class CsvParsingOptions
{
    public const int DefaultPreviewRowLimit = 10_000;

    public const string SectionName = "CsvParsing";

    public int? MaxRows { get; set; } =
        DefaultPreviewRowLimit;
}
