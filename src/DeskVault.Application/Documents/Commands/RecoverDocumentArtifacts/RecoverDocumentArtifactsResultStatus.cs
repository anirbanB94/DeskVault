namespace DeskVault.Application.Documents.Commands.RecoverDocumentArtifacts;

public enum RecoverDocumentArtifactsResultStatus
{
    /// <summary>
    /// Nothing actionable was found.
    /// </summary>
    NoActionRequired = 0,

    /// <summary>
    /// One or more orphan artifacts were safely cleaned up.
    /// </summary>
    Recovered = 1,

    /// <summary>
    /// Inconsistencies remain but were intentionally not mutated.
    /// </summary>
    PreservedForRecovery = 2
}
