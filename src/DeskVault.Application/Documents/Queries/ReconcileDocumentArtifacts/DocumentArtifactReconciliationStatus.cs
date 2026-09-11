namespace DeskVault.Application.Documents.Queries.ReconcileDocumentArtifacts;

public enum DocumentArtifactReconciliationStatus
{
    Matched = 0,

    MissingArtifact = 1,

    OrphanedArtifact = 2,

    UnreadableArtifact = 3,

    PathMismatch = 4
}
