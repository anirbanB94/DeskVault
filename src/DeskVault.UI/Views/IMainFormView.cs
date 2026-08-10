namespace DeskVault.UI.Views;

public interface IMainFormView
{
    event EventHandler ImportRequested;

    event EventHandler OpenRequested;

    event EventHandler DocumentSelectionChanged;
    
    Guid? SelectedDocumentId { get; }

    string? SelectedFilePath { get; }

    void SetSelectedDocumentId(Guid? documentId);

    void SetImportEnabled(bool enabled);

    void SetOpenEnabled(bool enabled);

    void SetStatus(string message);

    void ShowInformation(
        string message,
        string title);

    void ShowWarning(
        string message,
        string title);

    void ShowError(
        string message,
        string title);

    Task OpenDocumentAsync(
    Stream documentStream,
    string fileName,
    CancellationToken cancellationToken = default);

    void ShowDocuments(
    IReadOnlyList<DocumentListItem> documents);

    void ShowEmptyState();
}