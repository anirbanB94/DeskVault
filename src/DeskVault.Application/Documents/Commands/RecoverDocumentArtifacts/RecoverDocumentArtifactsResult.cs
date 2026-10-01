using DeskVault.Application.Documents.Queries.ReconcileDocumentArtifacts;

namespace DeskVault.Application.Documents.Commands.RecoverDocumentArtifacts;

public sealed record RecoverDocumentArtifactsResult(
    RecoverDocumentArtifactsResultStatus Status,
    IReadOnlyList<DocumentArtifactReconciliationResult> Findings);
