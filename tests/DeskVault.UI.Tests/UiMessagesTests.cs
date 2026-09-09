using DeskVault.UI.Resources;

namespace DeskVault.UI.Tests;

public sealed class UiMessagesTests
{
    [Fact]
    public void SupportedDocumentsFilter_ContainsAllSupportedImportPickerExtensions()
    {
        var patterns = GetSupportedDocumentPatterns();

        var requiredExtensions = new[]
        {
            "*.pdf",
            "*.docx",
            "*.txt",
            "*.md",
            "*.csv",
            "*.json",
            "*.xml",
            "*.yaml",
            "*.yml",
            "*.ini",
            "*.config",
            "*.log",
            "*.c",
            "*.cpp",
            "*.h",
            "*.hpp",
            "*.cs",
            "*.java",
            "*.py",
            "*.js",
            "*.ts",
            "*.css",
            "*.sql",
            "*.ps1",
            "*.html",
            "*.htm"
        };

        foreach (var extension in requiredExtensions)
        {
            Assert.Contains(
                extension,
                patterns,
                StringComparer.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void SupportedDocumentsFilter_DoesNotExposeUnrelatedSupportedFileTypes()
    {
        var patterns = GetSupportedDocumentPatterns();

        var unrelatedExtensions = new[]
        {
            "*.doc",
            "*.rtf",
            "*.xls",
            "*.xlsx",
            "*.ppt",
            "*.pptx",
            "*.odt",
            "*.ods",
            "*.odp",
            "*.png",
            "*.jpg",
            "*.jpeg",
            "*.bmp",
            "*.tif",
            "*.tiff",
            "*.webp",
            "*.eml",
            "*.msg"
        };

        foreach (var extension in unrelatedExtensions)
        {
            Assert.DoesNotContain(
                extension,
                patterns,
                StringComparer.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void AllFilesFilter_RemainsAvailable()
    {
        Assert.Equal(
            "All Files|*.*",
            UiMessages.AllFilesFilter);
    }

    private static IReadOnlyList<string> GetSupportedDocumentPatterns()
    {
        var parts =
            UiMessages.SupportedDocumentsFilter.Split('|');

        Assert.Equal(3, parts.Length);
        Assert.Equal(
            "Supported Documents",
            parts[0]);
        Assert.Equal(
            string.Empty,
            parts[2]);

        return parts[1]
            .Split(';', StringSplitOptions.RemoveEmptyEntries);
    }
}
