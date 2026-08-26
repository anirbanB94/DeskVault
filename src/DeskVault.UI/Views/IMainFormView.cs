namespace DeskVault.UI.Views;

public interface IMainFormView
{
    event EventHandler ImportRequested;

    event EventHandler OpenRequested;

    event EventHandler RemoveRequested;

    event EventHandler ReprocessRequested;

    event EventHandler DocumentSelectionChanged;

    event EventHandler SearchRequested;

    Guid? SelectedDocumentId { get; }

    string? SelectedDocumentFileName { get; }

    string? SelectedFilePath { get; }

    string SearchText { get; }

    void SetSelectedDocumentId(Guid? documentId);

    void SetImportEnabled(bool enabled);

    void SetOpenEnabled(bool enabled);

    void SetRemoveEnabled(bool enabled);

    void SetReprocessEnabled(bool enabled);

    void SetStatus(string message);

    void ShowInformation(
        string message,
        string title);

    void ShowWarning(
        string message,
        string title);

    bool ConfirmRemoval(
        string fileName);

    void ShowError(
        string message,
        string title);

    void ShowDocuments(
        IReadOnlyList<DocumentListItem> documents);

    void ShowEmptyState();
}
