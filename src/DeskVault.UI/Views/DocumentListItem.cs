namespace DeskVault.UI.Views;

public sealed record DocumentListItem(
    Guid Id,
    string FileName,
    string Type,
    DateTimeOffset Imported,
    string Status);
