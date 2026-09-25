namespace DeskVault.UI.Views;

public sealed record WorkspaceListItem(
    Guid Id,
    string WorkspaceName,
    string Description,
    string Files,
    DateTimeOffset LastUpdated);
