namespace DeskVault.Application.Documents.Queries.ReconcileDocumentArtifacts;

public sealed record DocumentArtifactReconciliationResult(
    DocumentArtifactReconciliationStatus Status,
    Guid? DocumentId,
    string ArtifactPath,
    string Description);
