using DeskVault.Application.Documents;

namespace DeskVault.Application.Tests;

/// <summary>
/// Verifies the supported document format declarations and their
/// in-app preview and text extraction capabilities.
/// </summary>
public sealed class SupportedFileTypesTests
{
    /// <summary>
    /// Verifies that HTML and HTM formats are available for import and
    /// in-app preview without enabling text extraction or external-view fallback.
    /// </summary>
    [Theory]
    [InlineData(".html")]
    [InlineData(".htm")]
    public void GetForExtension_HtmlFormat_DeclaresInAppPreviewWithoutExtraction(
        string extension)
    {
        // Act
        DocumentFormatCapability? capability =
            SupportedFileTypes.GetForExtension(extension);

        // Assert
        Assert.NotNull(capability);
        Assert.Equal(extension, capability.Extension);
        Assert.True(capability.IsImportPickerVisible);

        Assert.Equal(
            DocumentTextExtractionKind.None,
            capability.TextExtraction);

        Assert.Equal(
            DocumentInAppPreviewKind.Html,
            capability.InAppPreview);

        Assert.True(capability.SupportsInAppPreview);
        Assert.False(capability.SupportsExternalViewFallback);
    }

    /// <summary>
    /// Verifies that HTML and HTM preview and extraction capabilities
    /// are resolved consistently regardless of filename casing.
    /// </summary>
    [Theory]
    [InlineData("document.html")]
    [InlineData("document.htm")]
    [InlineData("document.HTML")]
    [InlineData("document.HTM")]
    [InlineData("document.Html")]
    [InlineData("document.Htm")]
    public void GetInAppPreviewKind_HtmlFileName_IsCaseInsensitive(
        string fileName)
    {
        // Act
        DocumentInAppPreviewKind previewKind =
            SupportedFileTypes.GetInAppPreviewKind(fileName);

        DocumentTextExtractionKind extractionKind =
            SupportedFileTypes.GetTextExtractionKind(fileName);

        // Assert
        Assert.Equal(
            DocumentInAppPreviewKind.Html,
            previewKind);

        Assert.Equal(
            DocumentTextExtractionKind.None,
            extractionKind);
    }

    /// <summary>
    /// Verifies that HTML and HTM formats remain available
    /// in the document import file picker.
    /// </summary>
    [Theory]
    [InlineData(".html")]
    [InlineData(".htm")]
    public void GetImportPickerExtensions_ContainsHtmlFormats(
        string extension)
    {
        // Act
        IReadOnlyCollection<string> extensions =
            SupportedFileTypes.GetImportPickerExtensions();

        // Assert
        Assert.Contains(extension, extensions);
        Assert.True(SupportedFileTypes.IsSupported(extension));
    }
}
