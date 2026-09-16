namespace DeskVault.UI.Views;

public sealed record SearchResultListItem(
    Guid DocumentId,
    string DisplayName,
    string FileName,
    string Snippet,
    int MatchCount);
