namespace DeskVault.Application.Documents.Queries.SearchDocuments;

public sealed record SearchDocumentsQuery(
    string SearchText,
    IReadOnlyList<string>? FileTypes = null,
    SearchDocumentsContinuation? Continuation = null,
    int Limit = SearchOptions.DefaultPageSize);
