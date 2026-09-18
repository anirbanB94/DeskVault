namespace DeskVault.Application.Documents.Commands.RemoveDocument;

public enum RemoveDocumentResultStatus
{
    Success,
    NotFound,
    StorageDeletionFailed,
    MetadataDeletionFailed,
    WorkspaceMembershipCleanupFailed
}
