namespace DeskVault.Application.Documents;

public enum DocumentTextExtractionKind
{
    None,
    PlainText,
    Markdown,
    Csv,
    Json,
    Xml,
    Yaml,
    Ini,
    Config
}

public enum DocumentInAppPreviewKind
{
    None,
    Text,
    Markdown,
    Csv
}

public sealed record DocumentFormatCapability(
    string Extension,
    bool IsImportPickerVisible,
    DocumentTextExtractionKind TextExtraction,
    DocumentInAppPreviewKind InAppPreview)
{
    public bool SupportsTextExtraction =>
        TextExtraction != DocumentTextExtractionKind.None;

    public bool SupportsInAppPreview =>
        InAppPreview != DocumentInAppPreviewKind.None;

    public bool SupportsExternalViewFallback =>
        !SupportsInAppPreview;
}

public static class SupportedFileTypes
{
    // This is the authoritative declaration of importable formats
    // and their MVP 2 capabilities. Keep each extension here once.
    private static readonly IReadOnlyList<DocumentFormatCapability>
        FormatCapabilities =
        Array.AsReadOnly<DocumentFormatCapability>(
        [
            // Current Supported Documents picker.
            new(".pdf", true, DocumentTextExtractionKind.None, DocumentInAppPreviewKind.None),
            new(".docx", true, DocumentTextExtractionKind.None, DocumentInAppPreviewKind.None),
            new(".txt", true, DocumentTextExtractionKind.PlainText, DocumentInAppPreviewKind.Text),
            new(".md", true, DocumentTextExtractionKind.Markdown, DocumentInAppPreviewKind.Markdown),
            new(".csv", true, DocumentTextExtractionKind.Csv, DocumentInAppPreviewKind.Csv),
            new(".json", true, DocumentTextExtractionKind.Json, DocumentInAppPreviewKind.Text),
            new(".xml", true, DocumentTextExtractionKind.Xml, DocumentInAppPreviewKind.Text),
            new(".yaml", true, DocumentTextExtractionKind.Yaml, DocumentInAppPreviewKind.Text),
            new(".yml", true, DocumentTextExtractionKind.Yaml, DocumentInAppPreviewKind.Text),
            new(".ini", true, DocumentTextExtractionKind.Ini, DocumentInAppPreviewKind.Text),
            new(".config", true, DocumentTextExtractionKind.Config, DocumentInAppPreviewKind.Text),
            new(".log", true, DocumentTextExtractionKind.PlainText, DocumentInAppPreviewKind.Text),
            new(".c", true, DocumentTextExtractionKind.PlainText, DocumentInAppPreviewKind.Text),
            new(".cpp", true, DocumentTextExtractionKind.PlainText, DocumentInAppPreviewKind.Text),
            new(".h", true, DocumentTextExtractionKind.PlainText, DocumentInAppPreviewKind.Text),
            new(".hpp", true, DocumentTextExtractionKind.PlainText, DocumentInAppPreviewKind.Text),
            new(".cs", true, DocumentTextExtractionKind.PlainText, DocumentInAppPreviewKind.Text),
            new(".java", true, DocumentTextExtractionKind.PlainText, DocumentInAppPreviewKind.Text),
            new(".py", true, DocumentTextExtractionKind.PlainText, DocumentInAppPreviewKind.Text),
            new(".js", true, DocumentTextExtractionKind.PlainText, DocumentInAppPreviewKind.Text),
            new(".ts", true, DocumentTextExtractionKind.PlainText, DocumentInAppPreviewKind.Text),
            new(".css", true, DocumentTextExtractionKind.PlainText, DocumentInAppPreviewKind.Text),
            new(".sql", true, DocumentTextExtractionKind.PlainText, DocumentInAppPreviewKind.Text),
            new(".ps1", true, DocumentTextExtractionKind.PlainText, DocumentInAppPreviewKind.Text),

            // Importable and explicitly externally viewable when
            // an in-app preview is unavailable.
            new(".html", true, DocumentTextExtractionKind.None, DocumentInAppPreviewKind.None),
            new(".htm", true, DocumentTextExtractionKind.None, DocumentInAppPreviewKind.None),

            // Other currently importable formats. These are not
            // currently exposed in the Supported Documents picker.
            new(".doc", false, DocumentTextExtractionKind.None, DocumentInAppPreviewKind.None),
            new(".rtf", false, DocumentTextExtractionKind.None, DocumentInAppPreviewKind.None),

            // Office.
            new(".xls", false, DocumentTextExtractionKind.None, DocumentInAppPreviewKind.None),
            new(".xlsx", false, DocumentTextExtractionKind.None, DocumentInAppPreviewKind.None),
            new(".ppt", false, DocumentTextExtractionKind.None, DocumentInAppPreviewKind.None),
            new(".pptx", false, DocumentTextExtractionKind.None, DocumentInAppPreviewKind.None),
            new(".odt", false, DocumentTextExtractionKind.None, DocumentInAppPreviewKind.None),
            new(".ods", false, DocumentTextExtractionKind.None, DocumentInAppPreviewKind.None),
            new(".odp", false, DocumentTextExtractionKind.None, DocumentInAppPreviewKind.None),

            // Images.
            new(".png", false, DocumentTextExtractionKind.None, DocumentInAppPreviewKind.None),
            new(".jpg", false, DocumentTextExtractionKind.None, DocumentInAppPreviewKind.None),
            new(".jpeg", false, DocumentTextExtractionKind.None, DocumentInAppPreviewKind.None),
            new(".bmp", false, DocumentTextExtractionKind.None, DocumentInAppPreviewKind.None),
            new(".tif", false, DocumentTextExtractionKind.None, DocumentInAppPreviewKind.None),
            new(".tiff", false, DocumentTextExtractionKind.None, DocumentInAppPreviewKind.None),
            new(".webp", false, DocumentTextExtractionKind.None, DocumentInAppPreviewKind.None),

            // Email.
            new(".eml", false, DocumentTextExtractionKind.None, DocumentInAppPreviewKind.None),
            new(".msg", false, DocumentTextExtractionKind.None, DocumentInAppPreviewKind.None)
        ]);

    private static readonly IReadOnlyDictionary<string, DocumentFormatCapability>
        CapabilitiesByExtension =
            FormatCapabilities.ToDictionary(
                capability => capability.Extension,
                StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyCollection<string> AllExtensions =
        Array.AsReadOnly(
            FormatCapabilities
                .Select(capability => capability.Extension)
                .ToArray());

    private static readonly IReadOnlyCollection<string> ImportPickerExtensions =
        Array.AsReadOnly(
            FormatCapabilities
                .Where(capability => capability.IsImportPickerVisible)
                .Select(capability => capability.Extension)
                .ToArray());

    public static bool IsSupported(string extension)
    {
        return GetForExtension(extension) is not null;
    }

    public static IReadOnlyCollection<string> GetAll()
    {
        return AllExtensions;
    }

    public static IReadOnlyCollection<string> GetImportPickerExtensions()
    {
        return ImportPickerExtensions;
    }

    public static DocumentFormatCapability? GetForExtension(
        string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return null;
        }

        return CapabilitiesByExtension.TryGetValue(
            extension,
            out DocumentFormatCapability? capability)
                ? capability
                : null;
    }

    public static DocumentFormatCapability? GetForFileName(
        string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        return GetForExtension(
            Path.GetExtension(fileName));
    }

    public static DocumentTextExtractionKind GetTextExtractionKind(
        string fileName)
    {
        return GetForFileName(fileName)?.TextExtraction
            ?? DocumentTextExtractionKind.None;
    }

    public static DocumentInAppPreviewKind GetInAppPreviewKind(
        string fileName)
    {
        return GetForFileName(fileName)?.InAppPreview
            ?? DocumentInAppPreviewKind.None;
    }
}
