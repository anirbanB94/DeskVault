using DeskVault.Application.Documents.Commands.RemoveDocument;
using DeskVault.Application.Documents.Queries.GetDocument;
using DeskVault.Application.Documents.Queries.OpenDocument;
using DeskVault.Application.Interfaces;
using DeskVault.Application.Security.Sessions;
using DeskVault.Shared.Extensions;
using DeskVault.UI.Resources;
using DeskVault.UI.Services.Interfaces;
using DeskVault.UI.Views;
using Microsoft.Extensions.Logging;
using DiagnosticOperationCodes = DeskVault.Shared.Resources.DiagnosticOperationCodes;

namespace DeskVault.UI.Presenters;

public sealed class DocumentWorkspacePresenter :
    IDocumentWorkspace,
    IDisposable
{
    private readonly IDocumentWorkspaceView _view;
    private readonly IDocumentViewer _documentViewer;
    private readonly GetDocumentHandler _getDocumentHandler;
    private readonly OpenDocumentHandler _openDocumentHandler;
    private readonly RemoveDocumentHandler _removeDocumentHandler;
    private readonly ILogger<DocumentWorkspacePresenter> _logger;
    private GetDocumentResult? _currentDocument;
    private Stream? _currentDocumentStream;
    private Stream? _pendingDocumentStream;
    private long _pendingDocumentStreamOperationVersion;
    private string? _currentFileName;
    private bool _disposed;
    private readonly object _openOperationSyncRoot = new();
    private CancellationTokenSource? _openOperationCancellationTokenSource;
    private long _openOperationVersion;
    private readonly IApplicationSecuritySession _applicationSecuritySession;
    private long _latestObservedSecuritySessionRevision;
    private long _activeOpenOperationSecuritySessionRevision;
    private long _currentDocumentSecuritySessionRevision;
    private int _sessionRevocationCleanupQueued;
    private readonly long _securitySessionRevision;

    public Guid WorkspaceId { get; }

    public Guid? CurrentDocumentId =>
        HasAuthorizedCurrentDocument()
            ? _currentDocument?.Id
            : null;

    public string? CurrentDocumentDisplayName =>
        HasAuthorizedCurrentDocument()
            ? _currentDocument?.DisplayName
            : null;

    public string? CurrentDocumentFileName =>
        HasAuthorizedCurrentDocument()
            ? _currentDocument?.FileName
            : null;

    public event EventHandler DocumentRemoved = null!;

    public DocumentWorkspacePresenter(
        Guid workspaceId,
        IDocumentWorkspaceView view,
        IDocumentViewer documentViewer,
        GetDocumentHandler getDocumentHandler,
        OpenDocumentHandler openDocumentHandler,
        RemoveDocumentHandler removeDocumentHandler,
        ILogger<DocumentWorkspacePresenter> logger,
        IApplicationSecuritySession applicationSecuritySession,
        long? securitySessionRevision = null)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(documentViewer);
        ArgumentNullException.ThrowIfNull(getDocumentHandler);
        ArgumentNullException.ThrowIfNull(openDocumentHandler);
        ArgumentNullException.ThrowIfNull(removeDocumentHandler);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(applicationSecuritySession);

        WorkspaceId = workspaceId;
        _view = view;
        _documentViewer = documentViewer;
        _getDocumentHandler = getDocumentHandler;
        _openDocumentHandler = openDocumentHandler;
        _removeDocumentHandler = removeDocumentHandler;
        _logger = logger;
        _applicationSecuritySession = applicationSecuritySession;
        _securitySessionRevision =
            securitySessionRevision ??
            applicationSecuritySession.CurrentSnapshot.Revision;

        ApplicationSecuritySessionSnapshot initialSnapshot =
            _applicationSecuritySession.CurrentSnapshot;

        _latestObservedSecuritySessionRevision =
            initialSnapshot.Revision;

        _applicationSecuritySession.StateChanged +=
            OnApplicationSecuritySessionStateChanged;

        _view.OpenExternallyRequested +=
            OnOpenExternallyRequested;

        _view.DocumentInformationRequested +=
            OnDocumentInformationRequested;

        _view.RemoveDocumentRequested +=
            OnRemoveDocumentRequested;

        _view.CloseWorkspaceRequested +=
            OnCloseWorkspaceRequested;
    }

    public void Activate()
    {
        ThrowIfDisposed();

        if (!IsOwningSessionCurrent())
        {
            return;
        }

        _view.ActivateWorkspace();
    }

    public async Task OpenAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        OpenOperation operation =
            BeginOpenOperation(cancellationToken);

        Stream? documentStream = null;

        try
        {
            OpenDocumentResult result =
                await _openDocumentHandler.HandleAsync(
                    new OpenDocumentQuery(documentId),
                    operation.Token);

            documentStream = result.Content;

            EnsureOpenOperationCurrent(operation);

            Stream streamForPresentation =
                documentStream;

            documentStream = null;

            await OpenDocumentCoreAsync(
                documentId,
                streamForPresentation,
                result.FileName,
                operation);
        }
        finally
        {
            // Attempt stream cleanup without preventing operation completion.
            try
            {
                DisposePendingDocumentStream(documentStream);
            }
            finally
            {
                CompleteOpenOperation(operation);
            }
        }
    }

    public async Task OpenAsync(
        Guid documentId,
        Stream documentStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documentStream);

        OpenOperation? operation = null;

        try
        {
            operation =
                BeginOpenOperation(cancellationToken);

            await OpenDocumentCoreAsync(
                documentId,
                documentStream,
                fileName,
                operation);
        }
        finally
        {
            if (operation is null)
            {
                DisposePendingDocumentStream(documentStream);
            }
            else
            {
                CompleteOpenOperation(operation);
            }
        }
    }

    private async Task OpenDocumentCoreAsync(
        Guid documentId,
        Stream documentStream,
        string fileName,
        OpenOperation operation)
    {
        bool streamRegistered = false;
        bool streamRetained = false;

        try
        {
            if (!TryRegisterPendingDocumentStream(
                    operation,
                    documentStream))
            {
                DisposePendingDocumentStream(documentStream);

                throw new OperationCanceledException(
                    operation.Token);
            }

            streamRegistered = true;

            GetDocumentResult document =
                await _getDocumentHandler.HandleAsync(
                    new GetDocumentQuery(documentId),
                    operation.Token);

            if (!TryPromotePendingDocumentStream(
                    operation,
                    document,
                    documentStream,
                    fileName,
                    out Stream? previousDocumentStream))
            {
                throw new OperationCanceledException(
                    operation.Token);
            }

            streamRetained = true;

            if (!ReferenceEquals(
                    previousDocumentStream,
                    documentStream))
            {
                previousDocumentStream?.Dispose();
            }

            _logger.LogInformation(
                LogMessages.DocumentWorkspaceOpenStarted);

            await _view.ShowDocumentAsync(
                documentStream,
                fileName,
                operation.Token);

            EnsureOpenOperationCurrent(operation);

            _logger.LogInformation(
                LogMessages.DocumentWorkspaceOpenCompleted);
        }
        catch (NotSupportedException)
        {
            if (!IsOpenOperationCurrent(operation))
            {
                ClearCurrentDocumentIfOwned(documentStream);

                throw new OperationCanceledException(
                    operation.Token);
            }

            _logger.LogInformation(
                LogMessages.DocumentWorkspacePreviewUnsupported);

            _view.ShowUnsupportedPreview(
                UiMessages.UnsupportedDocumentPreviewMessage);
        }
        catch (OperationCanceledException)
        {
            ClearCurrentDocumentIfOwned(documentStream);

            _logger.LogDebug(
                LogMessages.DocumentWorkspaceOpenCancelled);

            throw;
        }
        catch (Exception ex)
        {
            if (!IsOpenOperationCurrent(operation))
            {
                ClearCurrentDocumentIfOwned(documentStream);

                throw new OperationCanceledException(
                    "The document-open operation was invalidated before presentation.",
                    ex,
                    operation.Token);
            }

            _logger.LogSafeFailure(
                LogLevel.Error,
                DiagnosticOperationCodes.DocumentWorkspaceOpenFailure,
                LogMessages.DocumentWorkspaceOpenFailed,
                ex);

            _view.ShowError(
                UiMessages.UnableToOpenDocument,
                UiMessages.DeskVaultTitle,
                _securitySessionRevision);
        }
        finally
        {
            if (!streamRetained && streamRegistered)
            {
                DisposePendingDocumentStream(
                    TakePendingDocumentStream(operation));
            }
        }
    }

    private OpenOperation BeginOpenOperation(
        CancellationToken cancellationToken)
    {
        CancellationTokenSource? previousCancellationTokenSource = null;
        OpenOperation? operation = null;
        Stream? pendingStreamToDispose = null;

        try
        {
            lock (_openOperationSyncRoot)
            {
                // Take ownership of the pending stream before validating
                // the next operation. Cleanup is guaranteed by the finally block.
                pendingStreamToDispose =
                    TakePendingDocumentStreamLocked();

                previousCancellationTokenSource =
                    _openOperationCancellationTokenSource;

                _openOperationCancellationTokenSource = null;
                _activeOpenOperationSecuritySessionRevision = 0;

                // Invalidate the previous operation even when this new
                // operation is rejected by the authorization check.
                long version =
                    ++_openOperationVersion;

                ThrowIfDisposed();

                ApplicationSecuritySessionSnapshot securitySnapshot =
                    _applicationSecuritySession.CurrentSnapshot;

                ObserveSecuritySessionRevision(
                    securitySnapshot.Revision);

                long latestObservedRevision =
                    Volatile.Read(
                        ref _latestObservedSecuritySessionRevision);

                if (!securitySnapshot.IsProtectedAccessAvailable ||
                    securitySnapshot.Revision != _securitySessionRevision ||
                    securitySnapshot.Revision < latestObservedRevision)
                {
                    throw new OperationCanceledException(
                        "Protected document presentation is not authorized.",
                        cancellationToken);
                }

                var cancellationTokenSource =
                    CancellationTokenSource.CreateLinkedTokenSource(
                        cancellationToken);

                _openOperationCancellationTokenSource =
                    cancellationTokenSource;

                _activeOpenOperationSecuritySessionRevision =
                    securitySnapshot.Revision;

                operation =
                    new OpenOperation(
                        version,
                        securitySnapshot.Revision,
                        cancellationTokenSource);
            }
        }
        finally
        {
            // Cancellation callback failures must not prevent stream cleanup.
            try
            {
                CancelOpenOperation(
                    previousCancellationTokenSource);
            }
            finally
            {
                DisposePendingDocumentStream(
                    pendingStreamToDispose);
            }
        }

        return operation
            ?? throw new InvalidOperationException(
                "The authorized document-open operation was not created.");
    }

    private bool IsOpenOperationCurrent(
        OpenOperation operation)
    {
        lock (_openOperationSyncRoot)
        {
            if (_disposed ||
                operation.Version != _openOperationVersion ||
                !ReferenceEquals(
                    _openOperationCancellationTokenSource,
                    operation.CancellationTokenSource) ||
                operation.Token.IsCancellationRequested ||
                operation.SecuritySessionRevision != _securitySessionRevision)
            {
                return false;
            }

            ApplicationSecuritySessionSnapshot currentSnapshot =
                _applicationSecuritySession.CurrentSnapshot;

            ObserveSecuritySessionRevision(
                currentSnapshot.Revision);

            return currentSnapshot.IsProtectedAccessAvailable &&
                   currentSnapshot.Revision ==
                       operation.SecuritySessionRevision &&
                   Volatile.Read(
                       ref _latestObservedSecuritySessionRevision) <=
                       operation.SecuritySessionRevision;
        }
    }

    private bool TryRegisterPendingDocumentStream(
        OpenOperation operation,
        Stream documentStream)
    {
        lock (_openOperationSyncRoot)
        {
            if (_disposed ||
                operation.Version != _openOperationVersion ||
                !ReferenceEquals(
                    _openOperationCancellationTokenSource,
                    operation.CancellationTokenSource) ||
                operation.Token.IsCancellationRequested ||
                operation.SecuritySessionRevision != _securitySessionRevision)
            {
                return false;
            }

            ApplicationSecuritySessionSnapshot currentSnapshot =
                _applicationSecuritySession.CurrentSnapshot;

            ObserveSecuritySessionRevision(
                currentSnapshot.Revision);

            if (!currentSnapshot.IsProtectedAccessAvailable ||
                currentSnapshot.Revision !=
                    operation.SecuritySessionRevision ||
                Volatile.Read(
                    ref _latestObservedSecuritySessionRevision) >
                    operation.SecuritySessionRevision)
            {
                return false;
            }

            if (_pendingDocumentStream is not null)
            {
                return false;
            }

            _pendingDocumentStream = documentStream;
            _pendingDocumentStreamOperationVersion =
                operation.Version;

            return true;
        }
    }

    private bool TryPromotePendingDocumentStream(
        OpenOperation operation,
        GetDocumentResult document,
        Stream documentStream,
        string fileName,
        out Stream? previousDocumentStream)
    {
        lock (_openOperationSyncRoot)
        {
            previousDocumentStream = null;

            if (_disposed ||
                operation.Version != _openOperationVersion ||
                !ReferenceEquals(
                    _openOperationCancellationTokenSource,
                    operation.CancellationTokenSource) ||
                operation.Token.IsCancellationRequested ||
                operation.SecuritySessionRevision != _securitySessionRevision ||
                !ReferenceEquals(
                    _pendingDocumentStream,
                    documentStream) ||
                _pendingDocumentStreamOperationVersion !=
                    operation.Version)
            {
                return false;
            }

            ApplicationSecuritySessionSnapshot currentSnapshot =
                _applicationSecuritySession.CurrentSnapshot;

            ObserveSecuritySessionRevision(
                currentSnapshot.Revision);

            if (!currentSnapshot.IsProtectedAccessAvailable ||
                currentSnapshot.Revision !=
                    operation.SecuritySessionRevision ||
                Volatile.Read(
                    ref _latestObservedSecuritySessionRevision) >
                    operation.SecuritySessionRevision)
            {
                return false;
            }

            _pendingDocumentStream = null;
            _pendingDocumentStreamOperationVersion = 0;

            previousDocumentStream = _currentDocumentStream;

            _currentDocument = document;
            _currentDocumentStream = documentStream;
            _currentFileName = fileName;

            Volatile.Write(
                ref _currentDocumentSecuritySessionRevision,
                operation.SecuritySessionRevision);

            return true;
        }
    }

    private Stream? TakePendingDocumentStream(
        OpenOperation operation)
    {
        lock (_openOperationSyncRoot)
        {
            return TakePendingDocumentStreamLocked(
                operation.Version);
        }
    }

    private Stream? TakePendingDocumentStreamLocked(
        long? operationVersion = null)
    {
        if (_pendingDocumentStream is null ||
            (operationVersion.HasValue &&
             _pendingDocumentStreamOperationVersion !=
                 operationVersion.Value))
        {
            return null;
        }

        Stream documentStream = _pendingDocumentStream;

        _pendingDocumentStream = null;
        _pendingDocumentStreamOperationVersion = 0;

        return documentStream;
    }

    private void EnsureOpenOperationCurrent(
        OpenOperation operation)
    {
        if (!IsOpenOperationCurrent(operation))
        {
            throw new OperationCanceledException(
                operation.Token);
        }
    }

    private void CompleteOpenOperation(
        OpenOperation operation)
    {
        lock (_openOperationSyncRoot)
        {
            if (ReferenceEquals(
                    _openOperationCancellationTokenSource,
                    operation.CancellationTokenSource))
            {
                _openOperationCancellationTokenSource = null;
                _activeOpenOperationSecuritySessionRevision = 0;
            }
        }

        operation.CancellationTokenSource.Dispose();
    }

    private bool HasAuthorizedCurrentDocument()
    {
        Stream? documentStream =
            _currentDocumentStream;

        if (_currentDocument is null ||
            documentStream is null)
        {
            return false;
        }

        ApplicationSecuritySessionSnapshot securitySnapshot =
            _applicationSecuritySession.CurrentSnapshot;

        ObserveSecuritySessionRevision(
            securitySnapshot.Revision);

        long documentRevision =
            Volatile.Read(
                ref _currentDocumentSecuritySessionRevision);

        bool isAuthorized =
            securitySnapshot.IsProtectedAccessAvailable &&
            securitySnapshot.Revision == _securitySessionRevision &&
            documentRevision == _securitySessionRevision &&
            Volatile.Read(
                ref _latestObservedSecuritySessionRevision) <=
                    _securitySessionRevision;

        if (isAuthorized)
        {
            return true;
        }

        // The retained presentation belongs to an earlier or
        // non-authorized session. Clear it before exposing metadata.
        ClearCurrentDocumentIfOwned(documentStream);

        return false;
    }

    private void ClearCurrentDocumentIfOwned(
        Stream documentStream)
    {
        if (!ReferenceEquals(
                _currentDocumentStream,
                documentStream))
        {
            return;
        }

        _currentDocument = null;
        _currentDocumentStream = null;
        _currentFileName = null;

        Volatile.Write(
            ref _currentDocumentSecuritySessionRevision,
            0);

        try
        {
            _view.ClearPresentation();
        }
        finally
        {
            documentStream.Dispose();
        }
    }

    private void ObserveSecuritySessionRevision(
        long revision)
    {
        long observedRevision =
            Volatile.Read(
                ref _latestObservedSecuritySessionRevision);

        while (revision > observedRevision)
        {
            long previousRevision =
                Interlocked.CompareExchange(
                    ref _latestObservedSecuritySessionRevision,
                    revision,
                    observedRevision);

            if (previousRevision == observedRevision)
            {
                return;
            }

            observedRevision = previousRevision;
        }
    }

    private void OnApplicationSecuritySessionStateChanged(
        ApplicationSecuritySessionSnapshot notification)
    {
        ObserveSecuritySessionRevision(
            notification.Revision);

        // This presenter belongs permanently to its construction revision.
        // A later authenticated revision is just as invalid as a locked state.
        if (notification.Revision < _securitySessionRevision ||
            (notification.Revision == _securitySessionRevision &&
             notification.IsProtectedAccessAvailable))
        {
            return;
        }

        CancellationTokenSource? operationToCancel = null;
        Stream? pendingStreamToDispose = null;

        lock (_openOperationSyncRoot)
        {
            if (!_disposed &&
                _openOperationCancellationTokenSource is not null &&
                (notification.Revision >
                    _activeOpenOperationSecuritySessionRevision ||
                 !notification.IsProtectedAccessAvailable))
            {
                operationToCancel =
                    _openOperationCancellationTokenSource;

                _openOperationCancellationTokenSource = null;
                _activeOpenOperationSecuritySessionRevision = 0;

                // Invalidate the operation before running cancellation callbacks.
                long invalidatedOperationVersion =
                    ++_openOperationVersion;

                pendingStreamToDispose =
                    TakePendingDocumentStreamLocked(
                        invalidatedOperationVersion - 1);
            }
        }

        try
        {
            CancelOpenOperation(operationToCancel);
        }
        finally
        {
            DisposePendingDocumentStream(
                pendingStreamToDispose);
        }

        if (Interlocked.Exchange(
                ref _sessionRevocationCleanupQueued,
                1) != 0)
        {
            return;
        }

        try
        {
            // Control access and current-stream release must happen on the
            // owning WinForms thread. The view dispatcher also works before
            // the child form handle has been created.
            _view.PostToUi(
                ClearPresentationAfterSessionRevocation);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                "Document presentation could not schedule session-revocation cleanup. FailureType={FailureType}",
                exception.GetType().Name);
        }
    }

    private void ClearPresentationAfterSessionRevocation()
    {
        Stream? currentDocumentStream;

        lock (_openOperationSyncRoot)
        {
            if (_disposed)
            {
                return;
            }

            currentDocumentStream =
                _currentDocumentStream;

            _currentDocument = null;
            _currentDocumentStream = null;
            _currentFileName = null;

            Volatile.Write(
                ref _currentDocumentSecuritySessionRevision,
                0);
        }

        try
        {
            // Clear rendered controls, labels and unsupported-preview text.
            _view.ClearPresentation();
        }
        catch (Exception exception)
        {
            _logger.LogError(
                "Document presentation view cleanup after session revocation failed. FailureType={FailureType}",
                exception.GetType().Name);
        }
        finally
        {
            DisposeCurrentDocumentStreamAfterSessionRevocation(
                currentDocumentStream);
        }
    }

    private void DisposeCurrentDocumentStreamAfterSessionRevocation(
        Stream? documentStream)
    {
        if (documentStream is null)
        {
            return;
        }

        try
        {
            documentStream.Dispose();
        }
        catch (Exception exception)
        {
            _logger.LogError(
                "Current document stream cleanup after session revocation failed. FailureType={FailureType}",
                exception.GetType().Name);
        }
    }

    public void ShowDocumentInformation()
    {
        ThrowIfDisposed();

        if (!HasAuthorizedCurrentDocument() ||
            _currentDocument is not { } currentDocument)
        {
            _logger.LogDebug(
                LogMessages.DocumentInformationSkippedWithoutDocument);

            return;
        }

        string fileType =
            Path.GetExtension(currentDocument.FileName)
                .TrimStart('.')
                .ToUpperInvariant();

        _view.ShowDocumentInformation(
            currentDocument.DisplayName,
            currentDocument.FileName,
            fileType,
            currentDocument.ImportedAt,
            currentDocument.ProcessingState.ToString(),
            currentDocument.Sha256Hash,
            _securitySessionRevision);
    }

    public void RequestRemoveDocument()
    {
        ThrowIfDisposed();

        OnRemoveDocumentRequested(
            this,
            EventArgs.Empty);
    }

    private async void OnOpenExternallyRequested(
        object? sender,
        EventArgs e)
    {
        if (!HasAuthorizedCurrentDocument() ||
            _currentDocumentStream is not { } documentStream ||
            _currentFileName is not { } fileName ||
            string.IsNullOrWhiteSpace(fileName))
        {
            _logger.LogDebug(
                LogMessages.DocumentWorkspaceOpenExternallySkipped);

            return;
        }

        _logger.LogInformation(
            LogMessages.DocumentWorkspaceOpenExternallyStarted);

        try
        {
            documentStream.Position = 0;

            await _documentViewer.OpenAsync(
                documentStream,
                fileName);

            _logger.LogInformation(
                LogMessages.DocumentWorkspaceOpenExternallyCompleted);
        }
        catch (Exception ex)
        {
            _logger.LogSafeFailure(
                LogLevel.Error,
                DiagnosticOperationCodes.DocumentWorkspaceOpenExternallyFailure,
                LogMessages.DocumentWorkspaceOpenExternallyFailed,
                ex);

            if (!IsOwningSessionCurrent())
            {
                return;
            }

            _view.ShowError(
                UiMessages.UnableToOpenDocument,
                UiMessages.OpenDocumentTitle,
                _securitySessionRevision);
        }
    }

    private void OnDocumentInformationRequested(
        object? sender,
        EventArgs e)
    {
        ShowDocumentInformation();
    }

    private async void OnRemoveDocumentRequested(
        object? sender,
        EventArgs e)
    {
        if (!HasAuthorizedCurrentDocument() ||
            _currentDocument is not { } currentDocument)
        {
            _logger.LogDebug(
                LogMessages.DocumentWorkspaceRemovalSkippedWithoutDocument);

            return;
        }

        bool removalConfirmed;

        try
        {
            removalConfirmed = _view.ConfirmRemoval(
                currentDocument.FileName,
                _securitySessionRevision);
        }
        catch (Exception ex)
        {
            _logger.LogSafeFailure(
                LogLevel.Error,
                DiagnosticOperationCodes.DocumentWorkspaceRemovalConfirmationFailure,
                LogMessages.DocumentWorkspaceRemovalConfirmationFailed,
                ex);

            _view.ShowError(
                UiMessages.UnableToRemoveDocument,
                UiMessages.DeskVaultTitle,
                _securitySessionRevision);

            return;
        }

        if (!removalConfirmed)
        {
            _logger.LogDebug(
                LogMessages.DocumentWorkspaceRemovalCancelled);

            return;
        }

        // Revalidate after the confirmation dialog. The session or
        // current document may have changed while the dialog was open.
        if (!HasAuthorizedCurrentDocument() ||
            _currentDocument?.Id != currentDocument.Id)
        {
            _logger.LogDebug(
                LogMessages.DocumentWorkspaceRemovalSkippedWithoutDocument);

            return;
        }

        _logger.LogInformation(
            LogMessages.DocumentWorkspaceRemovalStarted);

        try
        {
            var result =
                await _removeDocumentHandler.HandleAsync(
                    new RemoveDocumentCommand(
                        currentDocument.Id));

            // The removal handler may complete after session revocation but
            // before the UI-dispatched presentation cleanup has run. Recheck
            // the authoritative owning session before publishing its result.
            if (!IsOwningSessionCurrent())
            {
                return;
            }

            if (result.Status ==
                RemoveDocumentResultStatus.Success)
            {
                _currentDocumentStream?.Dispose();

                _currentDocument = null;
                _currentDocumentStream = null;
                _currentFileName = null;

                Volatile.Write(
                    ref _currentDocumentSecuritySessionRevision,
                    0);

                _logger.LogInformation(
                    LogMessages.DocumentWorkspaceRemovalCompleted);

                DocumentRemoved?.Invoke(
                    this,
                    EventArgs.Empty);

                return;
            }

            _logger.LogWarning(
                LogMessages.DocumentWorkspaceRemovalRejected);

            _view.ShowError(
                result.Message,
                UiMessages.RemoveFailedTitle,
                _securitySessionRevision);
        }
        catch (Exception ex)
        {
            // Do not show an error from a protected operation in a later or
            // locked session. The owning view cleanup handles its teardown.
            if (!IsOwningSessionCurrent())
            {
                return;
            }

            _logger.LogSafeFailure(
                LogLevel.Error,
                DiagnosticOperationCodes.DocumentWorkspaceRemovalFailure,
                LogMessages.DocumentWorkspaceRemovalFailed,
                ex);

            _view.ShowError(
                UiMessages.UnableToRemoveDocument,
                UiMessages.DeskVaultTitle,
                _securitySessionRevision);
        }
    }

    private void OnCloseWorkspaceRequested(
        object? sender,
        EventArgs e)
    {
        _logger.LogDebug(
            LogMessages.DocumentWorkspaceCloseRequested);

        _view.CloseWorkspace();
    }

    public void Dispose()
    {
        CancellationTokenSource? activeOpenOperation;
        Stream? pendingDocumentStream;
        Stream? currentDocumentStream;

        lock (_openOperationSyncRoot)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _openOperationVersion++;

            activeOpenOperation =
                _openOperationCancellationTokenSource;

            pendingDocumentStream =
                TakePendingDocumentStreamLocked();

            _openOperationCancellationTokenSource = null;

            currentDocumentStream = _currentDocumentStream;

            _currentDocument = null;
            _currentDocumentStream = null;
            _currentFileName = null;
        }

        Exception? disposalException = null;

        void TryCleanup(Action cleanup)
        {
            try
            {
                cleanup();
            }
            catch (Exception exception)
            {
                disposalException ??= exception;
            }
        }

        // Cancellation callbacks can throw, but must not interrupt cleanup.
        TryCleanup(() =>
            CancelOpenOperation(activeOpenOperation));

        // Detach event handlers independently.
        TryCleanup(() =>
            _view.OpenExternallyRequested -= OnOpenExternallyRequested);

        TryCleanup(() =>
            _view.DocumentInformationRequested -= OnDocumentInformationRequested);

        TryCleanup(() =>
            _view.RemoveDocumentRequested -= OnRemoveDocumentRequested);

        TryCleanup(() =>
            _view.CloseWorkspaceRequested -= OnCloseWorkspaceRequested);

        TryCleanup(() =>
            _applicationSecuritySession.StateChanged -=
                OnApplicationSecuritySessionStateChanged);

        // Clear the protected view even if cancellation or event detachment failed.
        TryCleanup(_view.ClearPresentation);

        // Dispose both streams independently.
        TryCleanup(() =>
            currentDocumentStream?.Dispose());

        if (!ReferenceEquals(
                pendingDocumentStream,
                currentDocumentStream))
        {
            TryCleanup(() =>
                pendingDocumentStream?.Dispose());
        }

        // Preserve the first exception after all cleanup attempts.
        if (disposalException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo
                .Capture(disposalException)
                .Throw();
        }
    }

    private void CancelOpenOperation(
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
            // The operation completed and disposed its source concurrently.
        }
        catch (AggregateException exception)
        {
            // Cancellation has been requested. A failing callback must not
            // interrupt session invalidation or pending-resource cleanup.
            _logger.LogSafeFailure(
                LogLevel.Error,
                DiagnosticOperationCodes.DocumentWorkspaceCancellationCallbackFailure,
                LogMessages.DocumentWorkspaceCancellationCallbackFailed,
                exception);
        }
    }

    private void DisposePendingDocumentStream(
        Stream? documentStream)
    {
        if (documentStream is null)
        {
            return;
        }

        try
        {
            documentStream.Dispose();
        }
        catch (Exception exception)
        {
            _logger.LogSafeFailure(
                LogLevel.Error,
                DiagnosticOperationCodes.DocumentWorkspacePendingStreamCleanupFailure,
                LogMessages.DocumentWorkspacePendingStreamCleanupFailed,
                exception);
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);
    }

    private bool IsOwningSessionCurrent()
    {
        if (_disposed)
        {
            return false;
        }

        ApplicationSecuritySessionSnapshot currentSnapshot =
            _applicationSecuritySession.CurrentSnapshot;

        ObserveSecuritySessionRevision(
            currentSnapshot.Revision);

        return currentSnapshot.IsProtectedAccessAvailable &&
               currentSnapshot.Revision == _securitySessionRevision &&
               Volatile.Read(
                   ref _latestObservedSecuritySessionRevision) <=
                       _securitySessionRevision;
    }

    private sealed class OpenOperation
    {
        public OpenOperation(
            long version,
            long securitySessionRevision,
            CancellationTokenSource cancellationTokenSource)
        {
            Version = version;
            SecuritySessionRevision = securitySessionRevision;
            CancellationTokenSource = cancellationTokenSource;
            Token = cancellationTokenSource.Token;
        }

        public long Version { get; }

        public long SecuritySessionRevision { get; }

        public CancellationTokenSource CancellationTokenSource { get; }

        public CancellationToken Token { get; }
    }
}
