namespace DeskVault.Application.Documents.Queries.ReconcileDocumentArtifacts;

public enum DocumentArtifactReconciliationRecoveryAction
{
    /// <summary>
    /// None means the relationship is already consistent.
    /// </summary>
    None = 0,

    /// <summary>
    /// PreserveForRecovery means reconciliation has detected an inconsistency,
    /// but automatic mutation is not justified. This covers missing,
    /// unreadable, path-mismatch, and content-mismatch situations, as well
    /// as artifacts without sufficient identity or ownership evidence.
    /// </summary>
    PreserveForRecovery = 1,

    /// <summary>
    /// CleanupOrphanedArtifact means reconciliation has detected an orphaned
    /// artifact with sufficient identity and ownership evidence for safe
    /// automatic cleanup.
    /// </summary>
    CleanupOrphanedArtifact = 2
}
