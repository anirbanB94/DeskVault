namespace DeskVault.Application.Documents.Commands.RemoveDocument;

public enum RemoveDocumentResultStatus
{
    Success,
    NotFound,
    StorageDeletionFailed,
    MetadataDeletionFailed
}

public sealed record RemoveDocumentResult(
    RemoveDocumentResultStatus Status,
    string Message);
