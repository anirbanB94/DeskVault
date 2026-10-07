namespace DeskVault.Application.Documents.Queries.SearchDocuments;

public readonly record struct SearchDocumentsRankingKey(
    int RelevanceScore,
    string DisplayName,
    Guid DocumentId)
    : IComparable<SearchDocumentsRankingKey>
{
    public int CompareTo(
        SearchDocumentsRankingKey other)
    {
        int relevanceComparison =
            other.RelevanceScore.CompareTo(
                RelevanceScore);

        if (relevanceComparison != 0)
        {
            return relevanceComparison;
        }

        int displayNameComparison =
            StringComparer.Ordinal.Compare(
                DisplayName,
                other.DisplayName);

        if (displayNameComparison != 0)
        {
            return displayNameComparison;
        }

        return DocumentId.CompareTo(
            other.DocumentId);
    }
}
