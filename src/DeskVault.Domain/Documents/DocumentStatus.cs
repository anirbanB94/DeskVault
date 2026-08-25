namespace DeskVault.Domain.Documents;

public enum DocumentStatus
{
    Imported = 0,
    Processing = 1,
    Indexed = 2,
    Available = 3,
    Failed = 4,
    Archived = 5,
    Deleted = 6
}
