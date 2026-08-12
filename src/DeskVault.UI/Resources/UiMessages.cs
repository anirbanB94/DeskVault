namespace DeskVault.UI.Resources;

public static class UiMessages
{
    public const string DeskVaultTitle =
        "DeskVault";

    public const string OpenDocumentTitle =
        "Open Document";

    public const string DocumentColumnHeader =
        "Document";

    public const string SelectDocumentToImportTitle =
        "Select a document to import";

    public const string SupportedDocumentsFilter =
        "Supported Documents|*.pdf;*.docx;*.txt;*.md;*.csv|";

    public const string AllFilesFilter =
        "All Files|*.*";

    public const string RemoveDocumentTitle =
        "Remove Document";

    public static string ConfirmRemoveDocument(
        string fileName)
    {
        return
            $"Are you sure you want to remove '{fileName}'?\n\n" +
            "This will permanently delete the encrypted local copy " +
            "from DeskVault. This action cannot be undone.";
    }

    public const string DocumentRemovedTitle =
        "Document Removed";

    public const string RemoveFailedTitle =
        "Remove Failed";

    public const string UnableToRemoveDocument =
        "An unexpected error occurred while removing the document.";

    public const string DocumentWorkspaceTitle =
        "Document Workspace";

    public const string DocumentWorkspaceMetadata =
        "Document Workspace";

    public const string ReadyStatus =
        "Ready";

    public const string UnableToLoadDocumentsStatus =
        "Unable to load documents.";

    public const string UnableToLoadDocuments =
        "The imported documents could not be loaded.";

    public const string ImportingDocumentStatus =
        "Importing document...";

    public const string ImportCompleteTitle =
        "Import Complete";

    public const string ImportFailedTitle =
        "Import Failed";

    public const string OpeningDocumentStatus =
        "Opening document...";

    public const string DocumentOpenedStatus =
        "Document opened.";

    public const string UnableToOpenDocumentStatus =
        "Unable to open document.";

    public const string UnableToOpenDocument =
        "The document could not be opened.";

    public const string RemovingDocumentStatus =
        "Removing document...";

    public const string UnableToRemoveDocumentStatus =
        "Unable to remove document.";

    public const string UnexpectedImportError =
        "An unexpected error occurred while importing the document.";

    public const string BackToDocuments =
        "← Documents";

    public const string AiButton =
        "AI";

    public const string WorkspaceMenuButton =
        "⋯";

    public const string CloseButton =
        "Close";

    public const string AddRelatedDocuments =
        "Add Related Documents";

    public const string DocumentInformation =
        "Document Information";

    public const string SaveAsWorkspace =
        "Save as Workspace";

    public const string RemoveDocument =
        "Remove Document";

    public const string CloseWorkspace =
        "Close Workspace";
}
