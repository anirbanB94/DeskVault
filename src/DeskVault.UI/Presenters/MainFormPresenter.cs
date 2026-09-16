using DeskVault.Application.Documents.Commands.ImportDocument;
using DeskVault.Application.Documents.Commands.RemoveDocument;
using DeskVault.Application.Documents.Extraction;
using DeskVault.Application.Documents.Queries.GetDocument;
using DeskVault.Application.Documents.Queries.ListDocuments;
using DeskVault.Application.Documents.Queries.OpenDocument;
using DeskVault.Application.Documents.Queries.SearchDocuments;
using DeskVault.Application.Interfaces;
using DeskVault.UI.Resources;
using DeskVault.UI.Services;
using DeskVault.UI.Views;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
    private readonly DocumentTextExtractorResolver _documentTextExtractorResolver;
    private readonly ILogger<MainFormPresenter> _logger;
    private readonly int _searchPageSize;

    private string? _currentSearchText;
    private SearchDocumentsContinuation? _currentSearchContinuation;
    private CancellationTokenSource? _searchCancellationTokenSource;
    private long _searchOperationVersion;

    public MainFormPresenter(
        IMainFormView view,
        ImportDocumentHandler importDocumentHandler,
        RemoveDocumentHandler removeDocumentHandler,
        OpenDocumentHandler openDocumentHandler,
        ListDocumentsHandler listDocumentsHandler,
        SearchDocumentsHandler searchDocumentsHandler,
        IDocumentWorkspace documentWorkspace,
        IDocumentProcessingService documentProcessingService,
        DocumentTextExtractorResolver documentTextExtractorResolver,
        ILogger<MainFormPresenter> logger,
        IOptions<SearchOptions> searchOptions)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(importDocumentHandler);
        ArgumentNullException.ThrowIfNull(removeDocumentHandler);
        ArgumentNullException.ThrowIfNull(openDocumentHandler);
        ArgumentNullException.ThrowIfNull(listDocumentsHandler);
        ArgumentNullException.ThrowIfNull(searchDocumentsHandler);
        ArgumentNullException.ThrowIfNull(documentWorkspace);
        ArgumentNullException.ThrowIfNull(documentProcessingService);
        ArgumentNullException.ThrowIfNull(documentTextExtractorResolver);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(searchOptions);

        _view = view;
        _importDocumentHandler = importDocumentHandler;
        _removeDocumentHandler = removeDocumentHandler;
        _openDocumentHandler = openDocumentHandler;
        _listDocumentsHandler = listDocumentsHandler;
        _searchDocumentsHandler = searchDocumentsHandler;
        _documentWorkspace = documentWorkspace;
        _documentProcessingService = documentProcessingService;
        _documentTextExtractorResolver = documentTextExtractorResolver;
        _logger = logger;

        _searchPageSize = searchOptions.Value.PageSize;

        if (_searchPageSize <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(searchOptions),
                "Search page size must be greater than zero.");
        }

        _view.ImportRequested += OnImportRequested;
        _view.OpenRequested += OnOpenRequested;
        _view.RemoveRequested += OnRemoveRequested;
        _view.DocumentSelectionChanged += OnDocumentSelectionChanged;
        _view.SearchRequested += OnSearchRequested;
        _view.LoadMoreSearchResultsRequested +=
            OnLoadMoreSearchResultsRequested;
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

            UpdateReprocessEnabled();

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
                if (result.DocumentId is not Guid documentId)
                {
                    throw new InvalidOperationException(
                        "A successful document import did not return a document identifier.");
                }

                string fileName =
                    Path.GetFileName(filePath);

                if (_documentTextExtractorResolver.CanResolve(
                    fileName))
                {
                    await _documentProcessingService.ProcessAsync(
                        documentId);
                }
                else
                {
                    _logger.LogDebug(
                        "Document processing skipped because no text extractor is available for {FileName}.",
                        fileName);
                }

                await RefreshDocumentsAsync();

                _view.SetSelectedDocumentId(
                    result.DocumentId);

                _view.SetOpenEnabled(
                    result.DocumentId.HasValue);

                UpdateReprocessEnabled();

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

            UpdateReprocessEnabled();
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

            UpdateReprocessEnabled();
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

        string? fileName =
            _view.SelectedDocumentFileName;

        if (string.IsNullOrWhiteSpace(fileName))
        {
            _logger.LogDebug(
                LogMessages.DocumentReprocessSkippedWithoutFileName);

            _view.SetReprocessEnabled(false);

            return;
        }

        if (!_documentTextExtractorResolver.CanResolve(fileName))
        {
            _logger.LogDebug(
                "Document reprocessing skipped because no text extractor is available for {FileName}.",
                fileName);

            _view.SetReprocessEnabled(false);

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

            UpdateReprocessEnabled();
        }
    }

    private async void OnSearchRequested(
        object? sender,
        EventArgs e)
    {
        var operation =
            BeginSearchOperation();

        CancellationTokenSource cancellationTokenSource =
            operation.CancellationTokenSource;

        CancellationToken cancellationToken =
            cancellationTokenSource.Token;

        long operationVersion =
            operation.Version;

        try
        {
            string searchText =
                _view.SearchText?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(searchText))
            {
                _currentSearchText = null;
                _currentSearchContinuation = null;

                _logger.LogDebug(
                    LogMessages.DocumentSearchCleared);

                if (!IsCurrentSearchOperation(operationVersion))
                {
                    return;
                }

                await RefreshDocumentsAsync();

                return;
            }

            _currentSearchText = searchText;
            _currentSearchContinuation = null;

            string? searchFileType =
                _view.SearchFileType;

            IReadOnlyList<string>? fileTypes =
                string.IsNullOrWhiteSpace(searchFileType)
                    ? null
                    : [searchFileType];

            _logger.LogInformation(
                LogMessages.DocumentSearchStarted);

            var page =
                await _searchDocumentsHandler.HandleAsync(
                    new SearchDocumentsQuery(
                        searchText,
                        FileTypes: fileTypes,
                        Continuation: null,
                        Limit: _searchPageSize),
                    cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            if (!IsCurrentSearchOperation(operationVersion))
            {
                return;
            }

            var searchResults =
                MapSearchResults(page.Results);

            if (searchResults.Count == 0)
            {
                _logger.LogInformation(
                    LogMessages.DocumentSearchCompletedWithoutResults);

                _view.ShowEmptyState();
                _view.SetOpenEnabled(false);
                _view.SetRemoveEnabled(false);
                _view.SetReprocessEnabled(false);
                _view.SetLoadMoreEnabled(false);

                return;
            }

            _currentSearchContinuation =
                page.Continuation;

            _logger.LogInformation(
                LogMessages.DocumentSearchCompletedWithResults,
                searchResults.Count);

            _view.ShowSearchResults(
                searchResults);

            _view.SetLoadMoreEnabled(
                page.HasMore);

            bool hasSelection =
                _view.SelectedDocumentId.HasValue;

            _view.SetOpenEnabled(hasSelection);
            _view.SetRemoveEnabled(hasSelection);

            UpdateReprocessEnabled();
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug(
                "Document search operation was superseded.");
        }
        catch (Exception ex)
        {
            if (!IsCurrentSearchOperation(operationVersion))
            {
                return;
            }

            _logger.LogError(
                ex,
                LogMessages.DocumentSearchFailed);

            _view.SetStatus(
                $"Search failed: {ex.Message}");

            _view.ShowError(
                ex.ToString(),
                UiMessages.DeskVaultTitle);
        }
        finally
        {
            CompleteSearchOperation(
                cancellationTokenSource);
        }
    }

    private async void OnLoadMoreSearchResultsRequested(
        object? sender,
        EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_currentSearchText) ||
            _currentSearchContinuation is null)
        {
            _view.SetLoadMoreEnabled(false);

            return;
        }

        var operation =
            BeginSearchOperation();

        CancellationTokenSource cancellationTokenSource =
            operation.CancellationTokenSource;

        CancellationToken cancellationToken =
            cancellationTokenSource.Token;

        long operationVersion =
            operation.Version;

        try
        {
            _logger.LogInformation(
                LogMessages.DocumentSearchStarted);

            _view.SetLoadMoreEnabled(false);

            string? searchFileType =
                _view.SearchFileType;

            IReadOnlyList<string>? fileTypes =
                string.IsNullOrWhiteSpace(searchFileType)
                    ? null
                    : [searchFileType];

            SearchDocumentsContinuation continuation =
                _currentSearchContinuation;

            string searchText =
                _currentSearchText;

            var page =
                await _searchDocumentsHandler.HandleAsync(
                    new SearchDocumentsQuery(
                        searchText,
                        FileTypes: fileTypes,
                        Continuation: continuation,
                        Limit: _searchPageSize),
                    cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            if (!IsCurrentSearchOperation(operationVersion))
            {
                return;
            }

            var searchResults =
                MapSearchResults(page.Results);

            if (searchResults.Count == 0)
            {
                _currentSearchContinuation = null;
                _view.SetLoadMoreEnabled(false);

                return;
            }

            _view.AppendSearchResults(
                searchResults);

            _currentSearchContinuation =
                page.Continuation;

            _view.SetLoadMoreEnabled(
                page.HasMore);

            _logger.LogInformation(
                LogMessages.DocumentSearchCompletedWithResults,
                searchResults.Count);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug(
                "Document search operation was superseded.");

            if (IsCurrentSearchOperation(operationVersion))
            {
                _view.SetLoadMoreEnabled(
                    _currentSearchContinuation is not null);
            }
        }
        catch (Exception ex)
        {
            if (!IsCurrentSearchOperation(operationVersion))
            {
                return;
            }

            _logger.LogError(
                ex,
                LogMessages.DocumentSearchFailed);

            _view.SetLoadMoreEnabled(true);

            _view.SetStatus(
                $"Search failed: {ex.Message}");

            _view.ShowError(
                ex.ToString(),
                UiMessages.DeskVaultTitle);
        }
        finally
        {
            CompleteSearchOperation(
                cancellationTokenSource);
        }
    }

    private (
        CancellationTokenSource CancellationTokenSource,
        long Version) BeginSearchOperation()
    {
        _searchCancellationTokenSource?.Cancel();

        CancellationTokenSource current =
            new();

        _searchCancellationTokenSource =
            current;

        long version =
            Interlocked.Increment(
                ref _searchOperationVersion);

        return (
            current,
            version);
    }

    private bool IsCurrentSearchOperation(
        long operationVersion)
    {
        return operationVersion ==
            Volatile.Read(
                ref _searchOperationVersion);
    }

    private void CompleteSearchOperation(
        CancellationTokenSource cancellationTokenSource)
    {
        if (ReferenceEquals(
            _searchCancellationTokenSource,
            cancellationTokenSource))
        {
            _searchCancellationTokenSource = null;
        }

        cancellationTokenSource.Dispose();
    }

    private static List<SearchResultListItem> MapSearchResults(
        IReadOnlyList<SearchDocumentsResult> results)
    {
        return results
            .Select(result => new SearchResultListItem(
                result.DocumentId,
                result.DisplayName,
                result.FileName,
                result.Matches
                    .Select(match => match.Context)
                    .FirstOrDefault(
                        context => !string.IsNullOrWhiteSpace(context))
                    ?? string.Empty,
                result.MatchCount))
            .ToList();
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

        UpdateReprocessEnabled();

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

        UpdateReprocessEnabled();
    }

    private void UpdateReprocessEnabled()
    {
        if (_view.SelectedDocumentId is not Guid)
        {
            _view.SetReprocessEnabled(false);

            return;
        }

        string? fileName =
            _view.SelectedDocumentFileName;

        bool canReprocess =
            !string.IsNullOrWhiteSpace(fileName) &&
            _documentTextExtractorResolver.CanResolve(fileName);

        _view.SetReprocessEnabled(
            canReprocess);
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
