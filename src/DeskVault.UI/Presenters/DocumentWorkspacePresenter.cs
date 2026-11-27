using DeskVault.Application.Documents.Commands.RemoveDocument;
using DeskVault.Application.Documents.Queries.GetDocument;
using DeskVault.Application.Documents.Queries.OpenDocument;
using DeskVault.Application.Interfaces;
using DeskVault.Application.Security.Sessions;
using DeskVault.Shared.Extensions;
using DeskVault.UI.Rendering;
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
    private readonly IDocumentView _view;
    private readonly IDocumentViewer _documentViewer;
    private readonly GetDocumentHandler _getDocumentHandler;
    private readonly OpenDocumentHandler _openDocumentHandler;
    private readonly RemoveDocumentHandler _removeDocumentHandler;
    private readonly ILogger<DocumentWorkspacePresenter> _logger;
    private GetDocumentResult? _currentDocument;
    private Stream? _currentDocumentStream;
    private Stream? _pendingDocumentStream;
    private long _pendingDocumentStreamOperationVersion;
    private readonly Dictionary<Stream, ExternalDocumentOpenOperation> _externalDocumentOpenOperations =
        new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<Stream, DocumentStreamBorrowState> _documentStreamBorrowStates =
        new(ReferenceEqualityComparer.Instance);
    private string? _currentFileName;
    private bool _disposed;
    private readonly Lock _openOperationSyncRoot = new();
    private CancellationTokenSource? _openOperationCancellationTokenSource;
    private long _openOperationVersion;
    private readonly IApplicationSecuritySession _applicationSecuritySession;
    private long _latestObservedSecuritySessionRevision;
    private long _activeOpenOperationSecuritySessionRevision;
    private long _currentDocumentSecuritySessionRevision;
    private int _sessionRevocationCleanupQueued;
    private readonly long _securitySessionRevision;

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
        IDocumentView view,
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
        bool presentationStreamBorrowed = false;

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
                    out Stream? previousDocumentStream,
                    out CancellationTokenSource? previousExternalOpenCancellation))
            {
                throw new OperationCanceledException(
                    operation.Token);
            }

            presentationStreamBorrowed = true;
            streamRetained = true;

            try
            {
                CancelOpenOperation(
                    previousExternalOpenCancellation);
            }
            finally
            {
                DisposeCurrentDocumentStream(
                    previousDocumentStream);
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
        catch (DocumentContentRendererNotFoundException)
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
            if (presentationStreamBorrowed)
            {
                CompleteDocumentStreamBorrow(documentStream);
            }

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
        out Stream? previousDocumentStream,
        out CancellationTokenSource? previousExternalOpenCancellation)
    {
        lock (_openOperationSyncRoot)
        {
            previousDocumentStream = null;
            previousExternalOpenCancellation = null;

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

            // The renderer borrows the promoted source stream for the complete
            // asynchronous presentation operation. Acquire the borrow while the
            // presenter synchronization boundary still protects promotion.
            BeginDocumentStreamBorrowLocked(documentStream);

            _pendingDocumentStream = null;
            _pendingDocumentStreamOperationVersion = 0;

            if (!ReferenceEquals(
                    _currentDocumentStream,
                    documentStream))
            {
                previousDocumentStream =
                    ReleaseDocumentStreamLocked(
                        _currentDocumentStream,
                        out previousExternalOpenCancellation);
            }

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

    private Stream? ReleaseDocumentStreamLocked(
        Stream? documentStream,
        out CancellationTokenSource? externalOperationToCancel)
    {
        externalOperationToCancel = null;

        if (documentStream is null)
        {
            return null;
        }

        if (_externalDocumentOpenOperations.TryGetValue(
                documentStream,
                out ExternalDocumentOpenOperation? externalOperation))
        {
            externalOperation.ReleaseDocumentStreamOnCompletion = true;

            externalOperationToCancel =
                externalOperation.CancellationTokenSource;
        }

        if (_documentStreamBorrowStates.TryGetValue(
                documentStream,
                out DocumentStreamBorrowState? borrowState)
            && borrowState.ActiveBorrowCount > 0)
        {
            borrowState.ReleaseRequested = true;
            return null;
        }

        _documentStreamBorrowStates.Remove(documentStream);

        return documentStream;
    }

    private void DisposeCurrentDocumentStream(
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
                "Current document stream cleanup failed. FailureType={FailureType}",
                exception.GetType().Name);
        }
    }

    private ExternalDocumentOpenOperation? TryBeginExternalDocumentOpen()
    {
        lock (_openOperationSyncRoot)
        {
            if (_disposed ||
                _currentDocument is null ||
                _currentDocumentStream is not { } documentStream ||
                _currentFileName is not { } fileName ||
                string.IsNullOrWhiteSpace(fileName) ||
                _externalDocumentOpenOperations.ContainsKey(documentStream))
            {
                return null;
            }

            ApplicationSecuritySessionSnapshot currentSnapshot =
                _applicationSecuritySession.CurrentSnapshot;

            ObserveSecuritySessionRevision(
                currentSnapshot.Revision);

            long currentDocumentRevision =
                Volatile.Read(
                    ref _currentDocumentSecuritySessionRevision);

            if (!currentSnapshot.IsProtectedAccessAvailable ||
                currentSnapshot.Revision != _securitySessionRevision ||
                currentDocumentRevision != _securitySessionRevision ||
                Volatile.Read(
                    ref _latestObservedSecuritySessionRevision) >
                        _securitySessionRevision)
            {
                return null;
            }

            var operation = new ExternalDocumentOpenOperation(
                documentStream,
                fileName,
                _securitySessionRevision);

            BeginDocumentStreamBorrowLocked(documentStream);

            _externalDocumentOpenOperations.Add(
                documentStream,
                operation);

            return operation;
        }
    }

    private bool IsExternalDocumentOpenOperationCurrent(
        ExternalDocumentOpenOperation operation)
    {
        lock (_openOperationSyncRoot)
        {
            if (_disposed ||
                !_externalDocumentOpenOperations.TryGetValue(
                    operation.DocumentStream,
                    out ExternalDocumentOpenOperation? activeOperation) ||
                !ReferenceEquals(activeOperation, operation) ||
                !ReferenceEquals(
                    _currentDocumentStream,
                    operation.DocumentStream))
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

    private void CompleteExternalDocumentOpenOperation(
        ExternalDocumentOpenOperation operation)
    {
        Stream? streamToDispose = null;

        lock (_openOperationSyncRoot)
        {
            if (_externalDocumentOpenOperations.TryGetValue(
                    operation.DocumentStream,
                    out ExternalDocumentOpenOperation? activeOperation)
                && ReferenceEquals(activeOperation, operation))
            {
                _externalDocumentOpenOperations.Remove(
                    operation.DocumentStream);

                bool releaseStream =
                    operation.ReleaseDocumentStreamOnCompletion
                    || _disposed
                    || !ReferenceEquals(
                        _currentDocumentStream,
                        operation.DocumentStream);

                if (releaseStream)
                {
                    if (_documentStreamBorrowStates.TryGetValue(
                            operation.DocumentStream,
                            out DocumentStreamBorrowState? borrowState))
                    {
                        borrowState.ReleaseRequested = true;
                    }
                    else
                    {
                        streamToDispose = operation.DocumentStream;
                    }
                }

                streamToDispose ??=
                    CompleteDocumentStreamBorrowLocked(
                        operation.DocumentStream);
            }
        }

        try
        {
            DisposeCurrentDocumentStream(streamToDispose);
        }
        finally
        {
            operation.CancellationTokenSource.Dispose();
        }
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
        Stream? streamToDispose;
        CancellationTokenSource? externalOperationToCancel;

        lock (_openOperationSyncRoot)
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

            streamToDispose =
                ReleaseDocumentStreamLocked(
                    documentStream,
                    out externalOperationToCancel);
        }

        try
        {
            CancelOpenOperation(
                externalOperationToCancel);
        }
        finally
        {
            try
            {
                _view.ClearPresentation();
            }
            finally
            {
                DisposeCurrentDocumentStream(
                    streamToDispose);
            }
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
        CancellationTokenSource[] externalOperationsToCancel = [];

        lock (_openOperationSyncRoot)
        {
            if (!_disposed)
            {
                externalOperationsToCancel =
                    [.. _externalDocumentOpenOperations.Values.Select(operation => operation.CancellationTokenSource)];
            }

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

            foreach (CancellationTokenSource externalOperationToCancel
                     in externalOperationsToCancel)
            {
                CancelOpenOperation(
                    externalOperationToCancel);
            }
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
                ReleaseDocumentStreamLocked(
                    _currentDocumentStream,
                    out _);

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
            DisposeCurrentDocumentStream(
                currentDocumentStream);
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
        if (!HasAuthorizedCurrentDocument())
        {
            _logger.LogDebug(
                LogMessages.DocumentWorkspaceOpenExternallySkipped);

            return;
        }

        ExternalDocumentOpenOperation? operation =
            TryBeginExternalDocumentOpen();

        if (operation is null)
        {
            _logger.LogDebug(
                LogMessages.DocumentWorkspaceOpenExternallySkipped);

            return;
        }

        _logger.LogInformation(
            LogMessages.DocumentWorkspaceOpenExternallyStarted);

        try
        {
            operation.CancellationTokenSource.Token
                .ThrowIfCancellationRequested();

            operation.DocumentStream.Position = 0;

            await _documentViewer.OpenAsync(
                operation.DocumentStream,
                operation.FileName,
                operation.CancellationTokenSource.Token);

            _logger.LogInformation(
                LogMessages.DocumentWorkspaceOpenExternallyCompleted);
        }
        catch (OperationCanceledException)
            when (operation.CancellationTokenSource.IsCancellationRequested ||
                  !IsExternalDocumentOpenOperationCurrent(operation))
        {
            _logger.LogDebug(
                LogMessages.DocumentWorkspaceOpenCancelled);
        }
        catch (Exception exception)
        {
            _logger.LogSafeFailure(
                LogLevel.Error,
                DiagnosticOperationCodes.DocumentWorkspaceOpenExternallyFailure,
                LogMessages.DocumentWorkspaceOpenExternallyFailed,
                exception);

            if (!IsExternalDocumentOpenOperationCurrent(operation))
            {
                return;
            }

            _view.ShowError(
                UiMessages.UnableToOpenDocument,
                UiMessages.OpenDocumentTitle,
                operation.SecuritySessionRevision);
        }
        finally
        {
            CompleteExternalDocumentOpenOperation(
                operation);
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
                Stream? streamToDispose = null;
                CancellationTokenSource? externalOperationToCancel = null;

                lock (_openOperationSyncRoot)
                {
                    // Do not clear a different document that may have become current
                    // while the removal operation was awaiting storage/repository work.
                    if (_currentDocument?.Id == currentDocument.Id)
                    {
                        streamToDispose =
                            ReleaseDocumentStreamLocked(
                                _currentDocumentStream,
                                out externalOperationToCancel);

                        _currentDocument = null;
                        _currentDocumentStream = null;
                        _currentFileName = null;

                        Volatile.Write(
                            ref _currentDocumentSecuritySessionRevision,
                            0);
                    }
                }

                try
                {
                    CancelOpenOperation(
                        externalOperationToCancel);
                }
                finally
                {
                    DisposeCurrentDocumentStream(
                        streamToDispose);
                }

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

    public void Dispose()
    {
        CancellationTokenSource? activeOpenOperation;
        CancellationTokenSource[] externalOperationsToCancel;
        Stream? pendingDocumentStream;
        Stream? currentDocumentStreamToDispose;

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

            externalOperationsToCancel =
                [.. _externalDocumentOpenOperations.Values.Select(operation => operation.CancellationTokenSource)];

            pendingDocumentStream =
                TakePendingDocumentStreamLocked();

            _openOperationCancellationTokenSource = null;

            currentDocumentStreamToDispose =
                ReleaseDocumentStreamLocked(
                    _currentDocumentStream,
                    out _);

            _currentDocument = null;
            _currentDocumentStream = null;
            _currentFileName = null;

            Volatile.Write(
                ref _currentDocumentSecuritySessionRevision,
                0);
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

        // Cancellation callbacks must not prevent the remaining cleanup.
        TryCleanup(() =>
            CancelOpenOperation(activeOpenOperation));

        foreach (CancellationTokenSource externalOperationToCancel
                 in externalOperationsToCancel)
        {
            TryCleanup(() =>
                CancelOpenOperation(externalOperationToCancel));
        }

        // Detach event handlers independently.
        TryCleanup(() =>
            _view.OpenExternallyRequested -= OnOpenExternallyRequested);

        TryCleanup(() =>
            _view.DocumentInformationRequested -= OnDocumentInformationRequested);

        TryCleanup(() =>
            _view.RemoveDocumentRequested -= OnRemoveDocumentRequested);

        TryCleanup(() =>
            _applicationSecuritySession.StateChanged -=
                OnApplicationSecuritySessionStateChanged);

        // Clear the protected view even if cancellation or event detachment failed.
        TryCleanup(_view.ClearPresentation);

        // Disposal waits until renderer and external-open borrows
        // have both completed.
        TryCleanup(() =>
            DisposeCurrentDocumentStream(
                currentDocumentStreamToDispose));

        if (!ReferenceEquals(
                pendingDocumentStream,
                currentDocumentStreamToDispose))
        {
            TryCleanup(() =>
                DisposePendingDocumentStream(
                    pendingDocumentStream));
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

    private void BeginDocumentStreamBorrowLocked(
        Stream documentStream)
    {
        if (_documentStreamBorrowStates.TryGetValue(
                documentStream,
                out DocumentStreamBorrowState? borrowState))
        {
            if (borrowState.ReleaseRequested)
            {
                throw new InvalidOperationException(
                    "A document stream pending release cannot acquire another borrow.");
            }

            borrowState.ActiveBorrowCount =
                checked(borrowState.ActiveBorrowCount + 1);

            return;
        }

        _documentStreamBorrowStates.Add(
            documentStream,
            new DocumentStreamBorrowState());
    }

    private Stream? CompleteDocumentStreamBorrowLocked(
        Stream documentStream)
    {
        if (!_documentStreamBorrowStates.TryGetValue(
                documentStream,
                out DocumentStreamBorrowState? borrowState))
        {
            return null;
        }

        if (borrowState.ActiveBorrowCount <= 0)
        {
            return null;
        }

        borrowState.ActiveBorrowCount--;

        if (borrowState.ActiveBorrowCount > 0)
        {
            return null;
        }

        _documentStreamBorrowStates.Remove(documentStream);

        return borrowState.ReleaseRequested
            ? documentStream
            : null;
    }

    private void CompleteDocumentStreamBorrow(
        Stream documentStream)
    {
        Stream? streamToDispose;

        lock (_openOperationSyncRoot)
        {
            streamToDispose =
                CompleteDocumentStreamBorrowLocked(
                    documentStream);
        }

        DisposeCurrentDocumentStream(streamToDispose);
    }

    private sealed class ExternalDocumentOpenOperation
    {
        public ExternalDocumentOpenOperation(
            Stream documentStream,
            string fileName,
            long securitySessionRevision)
        {
            ArgumentNullException.ThrowIfNull(documentStream);
            ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

            DocumentStream = documentStream;
            FileName = fileName;
            SecuritySessionRevision = securitySessionRevision;
            CancellationTokenSource = new CancellationTokenSource();
        }

        public Stream DocumentStream { get; }

        public string FileName { get; }

        public long SecuritySessionRevision { get; }

        public CancellationTokenSource CancellationTokenSource { get; }

        public bool ReleaseDocumentStreamOnCompletion { get; set; }
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

    private sealed class DocumentStreamBorrowState
    {
        public int ActiveBorrowCount { get; set; } = 1;

        public bool ReleaseRequested { get; set; }
    }
}
