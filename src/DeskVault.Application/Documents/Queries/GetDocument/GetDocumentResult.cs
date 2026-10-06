using DeskVault.Domain.Documents;

namespace DeskVault.Application.Documents.Queries.GetDocument;

public sealed record GetDocumentResult(
    Guid Id,
    string FileName,
    string DisplayName,
    string Sha256Hash,
    DateTime ImportedAt,
    DocumentLifecycleState LifecycleState,
    DocumentProcessingState ProcessingState,
    IReadOnlyList<DocumentKnowledgeAvailability> KnowledgeAvailability);
