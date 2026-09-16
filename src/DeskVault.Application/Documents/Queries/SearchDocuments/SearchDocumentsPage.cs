namespace DeskVault.Application.Documents.Queries.SearchDocuments;

public sealed record SearchDocumentsPage(
    IReadOnlyList<SearchDocumentsResult> Results,
    bool HasMore,
    SearchDocumentsContinuation? Continuation);
