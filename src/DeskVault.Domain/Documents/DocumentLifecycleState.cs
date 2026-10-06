namespace DeskVault.Domain.Documents;

/// <summary>
/// Represents the lifecycle state of the document itself independently
/// from processing execution and derived knowledge availability.
/// </summary>
public enum DocumentLifecycleState
{
    Active = 0,
    Archived = 1,
    Deleted = 2
}
