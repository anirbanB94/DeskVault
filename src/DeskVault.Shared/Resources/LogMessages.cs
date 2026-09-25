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

    public const string WorkspaceActivationAlreadyActive =
        "Application workspace activation skipped because the workspace is already active.";

    public const string WorkspaceActivationNotFound =
        "Application workspace activation skipped because the workspace was not found.";

    public const string WorkspaceActivationCompleted =
        "Application workspace activation completed.";

    public const string WorkspaceCreationMissingDocuments =
        "Application workspace creation rejected because one or more documents were not found.";

    public const string WorkspaceCreationCompleted =
        "Application workspace creation completed.";

    public const string WorkspaceOpenAlreadyActive =
        "Application workspace open skipped because the workspace is already active.";

    public const string WorkspaceOpenNotFound =
        "Application workspace open skipped because the workspace was not found.";

    public const string WorkspaceOpenCompleted =
        "Application workspace open completed.";

    public const string WorkspaceOpenCompletedWithMissingDocuments =
        "Application workspace open completed with {MissingDocumentCount} missing document(s).";

    public const string WorkspaceCloseNotFound =
        "Application workspace close skipped because the workspace was not active.";

    public const string WorkspaceCloseCompleted =
        "Application workspace close completed.";

    public const string WorkspaceDeletionNotFound =
        "Application workspace deletion skipped because the workspace was not active.";

    public const string WorkspaceDeletionCompleted =
        "Application workspace deletion completed.";

    public const string WorkspaceDocumentAdditionWorkspaceNotFound =
        "Application workspace document addition skipped because the workspace was not active.";

    public const string WorkspaceDocumentAdditionAlreadyMember =
        "Application workspace document addition skipped because the document is already a member.";

    public const string WorkspaceDocumentAdditionDocumentNotFound =
        "Application workspace document addition skipped because the document was not found.";

    public const string WorkspaceDocumentAdditionCompleted =
        "Application workspace document addition completed.";

    public const string WorkspaceDocumentRemovalWorkspaceNotFound =
        "Application workspace document removal skipped because the workspace was not active.";

    public const string WorkspaceDocumentRemovalNotMember =
        "Application workspace document removal skipped because the document is not a member.";

    public const string WorkspaceDocumentRemovalCompleted =
        "Application workspace document removal completed.";

    public const string WorkspaceRenameWorkspaceNotFound =
        "Application workspace rename skipped because the workspace was not active.";

    public const string WorkspaceRenameNotPersistent =
        "Application workspace rename skipped because the workspace is not persistent.";

    public const string WorkspaceRenameNameRequired =
        "Application workspace rename skipped because a workspace name is required.";

    public const string WorkspaceRenameCompleted =
        "Application workspace rename completed.";

    public const string WorkspaceSaveTemporaryNotFound =
        "Application temporary workspace save skipped because the workspace was not active.";

    public const string WorkspaceSaveTemporaryNotTemporary =
        "Application temporary workspace save skipped because the workspace is not temporary.";

    public const string WorkspaceSaveTemporaryNameRequired =
        "Application temporary workspace save skipped because a workspace name is required.";

    public const string WorkspaceSaveTemporaryCompleted =
        "Application temporary workspace save completed.";

    public const string WorkspaceDescriptionUpdateWorkspaceNotFound =
        "Application workspace description update skipped because the workspace was not active.";

    public const string WorkspaceDescriptionUpdateNotPersistent =
        "Application workspace description update skipped because the workspace is not persistent.";

    public const string WorkspaceDescriptionUpdateCompleted =
        "Application workspace description update completed.";

    public const string WorkspacePresentationAddDocumentsOperation =
        "add documents";

    public const string WorkspacePresentationRemoveDocumentsOperation =
        "remove documents";

    public const string WorkspacePresentationSaveAsWorkspaceOperation =
        "save as workspace";

    public const string WorkspacePresentationUpdateDetailsOperation =
        "update workspace details";

    public const string WorkspacePresentationActivateDocumentOperation =
        "activate document";
    public const string DatabaseInitializationStarted =
        "Infrastructure database initialization started.";

    public const string DatabaseConnectionUnavailable =
        "Infrastructure database connection unavailable; applying database migrations.";

    public const string DatabaseConnectionAvailable =
        "Infrastructure database connection established.";

    public const string DatabaseMigrationsHistoryInitializing =
        "Infrastructure database migrations history is being initialized.";

    public const string DatabasePlaintextMigrationStarted =
        "Infrastructure plaintext database migration started.";

    public const string DatabasePlaintextMigrationCompleted =
        "Infrastructure plaintext database migration completed.";

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
