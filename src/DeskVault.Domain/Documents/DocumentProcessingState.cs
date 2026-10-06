namespace DeskVault.Domain.Documents;

/// <summary>
/// Represents the execution state of document processing independently
/// from document lifecycle and derived knowledge availability.
/// </summary>
public enum DocumentProcessingState
{
    NeverProcessed = 0,
    Processing = 1,
    Succeeded = 2,
    Failed = 3,
    Cancelled = 4
}
