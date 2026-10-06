namespace DeskVault.Domain.Documents;

/// <summary>
/// Represents the availability of a specific derived knowledge
/// representation independently from document processing execution.
/// </summary>
public enum DocumentKnowledgeAvailabilityState
{
    Unavailable = 0,
    Available = 1,
    Stale = 2,
    Failed = 3
}
