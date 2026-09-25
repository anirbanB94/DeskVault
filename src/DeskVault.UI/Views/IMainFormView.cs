namespace DeskVault.UI.Views;

public interface IMainFormView
{
    event EventHandler ImportRequested;

    event EventHandler OpenRequested;

    event EventHandler RemoveRequested;

    event EventHandler ReprocessRequested;

    event EventHandler DocumentSelectionChanged;

    event EventHandler SearchRequested;

    event EventHandler? LoadMoreSearchResultsRequested;

    event EventHandler WorkspaceSelectionChanged;

    event EventHandler WorkspaceCreateRequested;

    event EventHandler WorkspaceOpenRequested;

    event EventHandler WorkspaceRemoveRequested;

    Guid? SelectedDocumentId { get; }

    string? SelectedDocumentFileName { get; }

    string? SelectedFilePath { get; }

    string? SearchText { get; }

    string? SearchFileType { get; }

    Guid? SelectedWorkspaceId { get; }

    string? SelectedWorkspaceName { get; }

    WorkspaceCreateRequest? ShowCreateWorkspaceDialog();

    void SetSelectedDocumentId(Guid? documentId);

    void SetSelectedWorkspaceId(Guid? workspaceId);

    void SetImportEnabled(bool enabled);

    void SetOpenEnabled(bool enabled);

    void SetRemoveEnabled(bool enabled);

    void SetReprocessEnabled(bool enabled);

    void SetLoadMoreEnabled(bool enabled);

    void SetWorkspaceOpenEnabled(bool enabled);

    void SetWorkspaceRemoveEnabled(bool enabled);

    void SetStatus(string message);

    void SetDocumentsCount(int count);

    void SetWorkspacesCount(int count);

    void ShowInformation(
        string message,
        string title);

    void ShowWarning(
        string message,
        string title);

    bool ConfirmRemoval(
        string fileName);

    bool ConfirmWorkspaceRemoval(
        string workspaceName);

    void ShowError(
        string message,
        string title);

    void ShowDocuments(
        IReadOnlyList<DocumentListItem> documents);

    void ShowSearchResults(
        IReadOnlyList<SearchResultListItem> results);

    void AppendSearchResults(
        IReadOnlyList<SearchResultListItem> results);

    void ShowWorkspaces(
        IReadOnlyList<WorkspaceListItem> workspaces);

    void ShowEmptyState();
}
