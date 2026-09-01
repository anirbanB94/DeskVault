namespace DeskVault.Shared.Resources;

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

    public const string DatabaseInitializationStarted =
        "Infrastructure database initialization started.";

    public const string DatabaseConnectionUnavailable =
        "Infrastructure database connection unavailable; applying database migrations.";

    public const string DatabaseConnectionAvailable =
        "Infrastructure database connection established.";

    public const string DatabaseMigrationsHistoryInitializing =
        "Infrastructure database migrations history is being initialized.";

    public const string DatabaseInitializationCompleted =
        "Infrastructure database initialization completed.";

    public const string DatabaseInitializationFailed =
        "Infrastructure database initialization failed.";

    public const string DocumentRepositoryAddStarted =
        "Infrastructure document metadata persistence started.";

    public const string DocumentRepositoryAddCompleted =
        "Infrastructure document metadata persistence completed.";

    public const string DocumentRepositoryUpdateCompleted =
        "Infrastructure document metadata update completed.";

    public const string DocumentRepositoryDeleteCompleted =
        "Infrastructure document metadata deletion completed.";

    public const string DocumentRepositoryUpdateNotFound =
        "Infrastructure document metadata update failed because the document was not found.";

    public const string DocumentChunkReplacementStarted =
        "Infrastructure document chunk replacement started.";

    public const string DocumentChunkReplacementCompleted =
        "Infrastructure document chunk replacement completed with {ChunkCount} chunk(s).";

    public const string DocumentChunkReplacementFailed =
        "Infrastructure document chunk replacement failed.";

    public const string DocumentSearchStoreStarted =
        "Infrastructure document search started.";

    public const string DocumentSearchStoreCompleted =
        "Infrastructure document search completed with {ResultCount} result(s).";

    public const string DocumentSearchStoreFailed =
        "Infrastructure document search failed.";

    public const string DocumentStorageStarted =
        "Infrastructure document storage started.";

    public const string DocumentStorageCompleted =
        "Infrastructure document storage completed.";

    public const string DocumentStorageFailed =
        "Infrastructure document storage failed.";

    public const string DocumentStorageDeletionCompleted =
        "Infrastructure document storage deletion completed.";

    public const string DocumentEncryptionStarted =
        "Infrastructure document encryption started.";

    public const string DocumentEncryptionCompleted =
        "Infrastructure document encryption completed.";

    public const string DocumentEncryptionFailed =
        "Infrastructure document encryption failed.";

    public const string DocumentDecryptionStarted =
        "Infrastructure document decryption started.";

    public const string DocumentDecryptionCompleted =
        "Infrastructure document decryption completed.";

    public const string DocumentDecryptionFailed =
        "Infrastructure document decryption failed.";

    public const string DocumentReaderStarted =
        "Infrastructure encrypted document read started.";

    public const string DocumentReaderCompleted =
        "Infrastructure encrypted document read completed.";

    public const string DocumentReaderFailed =
        "Infrastructure encrypted document read failed.";

    public const string EncryptionKeyRetrievalStarted =
        "Infrastructure encryption key retrieval started.";

    public const string EncryptionKeyLoaded =
        "Infrastructure protected encryption key loaded.";

    public const string EncryptionKeyCreated =
        "Infrastructure encryption key created.";

    public const string EncryptionKeyOperationCompleted =
        "Infrastructure encryption key operation completed.";

    public const string EncryptionKeyOperationFailed =
        "Infrastructure encryption key operation failed.";

    public const string DatabaseEncryptionKeyLoaded =
        "Infrastructure database encryption key loaded.";

    public const string DatabaseEncryptionKeyCreated =
        "Infrastructure database encryption key created.";

    public const string DatabaseEncryptionKeyOperationFailed =
        "Infrastructure database encryption key operation failed.";

    public const string DocumentHashStarted =
        "Infrastructure document hash computation started.";

    public const string DocumentHashCompleted =
        "Infrastructure document hash computation completed.";

    public const string DocumentHashFailed =
        "Infrastructure document hash computation failed.";
}
