namespace DeskVault.Application.Documents.Queries.ReconcileDocumentArtifacts;

public sealed record ReconcileDocumentArtifactsResult(
    IReadOnlyList<DocumentArtifactReconciliationResult> Findings);
