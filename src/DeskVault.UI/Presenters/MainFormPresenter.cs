using DeskVault.Application.Documents.Commands.ImportDocument;
using DeskVault.Application.Documents.Commands.RemoveDocument;
using DeskVault.Application.Documents.Queries.ListDocuments;
using DeskVault.Application.Documents.Queries.OpenDocument;
using DeskVault.UI.Resources;
using DeskVault.UI.Services;
using DeskVault.UI.Views;

namespace DeskVault.UI.Presenters;

public sealed class MainFormPresenter
{
    private readonly IMainFormView _view;
    private readonly ImportDocumentHandler _importDocumentHandler;
    private readonly RemoveDocumentHandler _removeDocumentHandler;
    private readonly OpenDocumentHandler _openDocumentHandler;
    private readonly ListDocumentsHandler _listDocumentsHandler;
    private readonly IDocumentWorkspace _documentWorkspace;

    public MainFormPresenter(
        IMainFormView view,
        ImportDocumentHandler importDocumentHandler,
        RemoveDocumentHandler removeDocumentHandler,
        OpenDocumentHandler openDocumentHandler,
        ListDocumentsHandler listDocumentsHandler,
        IDocumentWorkspace documentWorkspace)
    {
        _view = view;
        _importDocumentHandler = importDocumentHandler;
        _removeDocumentHandler = removeDocumentHandler;
        _openDocumentHandler = openDocumentHandler;
        _listDocumentsHandler = listDocumentsHandler;
        _documentWorkspace = documentWorkspace;

        _view.ImportRequested += OnImportRequested;
        _view.OpenRequested += OnOpenRequested;
        _view.RemoveRequested += OnRemoveRequested;
        _view.DocumentSelectionChanged += OnDocumentSelectionChanged;
        _documentWorkspace.DocumentRemoved += OnDocumentRemoved;
    }

    public async Task InitializeAsync()
    {
        try
        {
            var documentCount =
                await RefreshDocumentsAsync();

            if (documentCount == 0)
            {
                _view.SetStatus(
                    UiMessages.ReadyStatus);

                return;
            }

            _view.SetOpenEnabled(true);

            _view.SetStatus(
                $"{documentCount} document(s) imported.");
        }
        catch (Exception)
        {
            _view.SetStatus(
                UiMessages.UnableToLoadDocumentsStatus);

            _view.ShowError(
                UiMessages.UnableToLoadDocuments,
                UiMessages.DeskVaultTitle);
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
        _view.SetStatus(
            UiMessages.ImportingDocumentStatus);

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
                await RefreshDocumentsAsync();

                _view.SetSelectedDocumentId(
                    result.DocumentId);

                _view.SetOpenEnabled(
                    result.DocumentId.HasValue);

                _view.SetStatus(
                    result.Description);

                _view.ShowInformation(
                    result.Description,
                    UiMessages.ImportCompleteTitle);

                return;
            }

            _view.SetStatus(
                result.Description);

            _view.ShowWarning(
                result.Description,
                UiMessages.ImportFailedTitle);
        }
        catch (Exception)
        {
            _view.SetStatus(
                UiMessages.UnexpectedImportError);

            _view.ShowError(
                UiMessages.UnexpectedImportError,
                UiMessages.DeskVaultTitle);
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
        _view.SetStatus(
            UiMessages.OpeningDocumentStatus);

        try
        {
            var result =
                await _openDocumentHandler.HandleAsync(
                    new OpenDocumentQuery(documentId));

            await _documentWorkspace.OpenAsync(
                documentId,
                result.Content,
                result.FileName);

            _view.SetStatus(
                UiMessages.DocumentOpenedStatus);
        }
        catch (Exception)
        {
            _view.SetStatus(
                UiMessages.UnableToOpenDocumentStatus);

            _view.ShowError(
                UiMessages.UnableToOpenDocument,
                UiMessages.OpenDocumentTitle);
        }
        finally
        {
            _view.SetOpenEnabled(
                _view.SelectedDocumentId.HasValue);
        }
    }

    private async void OnRemoveRequested(
        object? sender,
        EventArgs e)
    {
        if (_view.SelectedDocumentId is not Guid documentId)
        {
            return;
        }

        string? fileName =
            _view.SelectedDocumentFileName;

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return;
        }

        if (!_view.ConfirmRemoval(fileName))
        {
            return;
        }

        _view.SetRemoveEnabled(false);
        _view.SetOpenEnabled(false);
        _view.SetImportEnabled(false);
        _view.SetStatus(
            UiMessages.RemovingDocumentStatus);

        try
        {
            var result =
                await _removeDocumentHandler.HandleAsync(
                    new RemoveDocumentCommand(documentId));

            if (result.Status ==
                RemoveDocumentResultStatus.Success)
            {
                await RefreshDocumentsAsync();

                _view.SetStatus(result.Message);

                _view.ShowInformation(
                    result.Message,
                    UiMessages.DocumentRemovedTitle);

                return;
            }

            _view.SetStatus(result.Message);

            _view.ShowWarning(
                result.Message,
                UiMessages.RemoveFailedTitle);
        }
        catch (Exception)
        {
            _view.SetStatus(
                UiMessages.UnableToRemoveDocumentStatus);

            _view.ShowError(
                UiMessages.UnableToRemoveDocument,
                UiMessages.DeskVaultTitle);
        }
        finally
        {
            _view.SetImportEnabled(true);

            _view.SetOpenEnabled(
                _view.SelectedDocumentId.HasValue);

            _view.SetRemoveEnabled(
                _view.SelectedDocumentId.HasValue);
        }
    }

    private async Task<int> RefreshDocumentsAsync()
    {
        var documents =
            await _listDocumentsHandler.HandleAsync(
                new ListDocumentsQuery());

        if (documents.Count == 0)
        {
            _view.ShowEmptyState();
            _view.SetOpenEnabled(false);
            _view.SetRemoveEnabled(false);

            return 0;
        }

        var items = documents
            .Select(document => new DocumentListItem(
                document.Id,
                document.FileName))
            .ToList();

        _view.ShowDocuments(items);

        _view.SetOpenEnabled(
            _view.SelectedDocumentId.HasValue);

        _view.SetRemoveEnabled(
            _view.SelectedDocumentId.HasValue);

        return documents.Count;
    }

    private void OnDocumentSelectionChanged(
        object? sender,
        EventArgs e)
    {
        bool hasSelection =
            _view.SelectedDocumentId.HasValue;

        _view.SetOpenEnabled(hasSelection);
        _view.SetRemoveEnabled(hasSelection);
    }

    private async void OnDocumentRemoved(
        object? sender,
        EventArgs e)
    {
        await RefreshDocumentsAsync();
    }
}
