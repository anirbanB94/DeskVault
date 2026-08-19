namespace DeskVault.UI.Rendering.CsvDocumentRendering;

public sealed class CsvParsingOptions
{
    public const int DefaultPreviewRowLimit = 10_000;

    public const string SectionName = "CsvParsing";

    public int? MaxRows { get; set; } =
        DefaultPreviewRowLimit;
}
