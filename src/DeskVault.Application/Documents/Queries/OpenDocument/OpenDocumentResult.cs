namespace DeskVault.Application.Documents.Queries.OpenDocument;

public sealed record OpenDocumentResult(
    Stream Content,
    string FileName);