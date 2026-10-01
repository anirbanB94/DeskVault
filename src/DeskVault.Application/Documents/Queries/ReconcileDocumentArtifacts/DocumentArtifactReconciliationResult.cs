namespace DeskVault.Application.Documents.Queries.ReconcileDocumentArtifacts;

public sealed record DocumentArtifactReconciliationResult(
    DocumentArtifactReconciliationStatus Status,
    Guid? DocumentId,
    string ArtifactPath,
    string Description,
    DocumentArtifactReconciliationRecoveryAction? recoveryAction = null)
{
    public DocumentArtifactReconciliationRecoveryAction RecoveryAction { get; init; } =
        recoveryAction ?? GetDefaultRecoveryAction(
            Status,
            DocumentId);

    private static DocumentArtifactReconciliationRecoveryAction GetDefaultRecoveryAction(
        DocumentArtifactReconciliationStatus status,
        Guid? documentId)
    {
        return status switch
        {
            DocumentArtifactReconciliationStatus.Matched =>
                DocumentArtifactReconciliationRecoveryAction.None,

            DocumentArtifactReconciliationStatus.OrphanedArtifact =>
                documentId.HasValue
                    ? DocumentArtifactReconciliationRecoveryAction.CleanupOrphanedArtifact
                    : DocumentArtifactReconciliationRecoveryAction.PreserveForRecovery,

            DocumentArtifactReconciliationStatus.MissingArtifact =>
                DocumentArtifactReconciliationRecoveryAction.PreserveForRecovery,

            DocumentArtifactReconciliationStatus.UnreadableArtifact =>
                DocumentArtifactReconciliationRecoveryAction.PreserveForRecovery,

            DocumentArtifactReconciliationStatus.PathMismatch =>
                DocumentArtifactReconciliationRecoveryAction.PreserveForRecovery,

            DocumentArtifactReconciliationStatus.ContentMismatch =>
                DocumentArtifactReconciliationRecoveryAction.PreserveForRecovery,

            _ =>
                DocumentArtifactReconciliationRecoveryAction.PreserveForRecovery
        };
    }
}
