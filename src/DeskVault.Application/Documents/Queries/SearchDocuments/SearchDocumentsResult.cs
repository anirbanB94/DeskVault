namespace DeskVault.Application.Documents.Queries.SearchDocuments;

public sealed record SearchDocumentsResult(
    Guid DocumentId,
    string FileName,
    string DisplayName,
    int ChunkOrder,
    string ChunkText);
