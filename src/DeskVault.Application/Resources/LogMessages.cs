namespace DeskVault.Application.Resources;

public static class LogMessages
{
    public const string DocumentImportStarted =
        "Application document import started.";

    public const string DocumentImportValidationRejected =
        "Application document import rejected during validation.";

    public const string DocumentImportDuplicate =
        "Application document import rejected because the document already exists.";

    public const string DocumentImportCompleted =
        "Application document import completed.";

    public const string DocumentImportStorageFailed =
        "Application document import failed during storage or processing.";

    public const string DocumentProcessingStarted =
        "Application document processing started.";

    public const string DocumentProcessingNotFound =
        "Application document processing skipped because the document was not found.";

    public const string DocumentProcessingCompleted =
        "Application document processing completed.";

    public const string DocumentProcessingFailed =
        "Application document processing failed.";

    public const string DocumentRemovalStarted =
        "Application document removal started.";

    public const string DocumentRemovalNotFound =
        "Application document removal skipped because the document was not found.";

    public const string DocumentStorageDeletionFailed =
        "Application document storage deletion failed.";

    public const string DocumentMetadataDeletionFailed =
        "Application document metadata deletion failed.";

    public const string DocumentRemovalCompleted =
        "Application document removal completed.";

    public const string DocumentSearchStarted =
        "Application document search started.";

    public const string DocumentSearchCompleted =
        "Application document search completed with {ResultCount} result(s).";

    public const string DocumentOpenNotFound =
        "Application document open skipped because the document was not found.";

    public const string DocumentOpenCompleted =
        "Application document open completed.";

    public const string DocumentRetrievalNotFound =
        "Application document retrieval skipped because the document was not found.";

    public const string DocumentListRefreshStarted =
        "Application document list refresh started.";

    public const string DocumentListRefreshCompleted =
        "Application document list refresh completed with {DocumentCount} document(s).";
}
