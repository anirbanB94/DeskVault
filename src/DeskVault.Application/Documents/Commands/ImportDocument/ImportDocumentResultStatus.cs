namespace DeskVault.Application.Documents.Commands.ImportDocument;

public enum ImportDocumentResultStatus
{
    Success = 0,

    ValidationFailed = 1,
    FileNotFound = 2,
    UnsupportedFileType = 3,

    Duplicate = 4,

    StorageFailed = 5,

    UnexpectedError = 6
}