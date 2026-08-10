namespace DeskVault.Application.Documents;

public static class SupportedFileTypes
{
    private static readonly HashSet<string> Extensions =
    [
        // Documents
        ".pdf",
        ".txt",
        ".doc",
        ".docx",
        ".rtf",
        ".md",

        // Office
        ".xls",
        ".xlsx",
        ".ppt",
        ".pptx",
        ".odt",
        ".ods",
        ".odp",

        // Images
        ".png",
        ".jpg",
        ".jpeg",
        ".bmp",
        ".tif",
        ".tiff",
        ".webp",

        // Source Code
        ".cs",
        ".cpp",
        ".c",
        ".h",
        ".hpp",
        ".java",
        ".py",
        ".js",
        ".ts",
        ".html",
        ".htm",
        ".css",
        ".json",
        ".xml",
        ".yml",
        ".yaml",
        ".sql",
        ".ps1",

        // Data
        ".csv",
        ".log",
        ".ini",
        ".config",

        // Email
        ".eml",
        ".msg"
    ];

    public static bool IsSupported(string extension)
    {
        return Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    public static IReadOnlyCollection<string> GetAll()
    {
        return Extensions;
    }
}