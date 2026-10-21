using DeskVault.Application.Documents;
using DeskVault.UI.Resources;

namespace DeskVault.UI.Tests;

public sealed class UiMessagesTests
{
    [Fact]
    public void SupportedDocumentsFilter_MatchesDeclaredPickerCapabilities()
    {
        string[] parts =
            UiMessages.SupportedDocumentsFilter.Split('|');

        Assert.Equal(3, parts.Length);
        Assert.Equal("Supported Documents", parts[0]);
        Assert.Equal(string.Empty, parts[2]);

        string[] actualPatterns =
            parts[1].Split(
                ';',
                StringSplitOptions.RemoveEmptyEntries);

        string[] expectedPatterns =
            SupportedFileTypes
                .GetImportPickerExtensions()
                .Select(extension => $"*{extension}")
                .ToArray();

        Assert.Equal(
            expectedPatterns,
            actualPatterns);

        foreach (string extension in
            SupportedFileTypes.GetImportPickerExtensions())
        {
            Assert.True(
                SupportedFileTypes.IsSupported(extension),
                $"Picker extension '{extension}' is not supported for import.");
        }
    }

    [Theory]
    [InlineData(".html")]
    [InlineData(".htm")]
    public void HtmlFormats_RemainImportableAndVisibleInSupportedDocumentsPicker(
        string extension)
    {
        DocumentFormatCapability capability =
            SupportedFileTypes.GetForExtension(extension)
            ?? throw new InvalidOperationException(
                $"No capability declaration exists for '{extension}'.");

        Assert.True(
            capability.IsImportPickerVisible,
            $"'{extension}' must remain visible in the Supported Documents picker.");

        Assert.True(
            SupportedFileTypes.IsSupported(extension),
            $"'{extension}' must remain supported for import.");

        string[] actualPatterns =
            UiMessages.SupportedDocumentsFilter
                .Split('|')[1]
                .Split(';', StringSplitOptions.RemoveEmptyEntries);

        Assert.Contains(
            $"*{extension}",
            actualPatterns,
            StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void ImportPickerCapabilities_AreUnique()
    {
        IReadOnlyCollection<string> extensions =
            SupportedFileTypes.GetImportPickerExtensions();

        Assert.Equal(
            extensions.Count,
            extensions.Distinct(
                StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void AllFilesFilter_RemainsAvailable()
    {
        Assert.Equal(
            "All Files|*.*",
            UiMessages.AllFilesFilter);
    }
}
