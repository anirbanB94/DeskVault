
using System.Globalization;
using DeskVault.Application.Documents.Queries.SearchDocuments;

namespace DeskVault.Application.Tests;

public sealed class SearchTextCanonicalizerTests
{
    [Theory]
    [InlineData("  cafe\u0301  ", "café")]
    [InlineData("  Security  ", "Security")]
    public void CanonicalizeSearchText_WhenInputIsPaddedOrDecomposed_ReturnsTrimmedNfc(
        string searchText,
        string expected)
    {
        // Arrange
        string input = searchText;

        // Act
        string result =
            SearchTextCanonicalizer.CanonicalizeSearchText(
                input);

        // Assert
        Assert.Equal(
            expected,
            result);

        Assert.Equal(
            result.Normalize(),
            result);
    }

    [Fact]
    public void CanonicalizeValue_WhenInputIsDecomposed_PreservesWhitespaceAndReturnsNfc()
    {
        // Arrange
        const string value =
            " cafe\u0301 ";

        // Act
        string result =
            SearchTextCanonicalizer.CanonicalizeValue(
                value);

        // Assert
        Assert.Equal(
            " café ",
            result);

        Assert.Equal(
            result.Normalize(),
            result);
    }

    [Theory]
    [InlineData("The CAFÉ is open.", "cafe\u0301")]
    [InlineData("Security policy content.", "SECURITY")]
    public void ContainsCanonicalized_WhenTextMatchesIgnoringUnicodeCaseAndNormalization_ReturnsTrue(
        string value,
        string searchText)
    {
        // Arrange
        (string canonicalValue, string canonicalSearchText) =
            CreateCanonicalizedPair(
                value,
                searchText);

        // Act
        bool result =
            SearchTextCanonicalizer.ContainsCanonicalized(
                canonicalValue,
                canonicalSearchText);

        // Assert
        Assert.True(
            result);
    }

    [Fact]
    public void IndexOfCanonicalized_WhenTextMatchesCanonically_ReturnsCanonicalIndex()
    {
        // Arrange
        (string canonicalValue, string canonicalSearchText) =
            CreateCanonicalizedPair(
                "The CAFÉ is open.",
                "cafe\u0301");

        // Act
        int result =
            SearchTextCanonicalizer.IndexOfCanonicalized(
                canonicalValue,
                canonicalSearchText);

        // Assert
        Assert.Equal(
            4,
            result);
    }

    [Fact]
    public void ContainsCanonicalized_WhenCurrentCultureIsTurkish_UsesOrdinalIgnoreCase()
    {
        // Arrange
        CultureInfo originalCulture =
            CultureInfo.CurrentCulture;

        (string canonicalValue, string canonicalSearchText) =
            CreateCanonicalizedPair(
                "FILE",
                "file");

        bool result;

        try
        {
            CultureInfo.CurrentCulture =
                CultureInfo.GetCultureInfo(
                    "tr-TR");

            // Act
            result =
                SearchTextCanonicalizer.ContainsCanonicalized(
                    canonicalValue,
                    canonicalSearchText);
        }
        finally
        {
            CultureInfo.CurrentCulture =
                originalCulture;
        }

        // Assert
        Assert.True(
            result);
    }

    [Fact]
    public void CanonicalizeSearchText_WhenInputIsWhitespace_ThrowsArgumentException()
    {
        // Arrange
        const string searchText =
            "   ";

        // Act
        Action act =
            () =>
                SearchTextCanonicalizer.CanonicalizeSearchText(
                    searchText);

        // Assert
        Assert.Throws<ArgumentException>(
            act);
    }

    [Fact]
    public void CanonicalizeValue_WhenInputIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        string? value = null;

        // Act
        Action act =
            () =>
                SearchTextCanonicalizer.CanonicalizeValue(
                    value!);

        // Assert
        Assert.Throws<ArgumentNullException>(
            act);
    }

    private static (
        string CanonicalValue,
        string CanonicalSearchText) CreateCanonicalizedPair(
            string value,
            string searchText)
    {
        string canonicalValue =
            SearchTextCanonicalizer.CanonicalizeValue(
                value);

        string canonicalSearchText =
            SearchTextCanonicalizer.CanonicalizeSearchText(
                searchText);

        return (
            canonicalValue,
            canonicalSearchText);
    }
}
