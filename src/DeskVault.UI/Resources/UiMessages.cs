namespace DeskVault.UI.Resources;

public static class UiMessages
{
    // Application

    public const string DeskVaultTitle =
        "DeskVault";

    public const string DeskVaultWorkspaceTitle =
        "DeskVault Workspace";

    public const string DatabaseInitializationFailedTitle =
        "Database Initialization Failed";

    public const string UnableToInitializeDatabase =
        "DeskVault could not initialize its local database. " +
        "The application will now close.";


    // Common UI

    public const string OpenButton =
        "Open";

    public const string CloseButton =
        "Close";

    public const string CancelButton =
        "Cancel";

    public const string SaveButton =
        "Save";

    public const string CreateButton =
        "Create";

    public const string RemoveButton =
        "Remove";

    public const string View =
        "View";


    // Document list and import

    public const string OpenDocumentTitle =
        "Open Document";

    public const string DocumentColumnHeader =
        "Documents";

    public const string SelectDocumentToImportTitle =
        "Select a document to import";

    public const string SupportedDocumentsFilter =
        "Supported Documents|" +
        "*.pdf;*.docx;*.txt;*.md;*.csv;" +
        "*.json;*.xml;*.yaml;*.yml;*.ini;*.config;*.log;" +
        "*.c;*.cpp;*.h;*.hpp;*.cs;*.java;*.py;*.js;*.ts;*.css;*.sql;*.ps1;" +
        "*.html;*.htm|";

    public const string AllFilesFilter =
        "All Files|*.*";

    public const string ImportButton =
        "Import";

    public const string ReprocessButton =
        "Reprocess";

    public const string SearchButton =
        "Search";

    public const string ClearSearchButton =
        "Clear";

    public const string LoadMoreButton =
        "Load More";

    public const string NoDocumentsImportedMessage =
        "No documents imported yet.";

    public const string ImportFirstDocumentMessage =
        "Use Import to add your first document.";

    public const string SearchFileTypeAll =
        "All file types";


    // Document removal and lifecycle

    public const string RemoveDocumentTitle =
        "Remove Document";

    public const string CloseDocument =
        "Close Document";

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


    // Document opening and processing

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

    public const string ReprocessingDocumentStatus =
        "Reprocessing document...";

    public const string DocumentReprocessedStatus =
        "Document reprocessed successfully.";

    public const string ReprocessDocumentTitle =
        "Document Reprocessed";

    public const string UnableToReprocessDocumentStatus =
        "Unable to reprocess document.";

    public const string UnableToReprocessDocument =
        "The document could not be reprocessed.";


    // Document workspace presentation

    public const string DocumentWorkspaceTitle =
        "Document Workspace";

    public const string DocumentWorkspaceMetadata =
        "Document Workspace";

    public const string DocumentWorkspaceFallbackName =
        "Document";

    public const string TemporaryWorkspace =
        "Temporary Workspace";

    public const string BackToDocuments =
        "← Documents";

    public const string BackToWorkspaces =
        "← Workspaces";

    public const string WorkspaceMenuButton =
        "⋯";

    public const string AiButton =
        "AI";

    public const string AddRelatedDocuments =
        "Add Related Documents";

    public const string DocumentInformation =
        "Document Information";

    public const string DisplayNameLabel =
        "Display Name:";

    public const string DocumentFileNameLabel =
        "File Name:";

    public const string DocumentFileTypeLabel =
        "File Type:";

    public const string ImportedAtLabel =
        "Imported At:";

    public const string StatusLabel =
        "Status:";

    public const string Sha256Label =
        "SHA-256:";

    public const string SaveAsWorkspace =
        "Save as Workspace";

    public const string RemoveDocument =
        "Remove Document";

    public const string CloseWorkspace =
        "Close Workspace";

    public const string Workspace =
        "Workspace";

    public const string WorkspaceDescription =
        "Workspace description";

    public const string TabsNavigation =
        "Tabs";

    public const string SidebarNavigation =
        "Sidebar";

    public const string NoDocumentsOpen =
        "No documents open";


    // Document preview

    public const string PreviewUnavailableTitle =
        "Preview Unavailable";

    public const string UnsupportedDocumentPreviewMessage =
        "This document format is not supported for in-app preview.";

    public const string OpenExternallyButton =
        "Open Externally";


    // Workspace management

    public const string Workspaces =
        "Workspaces";

    public const string SearchDocumentsPlaceholder =
        "Search documents...";

    public const string NoWorkspacesAvailable =
        "No workspaces available.";

    public const string RequiredWorkspaceNameLabel =
        "*Workspace Name:";

    public const string WorkspaceNameRequired =
        "Workspace name is required.";

    public const string DescriptionLabel =
        "Description:";

    public const string CreateWorkspaceTitle =
        "Create Workspace";

    public const string UpdateWorkspaceDetailsTitle =
        "Update Workspace Details";

    public const string DeleteWorkspace =
        "Delete Workspace";

    public static string ConfirmDeleteWorkspace(
        string workspaceName)
    {
        return
            $"Are you sure you want to delete the workspace \"{workspaceName}\"?";
    }

    public const string AddDocumentsToWorkspace =
        "Add Documents to Workspace";

    public const string RemoveDocumentsFromWorkspace =
        "Remove Documents from Workspace";

    public const string NoDocumentsAvailable =
        "No documents available.";

    public const string NoDocumentsInWorkspace =
        "No documents in this workspace.";

    public const string AddSelectedButton =
        "Add Selected";

    public const string RemoveSelectedButton =
        "Remove Selected";

    public const string UnableToCreateWorkspace =
        "Unable to create workspace.";

    public const string CurrentWorkspaceFallbackName =
        "this workspace";

    public const string UnableToCloseWorkspace =
        "Unable to close workspace.";

    public const string UnableToRemoveWorkspace =
        "Unable to remove workspace.";

    public const string OpenWorkspaceTitle =
        "Open Workspace";

    public const string UnableToOpenWorkspace =
        "Unable to open workspace.";


    // Workspace list

    public const string WorkspaceNameColumnHeader =
        "Workspace Name";

    public const string DescriptionColumnHeader =
        "Description";

    public const string FilesColumnHeader =
        "Files";

    public const string LastUpdatedColumnHeader =
        "Last Updated";

    public const string WorkspacesCount =
        "{0} workspaces";

    public const string WorkspaceCount =
        "{0} workspace(s)";


    // Document list columns

    public const string FileNameColumnHeader =
        "File Name";

    public const string TypeColumnHeader =
        "Type";

    public const string ImportedColumnHeader =
        "Imported";

    public const string StatusColumnHeader =
        "Status";

    public const string DocumentCount =
        "{0} document(s)";


    // Search results columns

    public const string NameColumnHeader =
        "Name";

    public const string FileColumnHeader =
        "File";

    public const string MatchColumnHeader =
        "Match";

    public const string MatchesColumnHeader =
        "Matches";


    // Counts and general formatting

    public const string DocumentsCount =
        "{0} documents";

    public const string CountSeparator =
        "|";


    // Workspace/document picker and removal dialogs

    public const string FileTypeLabel =
        "File Type";

    public const string AllDocumentTypes =
        "All types";

    public const string SelectDocumentValidationMessage =
        "Select at least one document.";


    // AI

    public const string AiAssistant =
        "AI Assistant";

    public const string AiAssistanceFutureVersion =
        "AI assistance will be available in a future version.";

    public const string AskAboutDocuments =
        "Ask about your documents...";

    public const string SendButton =
        "Send";
}
