namespace DeskVault.Application.Documents.Queries.SearchDocuments;

public sealed record SearchDocumentsResult(
    Guid DocumentId,
    string FileName,
    string DisplayName,
    IReadOnlyList<SearchMatch> Matches,
    int MatchCount)
{
    /// <summary>
    /// The relevance score accumulated over all matching evidence when
    /// evidence retention is bounded. When null, the ranker derives the
    /// score from the supplied Matches collection.
    /// </summary>
    public int? RelevanceScore { get; init; }
}
