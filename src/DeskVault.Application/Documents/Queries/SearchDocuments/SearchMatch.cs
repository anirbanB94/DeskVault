namespace DeskVault.Application.Documents.Queries.SearchDocuments;

public sealed record SearchMatch(
    SearchMatchSource Source,
    SearchMatchKind Kind,
    string Context);
