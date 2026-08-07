namespace DeskVault.Domain.Documents;

public enum DocumentStatus
{
    Imported = 0,
    Processing = 1,
    Indexed = 2,
    Available = 3,
    Archived = 4,
    Deleted = 5
}