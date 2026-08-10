using DeskVault.Application.Documents.Commands.ImportDocument;
using DeskVault.Application.Documents.Queries.ListDocuments;
using DeskVault.Application.Documents.Queries.OpenDocument;
using DeskVault.UI.Views;

namespace DeskVault.UI.Presenters;

public sealed class MainFormPresenter
{
    private readonly IMainFormView _view;
    private readonly ImportDocumentHandler _importDocumentHandler;
    private readonly OpenDocumentHandler _openDocumentHandler;
    private readonly ListDocumentsHandler _listDocumentsHandler;

    public MainFormPresenter(
        IMainFormView view,
        ImportDocumentHandler importDocumentHandler,
        OpenDocumentHandler openDocumentHandler,
        ListDocumentsHandler listDocumentsHandler)
    {
        _view = view;
        _importDocumentHandler = importDocumentHandler;
        _openDocumentHandler = openDocumentHandler;
        _listDocumentsHandler = listDocumentsHandler;

        _view.ImportRequested += OnImportRequested;
        _view.OpenRequested += OnOpenRequested;
        _view.DocumentSelectionChanged += OnDocumentSelectionChanged;
    }

    public async Task InitializeAsync()
    {
        try
        {
            var documents = await _listDocumentsHandler.HandleAsync(
                new ListDocumentsQuery());

            if (documents.Count == 0)
            {
                _view.ShowEmptyState();
                _view.SetOpenEnabled(false);
                _view.SetStatus("Ready");

                return;
            }

            var items = documents
                .Select(document => new DocumentListItem(
                    document.Id,
                    document.FileName))
                .ToList();

            _view.ShowDocuments(items);

            var latestDocument = documents[^1];

            _view.SetSelectedDocumentId(
                latestDocument.Id);

            _view.SetOpenEnabled(true);

            _view.SetStatus(
                $"{documents.Count} document(s) imported.");
        }
        catch (Exception)
        {
            _view.SetStatus(
                "Unable to load documents.");

            _view.ShowError(
                "The imported documents could not be loaded.",
                "DeskVault");
        }
    }

    private async void OnImportRequested(
        object? sender,
        EventArgs e)
    {
        string? filePath = _view.SelectedFilePath;

        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        _view.SetImportEnabled(false);
        _view.SetStatus("Importing document...");

        try
        {
            var command = new ImportDocumentCommand(
                filePath,
                null);

            var result =
                await _importDocumentHandler.HandleAsync(command);

            if (result.Status ==
                ImportDocumentResultStatus.Success)
            {
                var documents =
                    await _listDocumentsHandler.HandleAsync(
                        new ListDocumentsQuery());

                var items = documents
                    .Select(document => new DocumentListItem(
                        document.Id,
                        document.FileName))
                    .ToList();

                _view.ShowDocuments(items);

                _view.SetSelectedDocumentId(
                    result.DocumentId);

                _view.SetOpenEnabled(
                    result.DocumentId.HasValue);

                _view.SetStatus(
                    result.Description);

                _view.ShowInformation(
                    result.Description,
                    "Import Complete");

                return;
            }

            _view.SetStatus(
                result.Description);

            _view.ShowWarning(
                result.Description,
                "Import Failed");
        }
        catch (Exception)
        {
            _view.SetStatus(
                "Unable to import document.");

            _view.ShowError(
                "An unexpected error occurred while importing the document.",
                "DeskVault");
        }
        finally
        {
            _view.SetImportEnabled(true);
        }
    }

    private async void OnOpenRequested(
        object? sender,
        EventArgs e)
    {
        if (_view.SelectedDocumentId is not Guid documentId)
        {
            return;
        }

        _view.SetOpenEnabled(false);
        _view.SetStatus("Opening document...");

        try
        {
            var result =
                await _openDocumentHandler.HandleAsync(
                    new OpenDocumentQuery(documentId));

            await _view.OpenDocumentAsync(
                result.Content,
                result.FileName);

            _view.SetStatus(
                "Document opened.");
        }
        catch (Exception)
        {
            _view.SetStatus(
                "Unable to open document.");

            _view.ShowError(
                "The document could not be opened.",
                "Open Document");
        }
        finally
        {
            _view.SetOpenEnabled(
                _view.SelectedDocumentId.HasValue);
        }
    }

    private void OnDocumentSelectionChanged(
    object? sender,
    EventArgs e)
    {
        _view.SetOpenEnabled(
            _view.SelectedDocumentId.HasValue);
    }
}