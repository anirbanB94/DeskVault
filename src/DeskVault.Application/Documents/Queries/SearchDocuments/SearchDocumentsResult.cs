namespace DeskVault.Application.Documents.Queries.SearchDocuments;

public sealed record SearchDocumentsResult(
    Guid DocumentId,
    string FileName,
    string DisplayName,
    IReadOnlyList<SearchMatch> Matches,
    int MatchCount);
