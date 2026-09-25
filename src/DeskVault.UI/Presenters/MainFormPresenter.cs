using DeskVault.Application.Documents.Commands.ImportDocument;
using DeskVault.Application.Documents.Commands.RemoveDocument;
using DeskVault.Application.Documents.Extraction;
using DeskVault.Application.Documents.Queries.ListDocuments;
using DeskVault.Application.Documents.Queries.SearchDocuments;
using DeskVault.Application.Interfaces;
using DeskVault.Application.Workspaces.Commands.CloseWorkspace;
using DeskVault.Application.Workspaces.Commands.CreateWorkspace;
using DeskVault.Application.Workspaces.Commands.DeleteWorkspace;
using DeskVault.Application.Workspaces.Commands.OpenWorkspace;
using DeskVault.Application.Workspaces.Queries.GetWorkspaces;
using DeskVault.UI.Resources;
using DeskVault.UI.Services.Interfaces;
using DeskVault.UI.Services.Workspace;
using DeskVault.UI.Views;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DeskVault.UI.Presenters;

public sealed class MainFormPresenter
{
    private readonly IMainFormView _view;
    private readonly ImportDocumentHandler _importDocumentHandler;
    private readonly RemoveDocumentHandler _removeDocumentHandler;
    private readonly ListDocumentsHandler _listDocumentsHandler;
    private readonly SearchDocumentsHandler _searchDocumentsHandler;
    private readonly CreateWorkspaceHandler _createWorkspaceHandler;
    private readonly DeleteWorkspaceHandler _deleteWorkspaceHandler;
    private readonly GetWorkspacesHandler _getWorkspacesHandler;
    private readonly OpenWorkspaceHandler _openWorkspaceHandler;
    private readonly CloseWorkspaceHandler _closeWorkspaceHandler;
    private readonly IWorkspacePresentationManager _workspacePresentationManager;
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
        ListDocumentsHandler listDocumentsHandler,
        SearchDocumentsHandler searchDocumentsHandler,
        CreateWorkspaceHandler createWorkspaceHandler,
        DeleteWorkspaceHandler deleteWorkspaceHandler,
        GetWorkspacesHandler getWorkspacesHandler,
        OpenWorkspaceHandler openWorkspaceHandler,
        CloseWorkspaceHandler closeWorkspaceHandler,
        IWorkspacePresentationManager workspacePresentationManager,
        IDocumentProcessingService documentProcessingService,
        DocumentTextExtractorResolver documentTextExtractorResolver,
        ILogger<MainFormPresenter> logger,
        IOptions<SearchOptions> searchOptions)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(importDocumentHandler);
        ArgumentNullException.ThrowIfNull(removeDocumentHandler);
        ArgumentNullException.ThrowIfNull(listDocumentsHandler);
        ArgumentNullException.ThrowIfNull(searchDocumentsHandler);
        ArgumentNullException.ThrowIfNull(createWorkspaceHandler);
        ArgumentNullException.ThrowIfNull(deleteWorkspaceHandler);
        ArgumentNullException.ThrowIfNull(getWorkspacesHandler);
        ArgumentNullException.ThrowIfNull(openWorkspaceHandler);
        ArgumentNullException.ThrowIfNull(closeWorkspaceHandler);
        ArgumentNullException.ThrowIfNull(workspacePresentationManager);
        ArgumentNullException.ThrowIfNull(documentProcessingService);
        ArgumentNullException.ThrowIfNull(documentTextExtractorResolver);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(searchOptions);

        _view = view;
        _importDocumentHandler = importDocumentHandler;
        _removeDocumentHandler = removeDocumentHandler;
        _listDocumentsHandler = listDocumentsHandler;
        _searchDocumentsHandler = searchDocumentsHandler;
        _createWorkspaceHandler = createWorkspaceHandler;
        _deleteWorkspaceHandler = deleteWorkspaceHandler;
        _getWorkspacesHandler = getWorkspacesHandler;
        _openWorkspaceHandler = openWorkspaceHandler;
        _closeWorkspaceHandler = closeWorkspaceHandler;
        _workspacePresentationManager = workspacePresentationManager;
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
        _view.LoadMoreSearchResultsRequested += OnLoadMoreSearchResultsRequested;
        _view.ReprocessRequested += OnReprocessRequested;
        _view.WorkspaceSelectionChanged += OnWorkspaceSelectionChanged;
        _view.WorkspaceCreateRequested += OnWorkspaceCreateRequested;
        _view.WorkspaceOpenRequested += OnWorkspaceOpenRequested;
        _view.WorkspaceRemoveRequested += OnWorkspaceRemoveRequested;
    }

    public async Task InitializeAsync()
    {
        _logger.LogInformation(
            LogMessages.MainWorkspaceInitializationStarted);

        try
        {
            var documentCount =
                await RefreshDocumentsAsync();

            await RefreshWorkspacesAsync();

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
                        LogMessages.DocumentProcessingSkippedWithoutTextExtractor,
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
            WorkspacePresentation? existingTemporaryPresentation =
                _workspacePresentationManager.FindTemporaryByDocument(
                    documentId);

            if (existingTemporaryPresentation is not null)
            {
                existingTemporaryPresentation.Activate();

                _view.SetStatus(
                    UiMessages.DocumentOpenedStatus);

                _logger.LogInformation(
                    LogMessages.DocumentOpenCompleted);

                return;
            }

            var workspaceResult =
                await _createWorkspaceHandler.HandleAsync(
                    new CreateWorkspaceCommand(
                        Name: null,
                        Description: null,
                        IsPersistent: false,
                        DocumentIds: [documentId]));

            if (workspaceResult.Status !=
                CreateWorkspaceResultStatus.Success ||
                workspaceResult.Workspace is null)
            {
                _logger.LogWarning(
                    LogMessages.TemporaryWorkspaceCreationFailed);

                _view.SetStatus(
                    workspaceResult.Description);

                _view.ShowError(
                    workspaceResult.Description,
                    UiMessages.OpenDocumentTitle);

                return;
            }

            var presentation =
                _workspacePresentationManager.GetOrCreate(
                    workspaceResult.Workspace.Id,
                    workspaceResult.Workspace.TypeOfWorkspace,
                    workspaceResult.Workspace.Name,
                    workspaceResult.Workspace.Description);

            SubscribeWorkspacePresentation(
                presentation);

            await presentation.SetWorkspaceDocumentsAsync(
                workspaceResult.Workspace.Memberships.Select(
                    membership => membership.DocumentId));

            await presentation.OpenDocumentAsync(
                documentId);

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

    private void OnWorkspaceSelectionChanged(
        object? sender,
        EventArgs e)
    {
        if (_view.SelectedWorkspaceId is not Guid workspaceId)
        {
            _view.SetWorkspaceOpenEnabled(false);
            _view.SetWorkspaceRemoveEnabled(false);

            return;
        }

        _view.SetWorkspaceOpenEnabled(true);
        _view.SetWorkspaceRemoveEnabled(true);

        _logger.LogDebug(
            LogMessages.WorkspaceSelected,
            workspaceId);
    }

    private async void OnWorkspaceCreateRequested(
        object? sender,
        EventArgs e)
    {
        _logger.LogInformation(
            LogMessages.WorkspaceCreationRequested);

        WorkspaceCreateRequest? request =
            _view.ShowCreateWorkspaceDialog();

        if (request is null)
        {
            return;
        }

        try
        {
            var result =
                await _createWorkspaceHandler.HandleAsync(
                    new CreateWorkspaceCommand(
                        Name: request.Name,
                        Description: request.Description,
                        IsPersistent: true,
                        DocumentIds: []));

            if (result.Status !=
                CreateWorkspaceResultStatus.Success ||
                result.Workspace is null)
            {
                _logger.LogWarning(
                    LogMessages.WorkspaceCreationFailed,
                    result.Description);

                _view.SetStatus(
                    result.Description);

                _view.ShowWarning(
                    result.Description,
                    UiMessages.CreateWorkspaceTitle);

                return;
            }

            await RefreshWorkspacesAsync();

            _view.SetSelectedWorkspaceId(
                result.Workspace.Id);

            await OpenWorkspaceAsync(
                result.Workspace.Id);

            _logger.LogInformation(
                LogMessages.WorkspaceCreatedAndOpened,
                result.Workspace.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                LogMessages.WorkspaceCreationUnexpectedFailure);

            _view.SetStatus(
                UiMessages.UnableToCreateWorkspace);

            _view.ShowError(
                ex.ToString(),
                UiMessages.CreateWorkspaceTitle);
        }
    }

    private async void OnWorkspaceOpenRequested(
        object? sender,
        EventArgs e)
    {
        if (_view.SelectedWorkspaceId is not Guid workspaceId)
        {
            _logger.LogDebug(
                LogMessages.WorkspaceOpenSkippedWithoutSelection);

            return;
        }

        await OpenWorkspaceAsync(workspaceId);
    }

    private async void OnWorkspaceRemoveRequested(
        object? sender,
        EventArgs e)
    {
        if (_view.SelectedWorkspaceId is not Guid workspaceId)
        {
            _logger.LogDebug(
                LogMessages.WorkspaceRemovalSkippedWithoutSelection);

            return;
        }

        string workspaceName =
            _view.SelectedWorkspaceName ??
            UiMessages.CurrentWorkspaceFallbackName;

        await DeleteWorkspaceAsync(
            workspaceId,
            workspaceName,
            clearMainWorkspaceSelection: true);
    }

    private async void OnWorkspaceDocumentsUpdated(
        object? sender,
        EventArgs e)
    {
        await RefreshWorkspacesAsync();
    }

    private async void OnWorkspaceDetailsUpdated(
        object? sender,
        EventArgs e)
    {
        await RefreshWorkspacesAsync();
    }

    private async void OnWorkspaceDeleteRequested(
        object? sender,
        EventArgs e)
    {
        if (sender is not WorkspacePresentation presentation)
        {
            _logger.LogWarning(
                LogMessages.WorkspaceDeletionUnexpectedSender);

            return;
        }

        await DeleteWorkspaceAsync(
            presentation.WorkspaceId,
            presentation.WorkspaceName ??
                UiMessages.CurrentWorkspaceFallbackName,
            clearMainWorkspaceSelection:
                _view.SelectedWorkspaceId ==
                presentation.WorkspaceId);
    }

    private async void OnWorkspaceCloseRequested(
        object? sender,
        EventArgs e)
    {
        if (sender is not WorkspacePresentation presentation)
        {
            _logger.LogWarning(
                LogMessages.WorkspaceCloseUnexpectedSender);

            return;
        }

        Guid workspaceId =
            presentation.WorkspaceId;

        _logger.LogInformation(
            LogMessages.WorkspaceCloseRequested,
            workspaceId);

        try
        {
            var result =
                await _closeWorkspaceHandler.HandleAsync(
                    new CloseWorkspaceCommand(
                        workspaceId));

            if (result.Status !=
                CloseWorkspaceResultStatus.Success)
            {
                _logger.LogWarning(
                    LogMessages.WorkspaceCloseRejected,
                    workspaceId,
                    result.Description);

                _view.SetStatus(
                    result.Description);

                _view.ShowWarning(
                    result.Description,
                    UiMessages.CloseWorkspace);

                return;
            }

            _workspacePresentationManager.Close(
                workspaceId);

            _view.SetStatus(
                result.Description);

            _logger.LogInformation(
                LogMessages.WorkspaceClosed,
                workspaceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                LogMessages.WorkspaceCloseFailed,
                workspaceId);

            _view.SetStatus(
                UiMessages.UnableToCloseWorkspace);

            _view.ShowError(
                ex.ToString(),
                UiMessages.CloseWorkspace);
        }
    }

    private async Task DeleteWorkspaceAsync(
        Guid workspaceId,
        string workspaceName,
        bool clearMainWorkspaceSelection)
    {
        if (!_view.ConfirmWorkspaceRemoval(
            workspaceName))
        {
            _logger.LogInformation(
                LogMessages.WorkspaceRemovalCancelled,
                workspaceId);

            return;
        }

        _logger.LogInformation(
            LogMessages.WorkspaceRemovalRequested,
            workspaceId);

        try
        {
            var openResult =
                await _openWorkspaceHandler.HandleAsync(
                    new OpenWorkspaceCommand(workspaceId));

            if (openResult.Status !=
                    OpenWorkspaceResultStatus.Activated &&
                openResult.Status !=
                    OpenWorkspaceResultStatus.AlreadyActive)
            {
                _logger.LogWarning(
                    LogMessages.WorkspaceRemovalActivationFailed,
                    workspaceId,
                    openResult.Description);

                _view.SetStatus(
                    openResult.Description);

                _view.ShowWarning(
                    openResult.Description,
                    UiMessages.DeleteWorkspace);

                return;
            }

            var result =
                await _deleteWorkspaceHandler.HandleAsync(
                    new DeleteWorkspaceCommand(workspaceId));

            if (result.Status !=
                DeleteWorkspaceResultStatus.Success)
            {
                _logger.LogWarning(
                    LogMessages.WorkspaceRemovalRejected,
                    workspaceId,
                    result.Description);

                _view.SetStatus(
                    result.Description);

                _view.ShowWarning(
                    result.Description,
                    UiMessages.DeleteWorkspace);

                return;
            }

            _workspacePresentationManager.Close(
                workspaceId);

            await RefreshWorkspacesAsync();

            if (clearMainWorkspaceSelection)
            {
                _view.SetSelectedWorkspaceId(null);
            }

            _view.SetStatus(
                result.Description);

            _logger.LogInformation(
                LogMessages.WorkspaceRemoved,
                workspaceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                LogMessages.WorkspaceRemovalFailed,
                workspaceId);

            _view.SetStatus(
                UiMessages.UnableToRemoveWorkspace);

            _view.ShowError(
                ex.ToString(),
                UiMessages.DeleteWorkspace);
        }
    }

    private async Task OpenWorkspaceAsync(
        Guid workspaceId)
    {
        _logger.LogInformation(
            LogMessages.WorkspaceOpenStarted,
            workspaceId);

        try
        {
            var result =
                await _openWorkspaceHandler.HandleAsync(
                    new OpenWorkspaceCommand(workspaceId));

            if (result.Status != OpenWorkspaceResultStatus.Activated &&
                result.Status != OpenWorkspaceResultStatus.AlreadyActive)
            {
                _logger.LogWarning(
                    LogMessages.WorkspaceOpenRejected,
                    workspaceId,
                    result.Description);

                _view.SetStatus(
                    result.Description);

                _view.ShowWarning(
                    result.Description,
                    UiMessages.OpenWorkspaceTitle);

                return;
            }

            if (result.MissingDocumentIds.Count > 0)
            {
                _logger.LogWarning(
                    LogMessages.WorkspaceOpenedWithMissingDocuments,
                    workspaceId,
                    result.MissingDocumentIds.Count);

                _view.ShowWarning(
                    result.Description,
                    UiMessages.OpenWorkspaceTitle);
            }

            if (result.Workspace is null)
            {
                throw new InvalidOperationException(
                    "A successful workspace open did not return a workspace.");
            }

            var presentation =
                _workspacePresentationManager.GetOrCreate(
                    result.Workspace.Id,
                    result.Workspace.TypeOfWorkspace,
                    result.Workspace.Name,
                    result.Workspace.Description);

            SubscribeWorkspacePresentation(
                presentation);

            await presentation.SetWorkspaceDocumentsAsync(
                result.Workspace.Memberships.Select(
                    membership => membership.DocumentId));

            foreach (var membership in
                     result.Workspace.Memberships.OrderBy(
                         membership => membership.Order))
            {
                if (!presentation.ContainsDocument(
                        membership.DocumentId))
                {
                    await presentation.OpenDocumentAsync(
                        membership.DocumentId);
                }
            }

            if (result.Workspace.LastActiveDocumentId is Guid lastActiveDocumentId)
            {
                presentation.ActivateDocument(
                    lastActiveDocumentId);
            }

            _view.SetStatus(
                result.Description);

            _logger.LogInformation(
                LogMessages.WorkspaceOpened,
                workspaceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                LogMessages.WorkspaceOpenFailed,
                workspaceId);

            _view.SetStatus(
                UiMessages.UnableToOpenWorkspace);

            _view.ShowError(
                ex.ToString(),
                UiMessages.OpenWorkspaceTitle);
        }
    }

    private void SubscribeWorkspacePresentation(
        WorkspacePresentation presentation)
    {
        presentation.DocumentRemoved -=
            OnDocumentRemoved;

        presentation.DocumentRemoved +=
            OnDocumentRemoved;

        presentation.WorkspaceDetailsUpdated -=
            OnWorkspaceDetailsUpdated;

        presentation.WorkspaceDetailsUpdated +=
            OnWorkspaceDetailsUpdated;

        presentation.WorkspaceDeleteRequested -=
            OnWorkspaceDeleteRequested;

        presentation.WorkspaceDeleteRequested +=
            OnWorkspaceDeleteRequested;

        presentation.WorkspaceDocumentsUpdated -=
            OnWorkspaceDocumentsUpdated;

        presentation.WorkspaceDocumentsUpdated +=
            OnWorkspaceDocumentsUpdated;

        presentation.WorkspaceCloseRequested -=
            OnWorkspaceCloseRequested;

        presentation.WorkspaceCloseRequested +=
            OnWorkspaceCloseRequested;
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
                await RefreshWorkspacesAsync();

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
                LogMessages.DocumentReprocessSkippedWithoutTextExtractor,
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

            _logger.LogInformation(
                LogMessages.DocumentSearchStarted);

            string? searchFileType =
                _view.SearchFileType;

            IReadOnlyList<string>? fileTypes =
                string.IsNullOrWhiteSpace(searchFileType)
                    ? null
                    : [searchFileType];

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
                LogMessages.DocumentSearchSuperseded);
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
                LogMessages.DocumentSearchSuperseded);

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
            _view.SetDocumentsCount(0);
            _view.SetOpenEnabled(false);
            _view.SetRemoveEnabled(false);
            _view.SetReprocessEnabled(false);

            return 0;
        }

        var items = documents
            .Select(document => new DocumentListItem(
                document.Id,
                document.FileName,
                Path.GetExtension(document.FileName)
                    .TrimStart('.')
                    .ToUpperInvariant(),
                new DateTimeOffset(
                    DateTime.SpecifyKind(
                        document.ImportedAt,
                        DateTimeKind.Utc)),
                document.Status.ToString()))
            .ToList();

        _view.ShowDocuments(items);
        _view.SetDocumentsCount(documents.Count);

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

    private async Task RefreshWorkspacesAsync()
    {
        _logger.LogDebug(
            LogMessages.WorkspaceListRefreshStarted);

        var result =
            await _getWorkspacesHandler.HandleAsync(
                new GetWorkspacesQuery());

        if (result.Status != GetWorkspacesResultStatus.Success)
        {
            _logger.LogWarning(
                LogMessages.WorkspaceListRefreshFailed,
                result.Description);

            _view.ShowWarning(
                result.Description,
                UiMessages.Workspaces);

            return;
        }

        var documents =
            await _listDocumentsHandler.HandleAsync(
                new ListDocumentsQuery());

        var documentNames =
            documents.ToDictionary(
                document => document.Id,
                document => document.FileName);

        var items = result.Workspaces
            .Select(workspace => new WorkspaceListItem(
                workspace.Id,
                workspace.Name ?? string.Empty,
                workspace.Description ?? string.Empty,
                string.Join(
                    ", ",
                    workspace.Memberships
                        .OrderBy(
                            membership => membership.Order)
                        .Select(
                            membership => documentNames.TryGetValue(
                                membership.DocumentId,
                                out string? fileName)
                                    ? fileName
                                    : string.Empty)
                        .Where(
                            fileName => !string.IsNullOrWhiteSpace(fileName))),
                workspace.LastUpdated))
            .ToList();

        _view.ShowWorkspaces(items);
        _view.SetWorkspacesCount(items.Count);

        _logger.LogDebug(
            LogMessages.WorkspaceListRefreshCompleted,
            items.Count);
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
        DocumentRemovedEventArgs e)
    {
        _logger.LogDebug(
            LogMessages.DocumentWorkspaceRemovalNotificationReceived);

        await RefreshDocumentsAsync();
    }
}
