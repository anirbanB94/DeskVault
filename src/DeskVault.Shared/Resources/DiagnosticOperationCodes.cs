namespace DeskVault.Shared.Resources;

/// <summary>
/// Provides stable, source-controlled identifiers for failure diagnostics.
/// </summary>
public static class DiagnosticOperationCodes
{
    public const string DatabaseEncryptionKeyOperationFailure =
        "database-encryption-key.operation-failure";

    public const string EncryptionKeyOperationFailure =
        "encryption-key.operation-failure";
    public const string DocumentEncryptionFailure =
        "document-encryption.operation-failure";

    public const string DocumentDecryptionFailure =
        "document-decryption.operation-failure";

    public const string DocumentReaderFailure =
        "document-reader.operation-failure";

    public const string DocumentStorageFailure =
        "document-storage.operation-failure";

    public const string DocumentStorageDeletionFailure =
        "document-storage.deletion-failure";

    public const string DocumentStorageCleanupFailure =
        "document-storage.cleanup-failure";
    public const string DatabaseInitializationFailure = "database-initialization.operation-failure";
    public const string DocumentChunkReplacementFailure = "document-chunk-replacement.operation-failure";
    public const string DocumentSearchStoreFailure = "document-search-store.operation-failure";
    public const string DocumentHashFailure = "document-hash.operation-failure";


    public const string DocumentImportUnexpectedFailure =
        "document-import.unexpected-failure";

    public const string DocumentImportDuplicate =
        "document-import.duplicate";

    public const string DocumentImportStorageFailure =
        "document-import.storage";

    public const string DocumentImportArtifactCleanupFailure =
        "document-import.cleanup";

    public const string DocumentRemovalUnexpectedFailure =
        "document-removal.unexpected-failure";

    public const string DocumentRemovalStorageDeletionFailure =
        "document-removal.storage-deletion";

    public const string DocumentRemovalWorkspaceMembershipCleanupFailure =
        "document-removal.workspace-membership-cleanup";

    public const string DocumentRemovalMetadataDeletionFailure =
        "document-removal.metadata-deletion";

    public const string DocumentReprocessFailure =
        "document-reprocess.failure";

    public const string DocumentProcessingFailure =
        "document-processing.failure";

    public const string DocumentSearchFailure =
        "document-search.failure";

    public const string DocumentSearchLoadMoreFailure =
        "document-search.load-more-failure";

    public const string MainFormLoadFailure =
        "main-form.load-failure";

    public const string MainWorkspaceInitializationFailure =
        "main-workspace.initialization-failure";

    public const string WorkspaceCreationFailure =
        "workspace-creation.unexpected-failure";

    public const string WorkspaceCloseFailure =
        "workspace-close.unexpected-failure";

    public const string WorkspaceRemovalFailure =
        "workspace-removal.unexpected-failure";

    public const string WorkspaceOpenFailure =
        "workspace-open.unexpected-failure";

    public const string DocumentOpenFailure =
        "document-open.failure";
    public const string DocumentWorkspaceOpenFailure =
        "document-workspace.open.failure";

    public const string DocumentWorkspaceOpenExternallyFailure =
        "document-workspace.open-externally.failure";

    public const string DocumentWorkspaceRemovalFailure =
        "document-workspace.removal.failure";

    public const string DocumentWorkspaceRemovalConfirmationFailure =
        "document-workspace.removal-confirmation.failure";


    public const string WorkspacePresentationDocumentOpenFailure =
        "workspace-presentation.document-open";

    public const string WorkspacePresentationAddDocumentsFailure =
        "workspace-presentation.add-documents";

    public const string WorkspacePresentationRemoveDocumentsFailure =
        "workspace-presentation.remove-documents";

    public const string WorkspacePresentationSaveAsWorkspaceFailure =
        "workspace-presentation.save-as-workspace";

    public const string WorkspacePresentationUpdateDetailsFailure =
        "workspace-presentation.update-details";

    public const string WorkspacePresentationActivateDocumentFailure =
        "workspace-presentation.activate-document";

    public const string DocumentArtifactPathNormalizationFailure =
        "document-artifact.path-normalization-failure";

    public const string DocumentArtifactReadValidationFailure =
        "document-artifact.read-validation-failure";
}
