using DeskVault.Application.Documents.Queries.SearchDocuments;

namespace DeskVault.Infrastructure.Persistence;

/// <summary>
/// Provides Unicode-aware keyword matching for SQLite queries.
/// </summary>
public static class SqliteSearchFunctions
{
    public const string ContainsCanonicalizedFunctionName =
        "dv_contains_canonicalized";

    /// <summary>
    /// Determines whether candidate text contains the canonicalized
    /// search text using ordinal, case-insensitive semantics.
    /// </summary>
    /// <param name="value">Candidate text stored in SQLite.</param>
    /// <param name="searchText">Canonicalized search input.</param>
    /// <returns>
    /// True when the candidate contains the search text;
    /// otherwise, false.
    /// </returns>
    public static bool ContainsCanonicalized(
        string value,
        string searchText)
    {
        string canonicalValue =
            SearchTextCanonicalizer.CanonicalizeValue(
                value);

        return SearchTextCanonicalizer.ContainsCanonicalized(
            canonicalValue,
            searchText);
    }
}
