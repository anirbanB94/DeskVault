using DeskVault.Application.Documents.Commands.ImportDocument;
using DeskVault.Application.Documents.Commands.RemoveDocument;
using DeskVault.Application.Documents.Queries.ListDocuments;
using DeskVault.Application.Documents.Queries.OpenDocument;
using DeskVault.Application.Documents.Queries.SearchDocuments;
using DeskVault.Application.Interfaces;
using DeskVault.UI.Resources;
using DeskVault.UI.Services;
using DeskVault.UI.Views;
using Microsoft.Extensions.Logging;

namespace DeskVault.UI.Presenters;

public sealed class MainFormPresenter
{
    private readonly IMainFormView _view;
    private readonly ImportDocumentHandler _importDocumentHandler;
    private readonly RemoveDocumentHandler _removeDocumentHandler;
    private readonly OpenDocumentHandler _openDocumentHandler;
    private readonly ListDocumentsHandler _listDocumentsHandler;
    private readonly SearchDocumentsHandler _searchDocumentsHandler;
    private readonly IDocumentWorkspace _documentWorkspace;
    private readonly IDocumentProcessingService _documentProcessingService;
    private readonly ILogger<MainFormPresenter> _logger;

    public MainFormPresenter(
        IMainFormView view,
        ImportDocumentHandler importDocumentHandler,
        RemoveDocumentHandler removeDocumentHandler,
        OpenDocumentHandler openDocumentHandler,
        ListDocumentsHandler listDocumentsHandler,
        SearchDocumentsHandler searchDocumentsHandler,
        IDocumentWorkspace documentWorkspace,
        IDocumentProcessingService documentProcessingService,
        ILogger<MainFormPresenter> logger)
    {
        _view = view;
        _importDocumentHandler = importDocumentHandler;
        _removeDocumentHandler = removeDocumentHandler;
        _openDocumentHandler = openDocumentHandler;
        _listDocumentsHandler = listDocumentsHandler;
        _searchDocumentsHandler = searchDocumentsHandler;
        _documentWorkspace = documentWorkspace;
        _documentProcessingService = documentProcessingService;
        _logger = logger;

        _view.ImportRequested += OnImportRequested;
        _view.OpenRequested += OnOpenRequested;
        _view.RemoveRequested += OnRemoveRequested;
        _view.DocumentSelectionChanged += OnDocumentSelectionChanged;
        _view.SearchRequested += OnSearchRequested;
        _view.ReprocessRequested += OnReprocessRequested;
        _documentWorkspace.DocumentRemoved += OnDocumentRemoved;
    }

    public async Task InitializeAsync()
    {
        _logger.LogInformation(
            LogMessages.MainWorkspaceInitializationStarted);

        try
        {
            var documentCount =
                await RefreshDocumentsAsync();

            if (documentCount == 0)
            {
                _logger.LogInformation(
                    LogMessages.MainWorkspaceInitializedWithoutDocuments);

                _view.SetStatus(
                    UiMessages.ReadyStatus);

                return;
            }

            _view.SetOpenEnabled(true);

            _view.SetStatus(
                $"{documentCount} document(s) imported.");

            _logger.LogInformation(
                LogMessages.MainWorkspaceInitializedWithDocuments,
                documentCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                LogMessages.MainWorkspaceInitializationFailed);

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
            _logger.LogDebug(
                LogMessages.DocumentImportCancelled);

            return;
        }

        _logger.LogInformation(
            LogMessages.DocumentImportStarted);

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

                _logger.LogInformation(
                    LogMessages.DocumentImportCompleted);

                _view.ShowInformation(
                    result.Description,
                    UiMessages.ImportCompleteTitle);

                return;
            }

            _logger.LogWarning(
                LogMessages.DocumentImportRejected);

            _view.SetStatus(
                result.Description);

            _view.ShowWarning(
                result.Description,
                UiMessages.ImportFailedTitle);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                LogMessages.DocumentImportFailed);

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
            _logger.LogDebug(
                LogMessages.DocumentOpenSkippedWithoutSelection);

            return;
        }

        _logger.LogInformation(
            LogMessages.DocumentOpenStarted);

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

            _logger.LogInformation(
                LogMessages.DocumentOpenCompleted);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                LogMessages.DocumentOpenFailed);

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
            _logger.LogDebug(
                LogMessages.DocumentRemovalSkippedWithoutSelection);

            return;
        }

        string? fileName =
            _view.SelectedDocumentFileName;

        if (string.IsNullOrWhiteSpace(fileName))
        {
            _logger.LogWarning(
                LogMessages.DocumentRemovalSkippedWithoutFileName);

            return;
        }

        if (!_view.ConfirmRemoval(fileName))
        {
            _logger.LogDebug(
                LogMessages.DocumentRemovalCancelled);

            return;
        }

        _logger.LogInformation(
            LogMessages.DocumentRemovalStarted);

        _view.SetRemoveEnabled(false);
        _view.SetOpenEnabled(false);
        _view.SetImportEnabled(false);
        _view.SetReprocessEnabled(false);
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

                _logger.LogInformation(
                    LogMessages.DocumentRemovalCompleted);

                _view.ShowInformation(
                    result.Message,
                    UiMessages.DocumentRemovedTitle);

                return;
            }

            _logger.LogWarning(
                LogMessages.DocumentRemovalRejected);

            _view.SetStatus(result.Message);

            _view.ShowWarning(
                result.Message,
                UiMessages.RemoveFailedTitle);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                LogMessages.DocumentRemovalFailed);

            _view.SetStatus(
                UiMessages.UnableToRemoveDocumentStatus);

            _view.ShowError(
                UiMessages.UnableToRemoveDocument,
                UiMessages.DeskVaultTitle);
        }
        finally
        {
            _view.SetImportEnabled(true);

            bool hasSelection =
                _view.SelectedDocumentId.HasValue;

            _view.SetOpenEnabled(hasSelection);
            _view.SetRemoveEnabled(hasSelection);
            _view.SetReprocessEnabled(hasSelection);
        }
    }

    private async void OnReprocessRequested(
        object? sender,
        EventArgs e)
    {
        if (_view.SelectedDocumentId is not Guid documentId)
        {
            _logger.LogDebug(
                LogMessages.DocumentReprocessSkippedWithoutSelection);

            return;
        }

        _logger.LogInformation(
            LogMessages.DocumentReprocessStarted);

        _view.SetReprocessEnabled(false);
        _view.SetOpenEnabled(false);
        _view.SetRemoveEnabled(false);
        _view.SetImportEnabled(false);
        _view.SetStatus(
            UiMessages.ReprocessingDocumentStatus);

        try
        {
            await _documentProcessingService.ProcessAsync(
                documentId);

            await RefreshDocumentsAsync();

            _view.SetStatus(
                UiMessages.DocumentReprocessedStatus);

            _logger.LogInformation(
                LogMessages.DocumentReprocessCompleted);

            _view.ShowInformation(
                UiMessages.DocumentReprocessedStatus,
                UiMessages.ReprocessDocumentTitle);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                LogMessages.DocumentReprocessFailed);

            _view.SetStatus(
                UiMessages.UnableToReprocessDocumentStatus);

            _view.ShowError(
                UiMessages.UnableToReprocessDocument,
                UiMessages.ReprocessDocumentTitle);
        }
        finally
        {
            bool hasSelection =
                _view.SelectedDocumentId.HasValue;

            _view.SetImportEnabled(true);
            _view.SetOpenEnabled(hasSelection);
            _view.SetRemoveEnabled(hasSelection);
            _view.SetReprocessEnabled(hasSelection);
        }
    }

    private async void OnSearchRequested(
        object? sender,
        EventArgs e)
    {
        try
        {
            string searchText =
                _view.SearchText.Trim();

            if (string.IsNullOrWhiteSpace(searchText))
            {
                _logger.LogDebug(
                    LogMessages.DocumentSearchCleared);

                await RefreshDocumentsAsync();

                return;
            }

            _logger.LogInformation(
                LogMessages.DocumentSearchStarted);

            var results =
                await _searchDocumentsHandler.HandleAsync(
                    new SearchDocumentsQuery(searchText));

            var documents = results
                .GroupBy(result => result.DocumentId)
                .Select(group => group.First())
                .Select(result => new DocumentListItem(
                    result.DocumentId,
                    result.FileName))
                .ToList();

            if (documents.Count == 0)
            {
                _logger.LogInformation(
                    LogMessages.DocumentSearchCompletedWithoutResults);

                _view.ShowEmptyState();
                _view.SetOpenEnabled(false);
                _view.SetRemoveEnabled(false);
                _view.SetReprocessEnabled(false);

                return;
            }

            _logger.LogInformation(
                LogMessages.DocumentSearchCompletedWithResults,
                documents.Count);

            _view.ShowDocuments(documents);

            bool hasSelection =
                _view.SelectedDocumentId.HasValue;

            _view.SetOpenEnabled(hasSelection);
            _view.SetRemoveEnabled(hasSelection);
            _view.SetReprocessEnabled(hasSelection);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                LogMessages.DocumentSearchFailed);

            _view.SetStatus(
                $"Search failed: {ex.Message}");

            _view.ShowError(
                ex.ToString(),
                UiMessages.DeskVaultTitle);
        }
    }

    private async Task<int> RefreshDocumentsAsync()
    {
        _logger.LogDebug(
            LogMessages.DocumentListRefreshStarted);

        var documents =
            await _listDocumentsHandler.HandleAsync(
                new ListDocumentsQuery());

        if (documents.Count == 0)
        {
            _logger.LogDebug(
                LogMessages.DocumentListRefreshCompletedWithoutDocuments);

            _view.ShowEmptyState();
            _view.SetOpenEnabled(false);
            _view.SetRemoveEnabled(false);
            _view.SetReprocessEnabled(false);

            return 0;
        }

        var items = documents
            .Select(document => new DocumentListItem(
                document.Id,
                document.FileName))
            .ToList();

        _view.ShowDocuments(items);

        bool hasSelection =
            _view.SelectedDocumentId.HasValue;

        _view.SetOpenEnabled(hasSelection);
        _view.SetRemoveEnabled(hasSelection);
        _view.SetReprocessEnabled(hasSelection);

        _logger.LogDebug(
            LogMessages.DocumentListRefreshCompleted,
            documents.Count);

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
        _view.SetReprocessEnabled(hasSelection);
    }

    private async void OnDocumentRemoved(
        object? sender,
        EventArgs e)
    {
        _logger.LogDebug(
            LogMessages.DocumentWorkspaceRemovalNotificationReceived);

        await RefreshDocumentsAsync();
    }
}
