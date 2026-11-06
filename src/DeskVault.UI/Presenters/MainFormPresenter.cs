using DeskVault.Application.Documents.Commands.ImportDocument;
using DeskVault.Application.Documents.Commands.RemoveDocument;
using DeskVault.Application.Documents.Extraction;
using DeskVault.Application.Documents.Queries.ListDocuments;
using DeskVault.Application.Documents.Queries.SearchDocuments;
using DeskVault.Application.Interfaces;
using DeskVault.Application.Security.Sessions;
using DeskVault.Application.Workspaces.Commands.CloseWorkspace;
using DeskVault.Application.Workspaces.Commands.CreateWorkspace;
using DeskVault.Application.Workspaces.Commands.DeleteWorkspace;
using DeskVault.Application.Workspaces.Commands.OpenWorkspace;
using DeskVault.Application.Workspaces.Queries.GetWorkspaces;
using DeskVault.Shared.Extensions;
using DeskVault.UI.Resources;
using DeskVault.UI.Services.Interfaces;
using DeskVault.UI.Services.Workspace;
using DeskVault.UI.Views;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DiagnosticOperationCodes = DeskVault.Shared.Resources.DiagnosticOperationCodes;

namespace DeskVault.UI.Presenters;

public sealed class MainFormPresenter : IDisposable
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
    private readonly IApplicationSecuritySession _applicationSecuritySession;
    private long _lastAppliedSecuritySessionRevision;
    private bool _isProtectedAccessAvailable;
    private long _latestObservedRevocationRevision;
    private bool _disposed;

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
        IOptions<SearchOptions> searchOptions,
        IApplicationSecuritySession applicationSecuritySession)
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
        ArgumentNullException.ThrowIfNull(applicationSecuritySession);

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

        _applicationSecuritySession = applicationSecuritySession;

        ApplicationSecuritySessionSnapshot initialSnapshot =
            _applicationSecuritySession.CurrentSnapshot;

        _lastAppliedSecuritySessionRevision =
            initialSnapshot.Revision;

        _isProtectedAccessAvailable =
            initialSnapshot.IsProtectedAccessAvailable;

        _applicationSecuritySession.StateChanged +=
            OnApplicationSecuritySessionStateChanged;

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

    private void OnApplicationSecuritySessionStateChanged(
        ApplicationSecuritySessionSnapshot notification)
    {
        if (!notification.IsProtectedAccessAvailable)
        {
            long observedRevision =
                Volatile.Read(ref _latestObservedRevocationRevision);

            while (notification.Revision > observedRevision)
            {
                long previousRevision =
                    Interlocked.CompareExchange(
                        ref _latestObservedRevocationRevision,
                        notification.Revision,
                        observedRevision);

                if (previousRevision == observedRevision)
                {
                    break;
                }

                observedRevision = previousRevision;
            }

            InvalidateSearchOperationImmediately();
        }

        _view.PostToUi(
            () => ApplyLatestApplicationSecuritySessionSnapshot(notification));
    }

    private void ApplyLatestApplicationSecuritySessionSnapshot(
        ApplicationSecuritySessionSnapshot notification)
    {
        if (_disposed)
        {
            return;
        }

        ApplicationSecuritySessionSnapshot currentSnapshot =
            _applicationSecuritySession.CurrentSnapshot;

        if (currentSnapshot.Revision < notification.Revision)
        {
            return;
        }

        long latestObservedRevocationRevision =
            Volatile.Read(ref _latestObservedRevocationRevision);

        bool hasUnappliedRevocation =
            latestObservedRevocationRevision >
            _lastAppliedSecuritySessionRevision;

        bool hasUnappliedNotification =
            notification.Revision >
            _lastAppliedSecuritySessionRevision;

        bool hasNewerSnapshot =
            currentSnapshot.Revision >
            _lastAppliedSecuritySessionRevision;

        if (!hasUnappliedRevocation &&
            !hasUnappliedNotification &&
            !hasNewerSnapshot &&
            currentSnapshot.IsProtectedAccessAvailable ==
                _isProtectedAccessAvailable)
        {
            return;
        }

        bool wasProtectedAccessAvailable =
            _isProtectedAccessAvailable;

        bool mustClearPresentation =
            hasUnappliedRevocation ||
            (hasUnappliedNotification &&
             !notification.IsProtectedAccessAvailable) ||
            !currentSnapshot.IsProtectedAccessAvailable;

        _lastAppliedSecuritySessionRevision =
            Math.Max(
                currentSnapshot.Revision,
                Math.Max(
                    notification.Revision,
                    latestObservedRevocationRevision));

        _isProtectedAccessAvailable =
            currentSnapshot.IsProtectedAccessAvailable;

        if (mustClearPresentation)
        {
            ClearProtectedPresentation();

            if (_isProtectedAccessAvailable)
            {
                _ = InitializeAsync();
            }

            return;
        }

        if (!wasProtectedAccessAvailable &&
            _isProtectedAccessAvailable)
        {
            _ = InitializeAsync();
        }
    }

    private bool TryBeginAuthorizedInitialization()
    {
        if (_disposed)
        {
            return false;
        }

        ApplicationSecuritySessionSnapshot currentSnapshot =
            _applicationSecuritySession.CurrentSnapshot;

        long latestObservedRevocationRevision =
            Volatile.Read(ref _latestObservedRevocationRevision);

        bool hasUnappliedRevocation =
            latestObservedRevocationRevision >
            _lastAppliedSecuritySessionRevision;

        _lastAppliedSecuritySessionRevision =
            Math.Max(
                _lastAppliedSecuritySessionRevision,
                Math.Max(
                    currentSnapshot.Revision,
                    latestObservedRevocationRevision));

        _isProtectedAccessAvailable =
            currentSnapshot.IsProtectedAccessAvailable;

        if (!_isProtectedAccessAvailable ||
            hasUnappliedRevocation)
        {
            ClearProtectedPresentation();

            if (!_isProtectedAccessAvailable)
            {
                return false;
            }
        }

        _view.ShowAuthenticatedState();

        return true;
    }

    private bool EnsureProtectedAccessAvailable()
    {
        if (_disposed)
        {
            return false;
        }

        ApplicationSecuritySessionSnapshot currentSnapshot =
            _applicationSecuritySession.CurrentSnapshot;

        long latestObservedRevocationRevision =
            Volatile.Read(ref _latestObservedRevocationRevision);

        bool hasUnappliedRevocation =
            latestObservedRevocationRevision >
            _lastAppliedSecuritySessionRevision;

        bool wasProtectedAccessAvailable =
            _isProtectedAccessAvailable;

        if (!currentSnapshot.IsProtectedAccessAvailable ||
            hasUnappliedRevocation)
        {
            _lastAppliedSecuritySessionRevision =
                Math.Max(
                    _lastAppliedSecuritySessionRevision,
                    Math.Max(
                        currentSnapshot.Revision,
                        latestObservedRevocationRevision));

            _isProtectedAccessAvailable =
                currentSnapshot.IsProtectedAccessAvailable;

            ClearProtectedPresentation();

            if (!_isProtectedAccessAvailable)
            {
                return false;
            }

            _view.ShowAuthenticatedState();

            // The session is authenticated again, but the earlier presentation
            // was invalidated. Start a fresh load; never restore old content.
            _ = InitializeAsync();

            return true;
        }

        if (currentSnapshot.Revision >
            _lastAppliedSecuritySessionRevision)
        {
            _lastAppliedSecuritySessionRevision =
                currentSnapshot.Revision;

            _isProtectedAccessAvailable = true;

            if (!wasProtectedAccessAvailable)
            {
                _view.ShowAuthenticatedState();
                _ = InitializeAsync();
            }
        }

        return true;
    }

    private bool IsAuthorizedSessionRevisionCurrent(
        long expectedRevision)
    {
        if (_disposed)
        {
            return false;
        }

        ApplicationSecuritySessionSnapshot currentSnapshot =
            _applicationSecuritySession.CurrentSnapshot;

        long latestObservedRevocationRevision =
            Volatile.Read(ref _latestObservedRevocationRevision);

        long lastAppliedRevision =
            Volatile.Read(ref _lastAppliedSecuritySessionRevision);

        return currentSnapshot.IsProtectedAccessAvailable &&
               currentSnapshot.Revision == expectedRevision &&
               lastAppliedRevision == expectedRevision &&
               latestObservedRevocationRevision <= lastAppliedRevision;
    }

    private void InvalidateSearchState()
    {
        _currentSearchText = null;
        _currentSearchContinuation = null;

        Interlocked.Increment(
            ref _searchOperationVersion);

        CancelSearchOperation(
            Volatile.Read(ref _searchCancellationTokenSource));
    }

    private void InvalidateSearchOperationImmediately()
    {
        Interlocked.Increment(
            ref _searchOperationVersion);

        CancelSearchOperation(
            Volatile.Read(ref _searchCancellationTokenSource));
    }

    private void CancelSearchOperation(
        CancellationTokenSource? cancellationTokenSource)
    {
        if (cancellationTokenSource is null)
        {
            return;
        }

        try
        {
            cancellationTokenSource.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The search completed and disposed its source concurrently.
        }
        catch (AggregateException exception)
        {
            // Cancellation is already requested. A failing callback must not
            // prevent security-session invalidation or protected-view cleanup.
            _logger.LogSafeFailure(
                LogLevel.Error,
                DiagnosticOperationCodes.DocumentSearchCancellationCallbackFailure,
                LogMessages.DocumentSearchCancellationCallbackFailed,
                exception);
        }
    }

    private void ClearProtectedPresentation()
    {
        InvalidateSearchState();

        // Lock the main application surface before disposing child workspace views.
        _view.ShowLockedState();

        // Attempt workspace cleanup without allowing disposal failures to
        // interrupt security-session processing.
        try
        {
            _workspacePresentationManager.CloseAll();
        }
        catch (Exception exception)
        {
            _logger.LogSafeFailure(
                LogLevel.Error,
                DiagnosticOperationCodes.WorkspacePresentationCleanupFailure,
                LogMessages.WorkspacePresentationCleanupFailed,
                exception);
        }
    }

    public async Task InitializeAsync()
    {
        if (!TryBeginAuthorizedInitialization())
        {
            return;
        }

        // Capture the revision after the authorization gate has been applied.
        ApplicationSecuritySessionSnapshot initializationSnapshot =
            _applicationSecuritySession.CurrentSnapshot;

        long initializationRevision =
            initializationSnapshot.Revision;

        if (!IsAuthorizedSessionRevisionCurrent(initializationRevision))
        {
            return;
        }

        _logger.LogInformation(
            LogMessages.MainWorkspaceInitializationStarted);

        try
        {
            var documentCount =
                await RefreshDocumentsAsync(
                    initializationRevision);

            if (!IsAuthorizedSessionRevisionCurrent(initializationRevision))
            {
                return;
            }

            await RefreshWorkspacesAsync(initializationRevision);

            if (!IsAuthorizedSessionRevisionCurrent(initializationRevision))
            {
                return;
            }

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

            if (!IsAuthorizedSessionRevisionCurrent(initializationRevision))
            {
                return;
            }

            _view.SetStatus(
                $"{documentCount} document(s) imported.");

            _logger.LogInformation(
                LogMessages.MainWorkspaceInitializedWithDocuments,
                documentCount);
        }
        catch (Exception ex)
        {
            // Do not expose stale errors or status updates after revocation.
            if (!IsAuthorizedSessionRevisionCurrent(initializationRevision))
            {
                return;
            }

            _logger.LogSafeFailure(
                LogLevel.Error,
                DiagnosticOperationCodes.MainWorkspaceInitializationFailure,
                LogMessages.MainWorkspaceInitializationFailed,
                ex);

            _view.SetStatus(
                UiMessages.UnableToLoadDocumentsStatus);

            _view.ShowError(
                UiMessages.UnableToLoadDocuments,
                UiMessages.DeskVaultTitle,
                initializationRevision);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _applicationSecuritySession.StateChanged -=
            OnApplicationSecuritySessionStateChanged;

        _view.ImportRequested -= OnImportRequested;
        _view.OpenRequested -= OnOpenRequested;
        _view.RemoveRequested -= OnRemoveRequested;
        _view.DocumentSelectionChanged -= OnDocumentSelectionChanged;
        _view.SearchRequested -= OnSearchRequested;
        _view.LoadMoreSearchResultsRequested -= OnLoadMoreSearchResultsRequested;
        _view.ReprocessRequested -= OnReprocessRequested;
        _view.WorkspaceSelectionChanged -= OnWorkspaceSelectionChanged;
        _view.WorkspaceCreateRequested -= OnWorkspaceCreateRequested;
        _view.WorkspaceOpenRequested -= OnWorkspaceOpenRequested;
        _view.WorkspaceRemoveRequested -= OnWorkspaceRemoveRequested;

        InvalidateSearchState();
        _workspacePresentationManager.CloseAll();
    }


    private async void OnImportRequested(
        object? sender,
        EventArgs e)
    {
        if (!EnsureProtectedAccessAvailable())
        {
            return;
        }

        ApplicationSecuritySessionSnapshot importSnapshot =
            _applicationSecuritySession.CurrentSnapshot;

        long importSessionRevision =
            importSnapshot.Revision;

        if (!IsAuthorizedSessionRevisionCurrent(importSessionRevision))
        {
            return;
        }

        string? filePath = _view.SelectedFilePath;

        // The native file picker may return after protected access has been withdrawn.
        // Discard its selection rather than starting an import from the earlier session.
        if (!IsAuthorizedSessionRevisionCurrent(importSessionRevision))
        {
            return;
        }

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

            // Do not continue an import from a revoked or superseded session.
            if (!IsAuthorizedSessionRevisionCurrent(importSessionRevision))
            {
                return;
            }

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
                    if (!IsAuthorizedSessionRevisionCurrent(importSessionRevision))
                    {
                        return;
                    }

                    await _documentProcessingService.ProcessAsync(
                        documentId);

                    if (!IsAuthorizedSessionRevisionCurrent(importSessionRevision))
                    {
                        return;
                    }
                }
                else
                {
                    _logger.LogDebug(
                        LogMessages.DocumentProcessingSkippedWithoutTextExtractor,
                        Path.GetExtension(fileName));
                }

                await RefreshDocumentsAsync(importSessionRevision);

                if (!IsAuthorizedSessionRevisionCurrent(importSessionRevision))
                {
                    return;
                }

                _view.SetSelectedDocumentId(
                    result.DocumentId);

                if (!IsAuthorizedSessionRevisionCurrent(importSessionRevision))
                {
                    return;
                }

                _view.SetOpenEnabled(
                    result.DocumentId.HasValue);

                if (!IsAuthorizedSessionRevisionCurrent(importSessionRevision))
                {
                    return;
                }

                UpdateReprocessEnabled();

                if (!IsAuthorizedSessionRevisionCurrent(importSessionRevision))
                {
                    return;
                }

                _view.SetStatus(
                    result.Description);

                if (!IsAuthorizedSessionRevisionCurrent(importSessionRevision))
                {
                    return;
                }

                _logger.LogInformation(
                    LogMessages.DocumentImportCompleted);

                _view.ShowInformation(
                    result.Description,
                    UiMessages.ImportCompleteTitle,
                    importSessionRevision);

                return;
            }

            _logger.LogWarning(
                LogMessages.DocumentImportRejected);

            if (!IsAuthorizedSessionRevisionCurrent(importSessionRevision))
            {
                return;
            }

            _view.SetStatus(
                result.Description);

            if (!IsAuthorizedSessionRevisionCurrent(importSessionRevision))
            {
                return;
            }

            _view.ShowWarning(
                result.Description,
                UiMessages.ImportFailedTitle,
                importSessionRevision);
        }
        catch (Exception ex)
        {
            _logger.LogSafeFailure(
                LogLevel.Error,
                DiagnosticOperationCodes.DocumentImportUnexpectedFailure,
                LogMessages.DocumentImportFailed,
                ex);

            if (!IsAuthorizedSessionRevisionCurrent(importSessionRevision))
            {
                return;
            }

            _view.SetStatus(
                UiMessages.UnexpectedImportError);

            if (!IsAuthorizedSessionRevisionCurrent(importSessionRevision))
            {
                return;
            }

            _view.ShowError(
                UiMessages.UnexpectedImportError,
                UiMessages.DeskVaultTitle,
                importSessionRevision);
        }
        finally
        {
            // An old operation must not re-enable controls in a locked
            // or newly authenticated session.
            if (IsAuthorizedSessionRevisionCurrent(importSessionRevision))
            {
                _view.SetImportEnabled(true);
            }
        }
    }


    private async void OnOpenRequested(
        object? sender,
        EventArgs e)
    {
        if (!EnsureProtectedAccessAvailable())
        {
            return;
        }

        ApplicationSecuritySessionSnapshot openSnapshot =
            _applicationSecuritySession.CurrentSnapshot;

        long openSessionRevision =
            openSnapshot.Revision;

        if (!IsAuthorizedSessionRevisionCurrent(openSessionRevision))
        {
            return;
        }

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

            if (!IsAuthorizedSessionRevisionCurrent(openSessionRevision))
            {
                return;
            }

            if (existingTemporaryPresentation is not null)
            {
                existingTemporaryPresentation.Activate();

                if (!IsAuthorizedSessionRevisionCurrent(openSessionRevision))
                {
                    return;
                }

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

            // Workspace creation may complete after session revocation.
            if (!IsAuthorizedSessionRevisionCurrent(openSessionRevision))
            {
                return;
            }

            if (workspaceResult.Status !=
                CreateWorkspaceResultStatus.Success ||
                workspaceResult.Workspace is null)
            {
                _logger.LogWarning(
                    LogMessages.TemporaryWorkspaceCreationFailed);

                if (!IsAuthorizedSessionRevisionCurrent(openSessionRevision))
                {
                    return;
                }

                _view.SetStatus(
                    workspaceResult.Description);

                if (!IsAuthorizedSessionRevisionCurrent(openSessionRevision))
                {
                    return;
                }

                _view.ShowError(
                    workspaceResult.Description,
                    UiMessages.OpenDocumentTitle,
                    openSessionRevision);

                return;
            }

            var presentation =
                _workspacePresentationManager.GetOrCreate(
                    workspaceResult.Workspace.Id,
                    workspaceResult.Workspace.TypeOfWorkspace,
                    workspaceResult.Workspace.Name,
                    workspaceResult.Workspace.Description,
                    openSessionRevision);

            if (!IsAuthorizedSessionRevisionCurrent(openSessionRevision))
            {
                return;
            }

            SubscribeWorkspacePresentation(
                presentation);

            if (!IsAuthorizedSessionRevisionCurrent(openSessionRevision))
            {
                return;
            }

            await presentation.SetWorkspaceDocumentsAsync(
                workspaceResult.Workspace.Memberships.Select(
                    membership => membership.DocumentId));

            if (!IsAuthorizedSessionRevisionCurrent(openSessionRevision))
            {
                return;
            }

            await presentation.OpenDocumentAsync(
                documentId);

            if (!IsAuthorizedSessionRevisionCurrent(openSessionRevision))
            {
                return;
            }

            _view.SetStatus(
                UiMessages.DocumentOpenedStatus);

            _logger.LogInformation(
                LogMessages.DocumentOpenCompleted);
        }
        catch (Exception ex)
        {
            _logger.LogSafeFailure(
                LogLevel.Error,
                DiagnosticOperationCodes.DocumentOpenFailure,
                LogMessages.DocumentOpenFailed,
                ex);

            if (!IsAuthorizedSessionRevisionCurrent(openSessionRevision))
            {
                return;
            }

            _view.SetStatus(
                UiMessages.UnableToOpenDocumentStatus);

            if (!IsAuthorizedSessionRevisionCurrent(openSessionRevision))
            {
                return;
            }

            _view.ShowError(
                UiMessages.UnableToOpenDocument,
                UiMessages.OpenDocumentTitle,
                openSessionRevision);
        }
        finally
        {
            // Do not restore controls for a revoked or superseded session.
            if (IsAuthorizedSessionRevisionCurrent(openSessionRevision))
            {
                _view.SetOpenEnabled(
                    _view.SelectedDocumentId.HasValue);

                UpdateReprocessEnabled();
            }
        }
    }

    private void OnWorkspaceSelectionChanged(
        object? sender,
        EventArgs e)
    {
        if (!EnsureProtectedAccessAvailable())
        {
            return;
        }

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
        if (!EnsureProtectedAccessAvailable())
        {
            return;
        }

        ApplicationSecuritySessionSnapshot createSnapshot =
            _applicationSecuritySession.CurrentSnapshot;

        long createSessionRevision =
            createSnapshot.Revision;

        if (!IsAuthorizedSessionRevisionCurrent(createSessionRevision))
        {
            return;
        }

        _logger.LogInformation(
            LogMessages.WorkspaceCreationRequested);

        WorkspaceCreateRequest? request =
            _view.ShowCreateWorkspaceDialog();

        if (!IsAuthorizedSessionRevisionCurrent(createSessionRevision))
        {
            return;
        }

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

            if (!IsAuthorizedSessionRevisionCurrent(createSessionRevision))
            {
                return;
            }

            if (result.Status !=
                CreateWorkspaceResultStatus.Success ||
                result.Workspace is null)
            {
                _logger.LogWarning(
                    LogMessages.WorkspaceCreationFailed,
                    result.Description);

                if (!IsAuthorizedSessionRevisionCurrent(createSessionRevision))
                {
                    return;
                }

                _view.SetStatus(
                    result.Description);

                if (!IsAuthorizedSessionRevisionCurrent(createSessionRevision))
                {
                    return;
                }

                _view.ShowWarning(
                    result.Description,
                    UiMessages.CreateWorkspaceTitle,
                    createSessionRevision);

                return;
            }

            await RefreshWorkspacesAsync(createSessionRevision);

            if (!IsAuthorizedSessionRevisionCurrent(createSessionRevision))
            {
                return;
            }

            _view.SetSelectedWorkspaceId(
                result.Workspace.Id);

            if (!IsAuthorizedSessionRevisionCurrent(createSessionRevision))
            {
                return;
            }

            await OpenWorkspaceAsync(
                result.Workspace.Id,
                createSessionRevision);

            if (!IsAuthorizedSessionRevisionCurrent(createSessionRevision))
            {
                return;
            }

            _logger.LogInformation(
                LogMessages.WorkspaceCreatedAndOpened,
                result.Workspace.Id);
        }
        catch (Exception ex)
        {
            _logger.LogSafeFailure(
                LogLevel.Error,
                DiagnosticOperationCodes.WorkspaceCreationFailure,
                LogMessages.WorkspaceCreationUnexpectedFailure,
                ex);

            if (!IsAuthorizedSessionRevisionCurrent(createSessionRevision))
            {
                return;
            }

            _view.SetStatus(
                UiMessages.UnableToCreateWorkspace);

            if (!IsAuthorizedSessionRevisionCurrent(createSessionRevision))
            {
                return;
            }

            _view.ShowError(
                UiMessages.UnableToCreateWorkspace,
                UiMessages.CreateWorkspaceTitle,
                createSessionRevision);
        }
    }

    private async void OnWorkspaceOpenRequested(
        object? sender,
        EventArgs e)
    {
        if (!EnsureProtectedAccessAvailable())
        {
            return;
        }

        ApplicationSecuritySessionSnapshot openSnapshot =
            _applicationSecuritySession.CurrentSnapshot;

        long openSessionRevision =
            openSnapshot.Revision;

        if (!IsAuthorizedSessionRevisionCurrent(openSessionRevision))
        {
            return;
        }

        if (_view.SelectedWorkspaceId is not Guid workspaceId)
        {
            _logger.LogDebug(
                LogMessages.WorkspaceOpenSkippedWithoutSelection);

            return;
        }

        await OpenWorkspaceAsync(
            workspaceId,
            openSessionRevision);
    }

    private async void OnWorkspaceRemoveRequested(
        object? sender,
        EventArgs e)
    {
        if (!EnsureProtectedAccessAvailable())
        {
            return;
        }

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
        if (!EnsureProtectedAccessAvailable())
        {
            return;
        }

        if (sender is not WorkspacePresentation presentation ||
            !IsAuthorizedSessionRevisionCurrent(
                presentation.SecuritySessionRevision))
        {
            return;
        }

        await RefreshWorkspacesAsync(
            presentation.SecuritySessionRevision);
    }

    private async void OnWorkspaceDetailsUpdated(
        object? sender,
        EventArgs e)
    {
        if (!EnsureProtectedAccessAvailable())
        {
            return;
        }

        if (sender is not WorkspacePresentation presentation ||
            !IsAuthorizedSessionRevisionCurrent(
                presentation.SecuritySessionRevision))
        {
            return;
        }

        await RefreshWorkspacesAsync(
            presentation.SecuritySessionRevision);
    }

    private async void OnWorkspaceDeleteRequested(
        object? sender,
        EventArgs e)
    {
        if (!EnsureProtectedAccessAvailable())
        {
            return;
        }

        if (sender is not WorkspacePresentation presentation)
        {
            _logger.LogWarning(
                LogMessages.WorkspaceDeletionUnexpectedSender);

            return;
        }

        long expectedSessionRevision =
            presentation.SecuritySessionRevision;

        if (!IsAuthorizedSessionRevisionCurrent(
                expectedSessionRevision))
        {
            return;
        }

        await DeleteWorkspaceAsync(
            presentation.WorkspaceId,
            presentation.WorkspaceName ??
                UiMessages.CurrentWorkspaceFallbackName,
            clearMainWorkspaceSelection:
                _view.SelectedWorkspaceId ==
                presentation.WorkspaceId,
            expectedSecuritySessionRevision:
                expectedSessionRevision);
    }

    private async void OnWorkspaceCloseRequested(
        object? sender,
        EventArgs e)
    {
        if (!EnsureProtectedAccessAvailable())
        {
            return;
        }

        if (sender is not WorkspacePresentation presentation)
        {
            _logger.LogWarning(
                LogMessages.WorkspaceCloseUnexpectedSender);

            return;
        }

        long closeSessionRevision =
            presentation.SecuritySessionRevision;

        if (!IsAuthorizedSessionRevisionCurrent(
                closeSessionRevision))
        {
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

            if (!IsAuthorizedSessionRevisionCurrent(closeSessionRevision))
            {
                return;
            }

            if (result.Status !=
                CloseWorkspaceResultStatus.Success)
            {
                _logger.LogWarning(
                    LogMessages.WorkspaceCloseRejected,
                    workspaceId,
                    result.Description);

                _view.SetStatus(
                    result.Description);

                if (!IsAuthorizedSessionRevisionCurrent(closeSessionRevision))
                {
                    return;
                }

                _view.ShowWarning(
                    result.Description,
                    UiMessages.CloseWorkspace,
                    closeSessionRevision);

                return;
            }

            _workspacePresentationManager.Close(
                workspaceId);

            if (!IsAuthorizedSessionRevisionCurrent(closeSessionRevision))
            {
                return;
            }

            _view.SetStatus(
                result.Description);

            _logger.LogInformation(
                LogMessages.WorkspaceClosed,
                workspaceId);
        }
        catch (Exception ex)
        {
            if (!IsAuthorizedSessionRevisionCurrent(closeSessionRevision))
            {
                return;
            }

            _logger.LogSafeFailure(
                LogLevel.Error,
                DiagnosticOperationCodes.WorkspaceCloseFailure,
                LogMessages.WorkspaceCloseUnexpectedFailure,
                ex);

            _view.SetStatus(
                UiMessages.UnableToCloseWorkspace);

            if (!IsAuthorizedSessionRevisionCurrent(closeSessionRevision))
            {
                return;
            }

            _view.ShowError(
                UiMessages.UnableToCloseWorkspace,
                UiMessages.CloseWorkspace,
                closeSessionRevision);
        }
    }

    private async Task DeleteWorkspaceAsync(
        Guid workspaceId,
        string workspaceName,
        bool clearMainWorkspaceSelection,
        long? expectedSecuritySessionRevision = null)
    {
        if (!EnsureProtectedAccessAvailable())
        {
            return;
        }

        ApplicationSecuritySessionSnapshot operationSnapshot =
            _applicationSecuritySession.CurrentSnapshot;

        long operationRevision =
            expectedSecuritySessionRevision ??
            operationSnapshot.Revision;

        if (!IsAuthorizedSessionRevisionCurrent(operationRevision))
        {
            return;
        }

        if (!_view.ConfirmWorkspaceRemoval(
            workspaceName,
            operationRevision))
        {
            _logger.LogInformation(
                LogMessages.WorkspaceRemovalCancelled,
                workspaceId);

            return;
        }

        // The session may have changed while confirmation was open.
        if (!IsAuthorizedSessionRevisionCurrent(operationRevision))
        {
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

            if (!IsAuthorizedSessionRevisionCurrent(operationRevision))
            {
                return;
            }

            if (openResult.Status !=
                    OpenWorkspaceResultStatus.Activated &&
                openResult.Status !=
                    OpenWorkspaceResultStatus.AlreadyActive)
            {
                _logger.LogWarning(
                    LogMessages.WorkspaceRemovalActivationFailed,
                    workspaceId,
                    openResult.Description);

                if (!IsAuthorizedSessionRevisionCurrent(operationRevision))
                {
                    return;
                }

                _view.SetStatus(
                    openResult.Description);

                if (!IsAuthorizedSessionRevisionCurrent(operationRevision))
                {
                    return;
                }

                _view.ShowWarning(
                    openResult.Description,
                    UiMessages.DeleteWorkspace,
                    operationRevision);

                return;
            }

            // Revalidate immediately before starting the delete operation.
            if (!IsAuthorizedSessionRevisionCurrent(operationRevision))
            {
                return;
            }

            var result =
                await _deleteWorkspaceHandler.HandleAsync(
                    new DeleteWorkspaceCommand(workspaceId));

            if (!IsAuthorizedSessionRevisionCurrent(operationRevision))
            {
                return;
            }

            if (result.Status !=
                DeleteWorkspaceResultStatus.Success)
            {
                _logger.LogWarning(
                    LogMessages.WorkspaceRemovalRejected,
                    workspaceId,
                    result.Description);

                if (!IsAuthorizedSessionRevisionCurrent(operationRevision))
                {
                    return;
                }

                _view.SetStatus(
                    result.Description);

                if (!IsAuthorizedSessionRevisionCurrent(operationRevision))
                {
                    return;
                }

                _view.ShowWarning(
                    result.Description,
                    UiMessages.DeleteWorkspace,
                    operationRevision);

                return;
            }

            // Domain deletion succeeded. Do not publish stale presentation state.
            if (!IsAuthorizedSessionRevisionCurrent(operationRevision))
            {
                return;
            }

            _workspacePresentationManager.Close(
                workspaceId);

            if (!IsAuthorizedSessionRevisionCurrent(operationRevision))
            {
                return;
            }

            await RefreshWorkspacesAsync(operationRevision);

            if (!IsAuthorizedSessionRevisionCurrent(operationRevision))
            {
                return;
            }

            if (clearMainWorkspaceSelection)
            {
                _view.SetSelectedWorkspaceId(null);
            }

            if (!IsAuthorizedSessionRevisionCurrent(operationRevision))
            {
                return;
            }

            _view.SetStatus(
                result.Description);

            _logger.LogInformation(
                LogMessages.WorkspaceRemoved,
                workspaceId);
        }
        catch (Exception ex)
        {
            if (!IsAuthorizedSessionRevisionCurrent(operationRevision))
            {
                return;
            }

            _logger.LogSafeFailure(
                LogLevel.Error,
                DiagnosticOperationCodes.WorkspaceRemovalFailure,
                LogMessages.WorkspaceRemovalUnexpectedFailure,
                ex);

            if (!IsAuthorizedSessionRevisionCurrent(operationRevision))
            {
                return;
            }

            _view.SetStatus(
                UiMessages.UnableToRemoveWorkspace);

            if (!IsAuthorizedSessionRevisionCurrent(operationRevision))
            {
                return;
            }

            _view.ShowError(
                UiMessages.UnableToRemoveWorkspace,
                UiMessages.DeleteWorkspace,
                operationRevision);
        }
    }

    private async Task OpenWorkspaceAsync(
        Guid workspaceId,
        long expectedSessionRevision)
    {
        if (!IsAuthorizedSessionRevisionCurrent(expectedSessionRevision))
        {
            return;
        }

        _logger.LogInformation(
            LogMessages.WorkspaceOpenStarted,
            workspaceId);

        try
        {
            var result =
                await _openWorkspaceHandler.HandleAsync(
                    new OpenWorkspaceCommand(workspaceId));

            // The handler may finish after the initiating session was revoked.
            if (!IsAuthorizedSessionRevisionCurrent(expectedSessionRevision))
            {
                return;
            }

            if (result.Status != OpenWorkspaceResultStatus.Activated &&
                result.Status != OpenWorkspaceResultStatus.AlreadyActive)
            {
                _logger.LogWarning(
                    LogMessages.WorkspaceOpenRejected,
                    workspaceId,
                    result.Description);

                if (!IsAuthorizedSessionRevisionCurrent(expectedSessionRevision))
                {
                    return;
                }

                _view.SetStatus(
                    result.Description);

                if (!IsAuthorizedSessionRevisionCurrent(expectedSessionRevision))
                {
                    return;
                }

                _view.ShowWarning(
                    result.Description,
                    UiMessages.OpenWorkspaceTitle,
                    expectedSessionRevision);

                return;
            }

            if (result.MissingDocumentIds.Count > 0)
            {
                _logger.LogWarning(
                    LogMessages.WorkspaceOpenedWithMissingDocuments,
                    workspaceId,
                    result.MissingDocumentIds.Count);

                if (!IsAuthorizedSessionRevisionCurrent(expectedSessionRevision))
                {
                    return;
                }

                _view.ShowWarning(
                    result.Description,
                    UiMessages.OpenWorkspaceTitle,
                    expectedSessionRevision);
            }

            if (!IsAuthorizedSessionRevisionCurrent(expectedSessionRevision))
            {
                return;
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
                    result.Workspace.Description,
                    expectedSessionRevision);

            if (!IsAuthorizedSessionRevisionCurrent(expectedSessionRevision))
            {
                return;
            }

            SubscribeWorkspacePresentation(
                presentation);

            if (!IsAuthorizedSessionRevisionCurrent(expectedSessionRevision))
            {
                return;
            }

            await presentation.SetWorkspaceDocumentsAsync(
                result.Workspace.Memberships.Select(
                    membership => membership.DocumentId));

            if (!IsAuthorizedSessionRevisionCurrent(expectedSessionRevision))
            {
                return;
            }

            foreach (var membership in
                     result.Workspace.Memberships.OrderBy(
                         membership => membership.Order))
            {
                if (!IsAuthorizedSessionRevisionCurrent(expectedSessionRevision))
                {
                    return;
                }

                bool documentAlreadyPresented =
                    presentation.ContainsDocument(
                        membership.DocumentId);

                if (!IsAuthorizedSessionRevisionCurrent(expectedSessionRevision))
                {
                    return;
                }

                if (!documentAlreadyPresented)
                {
                    await presentation.OpenDocumentAsync(
                        membership.DocumentId);

                    if (!IsAuthorizedSessionRevisionCurrent(expectedSessionRevision))
                    {
                        return;
                    }
                }
            }

            if (result.Workspace.LastActiveDocumentId is Guid lastActiveDocumentId)
            {
                if (!IsAuthorizedSessionRevisionCurrent(expectedSessionRevision))
                {
                    return;
                }

                presentation.ActivateDocument(
                    lastActiveDocumentId);
            }

            if (!IsAuthorizedSessionRevisionCurrent(expectedSessionRevision))
            {
                return;
            }

            _view.SetStatus(
                result.Description);

            _logger.LogInformation(
                LogMessages.WorkspaceOpened,
                workspaceId);
        }
        catch (Exception ex)
        {
            _logger.LogSafeFailure(
                LogLevel.Error,
                DiagnosticOperationCodes.WorkspaceOpenFailure,
                LogMessages.WorkspaceOpenUnexpectedFailure,
                ex);

            if (!IsAuthorizedSessionRevisionCurrent(expectedSessionRevision))
            {
                return;
            }

            _view.SetStatus(
                UiMessages.UnableToOpenWorkspace);

            if (!IsAuthorizedSessionRevisionCurrent(expectedSessionRevision))
            {
                return;
            }

            _view.ShowError(
                UiMessages.UnableToOpenWorkspace,
                UiMessages.OpenWorkspaceTitle,
                expectedSessionRevision);
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
        if (!EnsureProtectedAccessAvailable())
        {
            return;
        }

        ApplicationSecuritySessionSnapshot operationSnapshot =
            _applicationSecuritySession.CurrentSnapshot;

        long operationRevision =
            operationSnapshot.Revision;

        if (!IsAuthorizedSessionRevisionCurrent(operationRevision))
        {
            return;
        }

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

        bool confirmed =
            _view.ConfirmRemoval(
                fileName,
                operationRevision);

        // Confirmation is modal; the session may have locked while it was open.
        if (!IsAuthorizedSessionRevisionCurrent(operationRevision))
        {
            return;
        }

        if (!confirmed)
        {
            _logger.LogDebug(
                LogMessages.DocumentRemovalCancelled);

            return;
        }

        _logger.LogInformation(
            LogMessages.DocumentRemovalStarted);

        if (!IsAuthorizedSessionRevisionCurrent(operationRevision))
        {
            return;
        }

        _view.SetRemoveEnabled(false);
        _view.SetOpenEnabled(false);
        _view.SetImportEnabled(false);
        _view.SetReprocessEnabled(false);
        _view.SetStatus(UiMessages.RemovingDocumentStatus);

        try
        {
            var result =
                await _removeDocumentHandler.HandleAsync(
                    new RemoveDocumentCommand(documentId));

            if (!IsAuthorizedSessionRevisionCurrent(operationRevision))
            {
                return;
            }

            if (result.Status ==
                RemoveDocumentResultStatus.Success)
            {
                await RefreshDocumentsAsync(operationRevision);

                if (!IsAuthorizedSessionRevisionCurrent(operationRevision))
                {
                    return;
                }

                await RefreshWorkspacesAsync(operationRevision);

                if (!IsAuthorizedSessionRevisionCurrent(operationRevision))
                {
                    return;
                }

                _view.SetStatus(result.Message);

                if (!IsAuthorizedSessionRevisionCurrent(operationRevision))
                {
                    return;
                }

                _logger.LogInformation(
                    LogMessages.DocumentRemovalCompleted);

                _view.ShowInformation(
                    result.Message,
                    UiMessages.DocumentRemovedTitle,
                    operationRevision);

                return;
            }

            _logger.LogWarning(
                LogMessages.DocumentRemovalRejected);

            if (!IsAuthorizedSessionRevisionCurrent(operationRevision))
            {
                return;
            }

            _view.SetStatus(result.Message);

            if (!IsAuthorizedSessionRevisionCurrent(operationRevision))
            {
                return;
            }

            _view.ShowWarning(
                result.Message,
                UiMessages.RemoveFailedTitle,
                operationRevision);
        }
        catch (Exception ex)
        {
            if (!IsAuthorizedSessionRevisionCurrent(operationRevision))
            {
                return;
            }

            _logger.LogSafeFailure(
                LogLevel.Error,
                DiagnosticOperationCodes.DocumentRemovalUnexpectedFailure,
                LogMessages.DocumentRemovalFailed,
                ex);

            if (!IsAuthorizedSessionRevisionCurrent(operationRevision))
            {
                return;
            }

            _view.SetStatus(
                UiMessages.UnableToRemoveDocumentStatus);

            if (!IsAuthorizedSessionRevisionCurrent(operationRevision))
            {
                return;
            }

            _view.ShowError(
                UiMessages.UnableToRemoveDocument,
                UiMessages.DeskVaultTitle,
                operationRevision);
        }
        finally
        {
            // Do not restore enabled controls after the session has been revoked.
            if (IsAuthorizedSessionRevisionCurrent(operationRevision))
            {
                _view.SetImportEnabled(true);

                if (IsAuthorizedSessionRevisionCurrent(operationRevision))
                {
                    bool hasSelection =
                        _view.SelectedDocumentId.HasValue;

                    _view.SetOpenEnabled(hasSelection);

                    if (IsAuthorizedSessionRevisionCurrent(operationRevision))
                    {
                        _view.SetRemoveEnabled(hasSelection);

                        if (IsAuthorizedSessionRevisionCurrent(operationRevision))
                        {
                            UpdateReprocessEnabled();
                        }
                    }
                }
            }
        }
    }

    private async void OnReprocessRequested(
        object? sender,
        EventArgs e)
    {
        if (!EnsureProtectedAccessAvailable())
        {
            return;
        }

        ApplicationSecuritySessionSnapshot operationSnapshot =
            _applicationSecuritySession.CurrentSnapshot;

        long operationRevision =
            operationSnapshot.Revision;

        if (!IsAuthorizedSessionRevisionCurrent(operationRevision))
        {
            return;
        }

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

            if (IsAuthorizedSessionRevisionCurrent(operationRevision))
            {
                _view.SetReprocessEnabled(false);
            }

            return;
        }

        if (!_documentTextExtractorResolver.CanResolve(fileName))
        {
            _logger.LogDebug(
                LogMessages.DocumentReprocessSkippedWithoutTextExtractor,
                Path.GetExtension(fileName));

            if (IsAuthorizedSessionRevisionCurrent(operationRevision))
            {
                _view.SetReprocessEnabled(false);
            }

            return;
        }

        if (!IsAuthorizedSessionRevisionCurrent(operationRevision))
        {
            return;
        }

        _logger.LogInformation(
            LogMessages.DocumentReprocessStarted);

        _view.SetReprocessEnabled(false);

        if (!IsAuthorizedSessionRevisionCurrent(operationRevision))
        {
            return;
        }

        _view.SetOpenEnabled(false);

        if (!IsAuthorizedSessionRevisionCurrent(operationRevision))
        {
            return;
        }

        _view.SetRemoveEnabled(false);

        if (!IsAuthorizedSessionRevisionCurrent(operationRevision))
        {
            return;
        }

        _view.SetImportEnabled(false);

        if (!IsAuthorizedSessionRevisionCurrent(operationRevision))
        {
            return;
        }

        _view.SetStatus(
            UiMessages.ReprocessingDocumentStatus);

        try
        {
            await _documentProcessingService.ProcessAsync(
                documentId);

            if (!IsAuthorizedSessionRevisionCurrent(operationRevision))
            {
                return;
            }

            await RefreshDocumentsAsync(operationRevision);

            if (!IsAuthorizedSessionRevisionCurrent(operationRevision))
            {
                return;
            }

            _view.SetStatus(
                UiMessages.DocumentReprocessedStatus);

            if (!IsAuthorizedSessionRevisionCurrent(operationRevision))
            {
                return;
            }

            _logger.LogInformation(
                LogMessages.DocumentReprocessCompleted);

            _view.ShowInformation(
                UiMessages.DocumentReprocessedStatus,
                UiMessages.ReprocessDocumentTitle,
                operationRevision);
        }
        catch (Exception ex)
        {
            if (!IsAuthorizedSessionRevisionCurrent(operationRevision))
            {
                return;
            }

            _logger.LogSafeFailure(
                LogLevel.Error,
                DiagnosticOperationCodes.DocumentReprocessFailure,
                LogMessages.DocumentReprocessFailed,
                ex);

            if (!IsAuthorizedSessionRevisionCurrent(operationRevision))
            {
                return;
            }

            _view.SetStatus(
                UiMessages.UnableToReprocessDocumentStatus);

            if (!IsAuthorizedSessionRevisionCurrent(operationRevision))
            {
                return;
            }

            _view.ShowError(
                UiMessages.UnableToReprocessDocument,
                UiMessages.ReprocessDocumentTitle,
                operationRevision);
        }
        finally
        {
            // Do not re-enable protected controls for a stale operation.
            if (IsAuthorizedSessionRevisionCurrent(operationRevision))
            {
                _view.SetImportEnabled(true);

                if (IsAuthorizedSessionRevisionCurrent(operationRevision))
                {
                    bool hasSelection =
                        _view.SelectedDocumentId.HasValue;

                    _view.SetOpenEnabled(hasSelection);

                    if (IsAuthorizedSessionRevisionCurrent(operationRevision))
                    {
                        _view.SetRemoveEnabled(hasSelection);

                        if (IsAuthorizedSessionRevisionCurrent(operationRevision))
                        {
                            string? selectedFileName =
                                _view.SelectedDocumentFileName;

                            bool canReprocess =
                                hasSelection &&
                                !string.IsNullOrWhiteSpace(selectedFileName) &&
                                _documentTextExtractorResolver.CanResolve(
                                    selectedFileName);

                            if (IsAuthorizedSessionRevisionCurrent(operationRevision))
                            {
                                _view.SetReprocessEnabled(canReprocess);
                            }
                        }
                    }
                }
            }
        }
    }

    private async void OnSearchRequested(
        object? sender,
        EventArgs e)
    {
        if (!EnsureProtectedAccessAvailable())
        {
            return;
        }

        long searchSessionRevision =
            _applicationSecuritySession.CurrentSnapshot.Revision;

        if (!IsAuthorizedSessionRevisionCurrent(searchSessionRevision))
        {
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
            string searchText =
                _view.SearchText?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(searchText))
            {
                _currentSearchText = null;
                _currentSearchContinuation = null;

                _logger.LogDebug(
                    LogMessages.DocumentSearchCleared);

                if (!IsCurrentSearchOperation(
                        operationVersion,
                        searchSessionRevision))
                {
                    return;
                }

                await RefreshDocumentsAsync(
                    searchSessionRevision);

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

            if (!IsCurrentSearchOperation(
                    operationVersion,
                    searchSessionRevision))
            {
                return;
            }

            var searchResults =
                MapSearchResults(page.Results);

            if (searchResults.Count == 0)
            {
                if (!IsCurrentSearchOperation(
                        operationVersion,
                        searchSessionRevision))
                {
                    return;
                }

                _logger.LogInformation(
                    LogMessages.DocumentSearchCompletedWithoutResults);

                _view.ShowEmptyState();

                if (!IsCurrentSearchOperation(
                        operationVersion,
                        searchSessionRevision))
                {
                    return;
                }

                _view.SetOpenEnabled(false);
                _view.SetRemoveEnabled(false);
                _view.SetReprocessEnabled(false);
                _view.SetLoadMoreEnabled(false);

                return;
            }

            if (!IsCurrentSearchOperation(
                    operationVersion,
                    searchSessionRevision))
            {
                return;
            }

            _currentSearchContinuation =
                page.Continuation;

            _logger.LogInformation(
                LogMessages.DocumentSearchCompletedWithResults,
                searchResults.Count);

            _view.ShowSearchResults(
                searchResults);

            if (!IsCurrentSearchOperation(
                    operationVersion,
                    searchSessionRevision))
            {
                return;
            }

            _view.SetLoadMoreEnabled(
                page.HasMore);

            bool hasSelection =
                _view.SelectedDocumentId.HasValue;

            if (!IsCurrentSearchOperation(
                    operationVersion,
                    searchSessionRevision))
            {
                return;
            }

            _view.SetOpenEnabled(hasSelection);

            if (!IsCurrentSearchOperation(
                    operationVersion,
                    searchSessionRevision))
            {
                return;
            }

            _view.SetRemoveEnabled(hasSelection);

            if (!IsCurrentSearchOperation(
                    operationVersion,
                    searchSessionRevision))
            {
                return;
            }

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
            if (!IsCurrentSearchOperation(
                    operationVersion,
                    searchSessionRevision))
            {
                return;
            }

            _logger.LogSafeFailure(
                LogLevel.Error,
                DiagnosticOperationCodes.DocumentSearchFailure,
                LogMessages.DocumentSearchFailed,
                ex);

            if (!IsCurrentSearchOperation(
                    operationVersion,
                    searchSessionRevision))
            {
                return;
            }

            _view.SetStatus(
                UiMessages.SearchFailedStatus);

            if (!IsCurrentSearchOperation(
                    operationVersion,
                    searchSessionRevision))
            {
                return;
            }

            _view.ShowError(
                UiMessages.SearchFailedMessage,
                UiMessages.DeskVaultTitle,
                searchSessionRevision);
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
        if (!EnsureProtectedAccessAvailable())
        {
            return;
        }

        long searchSessionRevision =
            _applicationSecuritySession.CurrentSnapshot.Revision;

        if (!IsAuthorizedSessionRevisionCurrent(searchSessionRevision))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_currentSearchText) ||
            _currentSearchContinuation is null)
        {
            if (IsAuthorizedSessionRevisionCurrent(searchSessionRevision))
            {
                _view.SetLoadMoreEnabled(false);
            }

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
            if (!IsCurrentSearchOperation(
                    operationVersion,
                    searchSessionRevision))
            {
                return;
            }

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

            if (!IsCurrentSearchOperation(
                    operationVersion,
                    searchSessionRevision))
            {
                return;
            }

            var searchResults =
                MapSearchResults(page.Results);

            if (searchResults.Count == 0)
            {
                if (!IsCurrentSearchOperation(
                        operationVersion,
                        searchSessionRevision))
                {
                    return;
                }

                _currentSearchContinuation = null;

                _view.SetLoadMoreEnabled(false);

                return;
            }

            if (!IsCurrentSearchOperation(
                    operationVersion,
                    searchSessionRevision))
            {
                return;
            }

            _view.AppendSearchResults(
                searchResults);

            if (!IsCurrentSearchOperation(
                    operationVersion,
                    searchSessionRevision))
            {
                return;
            }

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

            if (IsCurrentSearchOperation(
                    operationVersion,
                    searchSessionRevision))
            {
                _view.SetLoadMoreEnabled(
                    _currentSearchContinuation is not null);
            }
        }
        catch (Exception ex)
        {
            if (!IsCurrentSearchOperation(
                    operationVersion,
                    searchSessionRevision))
            {
                return;
            }

            _logger.LogSafeFailure(
                LogLevel.Error,
                DiagnosticOperationCodes.DocumentSearchLoadMoreFailure,
                LogMessages.DocumentSearchFailed,
                ex);

            if (!IsCurrentSearchOperation(
                    operationVersion,
                    searchSessionRevision))
            {
                return;
            }

            _view.SetLoadMoreEnabled(true);

            if (!IsCurrentSearchOperation(
                    operationVersion,
                    searchSessionRevision))
            {
                return;
            }

            _view.SetStatus(
                UiMessages.SearchFailedStatus);

            if (!IsCurrentSearchOperation(
                    operationVersion,
                    searchSessionRevision))
            {
                return;
            }

            _view.ShowError(
                UiMessages.SearchFailedMessage,
                UiMessages.DeskVaultTitle,
                searchSessionRevision);
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
        CancelSearchOperation(
            Volatile.Read(ref _searchCancellationTokenSource));

        CancellationTokenSource current = new();

        Volatile.Write(
            ref _searchCancellationTokenSource,
            current);

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
        if (_disposed ||
            operationVersion !=
                Volatile.Read(ref _searchOperationVersion))
        {
            return false;
        }

        ApplicationSecuritySessionSnapshot currentSnapshot =
            _applicationSecuritySession.CurrentSnapshot;

        long latestObservedRevocationRevision =
            Volatile.Read(ref _latestObservedRevocationRevision);

        long lastAppliedSecuritySessionRevision =
            Volatile.Read(ref _lastAppliedSecuritySessionRevision);

        return currentSnapshot.IsProtectedAccessAvailable &&
               currentSnapshot.Revision ==
                   lastAppliedSecuritySessionRevision &&
               latestObservedRevocationRevision <=
                   lastAppliedSecuritySessionRevision;
    }

    private bool IsCurrentSearchOperation(
        long operationVersion,
        long expectedSessionRevision)
    {
        return IsCurrentSearchOperation(operationVersion) &&
               IsAuthorizedSessionRevisionCurrent(expectedSessionRevision);
    }

    private void CompleteSearchOperation(
        CancellationTokenSource cancellationTokenSource)
    {
        if (ReferenceEquals(
                Volatile.Read(ref _searchCancellationTokenSource),
                cancellationTokenSource))
        {
            Volatile.Write(
                ref _searchCancellationTokenSource,
                null);
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

    private async Task<int> RefreshDocumentsAsync(
        long? expectedSecuritySessionRevision = null)
    {
        long sessionRevision;

        if (expectedSecuritySessionRevision is long expectedRevision)
        {
            sessionRevision = expectedRevision;

            if (!IsAuthorizedSessionRevisionCurrent(sessionRevision))
            {
                return 0;
            }
        }
        else
        {
            if (!EnsureProtectedAccessAvailable())
            {
                return 0;
            }

            sessionRevision =
                _applicationSecuritySession.CurrentSnapshot.Revision;

            if (!IsAuthorizedSessionRevisionCurrent(sessionRevision))
            {
                return 0;
            }
        }

        _logger.LogDebug(
            LogMessages.DocumentListRefreshStarted);

        var documents =
            await _listDocumentsHandler.HandleAsync(
                new ListDocumentsQuery());

        // Reject results belonging to an earlier or revoked session.
        if (!IsAuthorizedSessionRevisionCurrent(sessionRevision))
        {
            return 0;
        }

        if (documents.Count == 0)
        {
            _logger.LogDebug(
                LogMessages.DocumentListRefreshCompletedWithoutDocuments);

            if (!IsAuthorizedSessionRevisionCurrent(sessionRevision))
            {
                return 0;
            }

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
                document.ProcessingState.ToString()))
            .ToList();

        // Validate again immediately before exposing the retrieved metadata.
        if (!IsAuthorizedSessionRevisionCurrent(sessionRevision))
        {
            return 0;
        }

        _view.ShowDocuments(items);
        _view.SetDocumentsCount(documents.Count);

        bool hasSelection =
            _view.SelectedDocumentId.HasValue;

        _view.SetOpenEnabled(hasSelection);
        _view.SetRemoveEnabled(hasSelection);

        UpdateReprocessEnabled();

        if (!IsAuthorizedSessionRevisionCurrent(sessionRevision))
        {
            return 0;
        }

        _logger.LogDebug(
            LogMessages.DocumentListRefreshCompleted,
            documents.Count);

        return documents.Count;
    }

    private async Task RefreshWorkspacesAsync(
        long? expectedSecuritySessionRevision = null)
    {
        long sessionRevision;

        if (expectedSecuritySessionRevision is long expectedRevision)
        {
            sessionRevision = expectedRevision;

            if (!IsAuthorizedSessionRevisionCurrent(sessionRevision))
            {
                return;
            }
        }
        else
        {
            if (!EnsureProtectedAccessAvailable())
            {
                return;
            }

            sessionRevision =
                _applicationSecuritySession.CurrentSnapshot.Revision;

            if (!IsAuthorizedSessionRevisionCurrent(sessionRevision))
            {
                return;
            }
        }

        _logger.LogDebug(
            LogMessages.WorkspaceListRefreshStarted);

        var result =
            await _getWorkspacesHandler.HandleAsync(
                new GetWorkspacesQuery());

        // Do not expose stale workspace results or failure messages.
        if (!IsAuthorizedSessionRevisionCurrent(sessionRevision))
        {
            return;
        }

        if (result.Status != GetWorkspacesResultStatus.Success)
        {
            _logger.LogWarning(
                LogMessages.WorkspaceListRefreshFailed,
                result.Description);

            if (!IsAuthorizedSessionRevisionCurrent(sessionRevision))
            {
                return;
            }

            _view.ShowWarning(
                result.Description,
                UiMessages.Workspaces,
                sessionRevision);

            return;
        }

        var documents =
            await _listDocumentsHandler.HandleAsync(
                new ListDocumentsQuery());

        // Document names are also protected metadata.
        if (!IsAuthorizedSessionRevisionCurrent(sessionRevision))
        {
            return;
        }

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

        // Final revision check before presenting workspace metadata.
        if (!IsAuthorizedSessionRevisionCurrent(sessionRevision))
        {
            return;
        }

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
        if (!EnsureProtectedAccessAvailable())
        {
            return;
        }

        bool hasSelection =
            _view.SelectedDocumentId.HasValue;

        _view.SetOpenEnabled(hasSelection);
        _view.SetRemoveEnabled(hasSelection);

        UpdateReprocessEnabled();
    }

    private void UpdateReprocessEnabled()
    {
        if (!EnsureProtectedAccessAvailable())
        {
            return;
        }

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
        if (!EnsureProtectedAccessAvailable())
        {
            return;
        }

        _logger.LogDebug(
            LogMessages.DocumentWorkspaceRemovalNotificationReceived);

        await RefreshDocumentsAsync();
    }
}
