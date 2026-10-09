
using System.Text;

namespace DeskVault.Application.Documents.Queries.SearchDocuments;

/// <summary>
/// Defines the canonical Unicode representation and comparison semantics
/// used by keyword search.
/// </summary>
public static class SearchTextCanonicalizer
{
    /// <summary>
    /// Trims and normalizes search input to Unicode Normalization Form C
    /// so canonically equivalent queries use the same representation.
    /// </summary>
    /// <param name="searchText">The original search input.</param>
    /// <returns>The trimmed, NFC-normalized search text.</returns>
    /// <exception cref="ArgumentException">
    /// The search input is null, empty, or consists only of whitespace.
    /// </exception>
    public static string CanonicalizeSearchText(
        string searchText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            searchText);

        return searchText
            .Trim()
            .Normalize(
                NormalizationForm.FormC);
    }

    /// <summary>
    /// Normalizes a candidate value to Unicode Normalization Form C
    /// without trimming or otherwise changing its content.
    /// </summary>
    /// <param name="value">The candidate text to normalize.</param>
    /// <returns>The NFC-normalized candidate text.</returns>
    /// <exception cref="ArgumentNullException">
    /// The candidate value is null.
    /// </exception>
    public static string CanonicalizeValue(
        string value)
    {
        ArgumentNullException.ThrowIfNull(
            value);

        return value.Normalize(
            NormalizationForm.FormC);
    }

    /// <summary>
    /// Determines whether canonicalized candidate text contains
    /// canonicalized search text using ordinal, case-insensitive
    /// comparison semantics.
    /// </summary>
    /// <param name="canonicalValue">
    /// Candidate text already normalized using <see cref="CanonicalizeValue"/>.
    /// </param>
    /// <param name="canonicalSearchText">
    /// Search text already normalized using <see cref="CanonicalizeSearchText"/>.
    /// </param>
    /// <returns>
    /// True when the candidate contains the search text; otherwise, false.
    /// </returns>
    public static bool ContainsCanonicalized(
        string canonicalValue,
        string canonicalSearchText)
    {
        ArgumentNullException.ThrowIfNull(
            canonicalValue);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            canonicalSearchText);

        return canonicalValue.Contains(
            canonicalSearchText,
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Finds canonicalized search text using ordinal, case-insensitive
    /// comparison semantics.
    /// </summary>
    /// <param name="canonicalValue">
    /// Candidate text already normalized using <see cref="CanonicalizeValue"/>.
    /// </param>
    /// <param name="canonicalSearchText">
    /// Search text already normalized using <see cref="CanonicalizeSearchText"/>.
    /// </param>
    /// <returns>
    /// The zero-based match index in the canonicalized candidate, or -1
    /// when no match is found.
    /// </returns>
    public static int IndexOfCanonicalized(
        string canonicalValue,
        string canonicalSearchText)
    {
        ArgumentNullException.ThrowIfNull(
            canonicalValue);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            canonicalSearchText);

        return canonicalValue.IndexOf(
            canonicalSearchText,
            StringComparison.OrdinalIgnoreCase);
    }
}
