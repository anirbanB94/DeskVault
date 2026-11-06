using DeskVault.Application.Documents.Queries.ListDocuments;
using DeskVault.Application.Interfaces;
using DeskVault.Application.Security.Sessions;
using DeskVault.Application.Workspaces.Commands.AddDocumentToWorkspace;
using DeskVault.Application.Workspaces.Commands.RemoveDocumentFromWorkspace;
using DeskVault.Application.Workspaces.Commands.RenameWorkspace;
using DeskVault.Application.Workspaces.Commands.SaveTemporaryWorkspace;
using DeskVault.Application.Workspaces.Commands.UpdateWorkspaceDescription;
using DeskVault.Domain.Workspaces;
using DeskVault.Shared.Extensions;
using DeskVault.UI.Resources;
using DeskVault.UI.Services.Interfaces;
using DeskVault.UI.Views;
using Microsoft.Extensions.Logging;
using DeskVaultDocument = DeskVault.Domain.Documents.Document;
using DiagnosticOperationCodes = DeskVault.Shared.Resources.DiagnosticOperationCodes;

namespace DeskVault.UI.Services.Workspace;

public sealed class WorkspacePresentation : IDisposable
{
    private readonly IWorkspaceDocumentPresentationFactory _documentPresentationFactory;
    private readonly ListDocumentsHandler _listDocumentsHandler;
    private readonly AddDocumentToWorkspaceHandler _addDocumentToWorkspaceHandler;
    private readonly RemoveDocumentFromWorkspaceHandler _removeDocumentFromWorkspaceHandler;
    private readonly RenameWorkspaceHandler _renameWorkspaceHandler;
    private readonly SaveTemporaryWorkspaceHandler _saveTemporaryWorkspaceHandler;
    private readonly UpdateWorkspaceDescriptionHandler _updateWorkspaceDescriptionHandler;
    private readonly IWorkspaceDialogService _dialogService;
    private readonly IWorkspacePresentationView _view;
    private readonly ILogger<WorkspacePresentation> _logger;
    private readonly Dictionary<Guid, WorkspaceDocumentPresentation> _documentPresentations = [];
    private readonly HashSet<Guid> _workspaceDocumentIds = [];
    private bool _disposed;
    private readonly IApplicationSecuritySession _applicationSecuritySession;
    private readonly long _securitySessionRevision;

    public WorkspacePresentation(
        Guid workspaceId,
        WorkspaceType workspaceType,
        string? workspaceName,
        string? description,
        IWorkspaceDocumentPresentationFactory documentPresentationFactory,
        ListDocumentsHandler listDocumentsHandler,
        AddDocumentToWorkspaceHandler addDocumentToWorkspaceHandler,
        RemoveDocumentFromWorkspaceHandler removeDocumentFromWorkspaceHandler,
        RenameWorkspaceHandler renameWorkspaceHandler,
        SaveTemporaryWorkspaceHandler saveTemporaryWorkspaceHandler,
        UpdateWorkspaceDescriptionHandler updateWorkspaceDescriptionHandler,
        IWorkspaceDialogService dialogService,
        IWorkspacePresentationView view,
        ILogger<WorkspacePresentation> logger,
        IApplicationSecuritySession applicationSecuritySession,
        long? securitySessionRevision = null)
    {
        ArgumentNullException.ThrowIfNull(documentPresentationFactory);
        ArgumentNullException.ThrowIfNull(listDocumentsHandler);
        ArgumentNullException.ThrowIfNull(addDocumentToWorkspaceHandler);
        ArgumentNullException.ThrowIfNull(removeDocumentFromWorkspaceHandler);
        ArgumentNullException.ThrowIfNull(renameWorkspaceHandler);
        ArgumentNullException.ThrowIfNull(saveTemporaryWorkspaceHandler);
        ArgumentNullException.ThrowIfNull(updateWorkspaceDescriptionHandler);
        ArgumentNullException.ThrowIfNull(dialogService);
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(applicationSecuritySession);

        WorkspaceId = workspaceId;
        WorkspaceType = workspaceType;
        WorkspaceName = workspaceName;
        WorkspaceDescription = description;
        _documentPresentationFactory = documentPresentationFactory;
        _listDocumentsHandler = listDocumentsHandler;
        _addDocumentToWorkspaceHandler = addDocumentToWorkspaceHandler;
        _removeDocumentFromWorkspaceHandler = removeDocumentFromWorkspaceHandler;
        _renameWorkspaceHandler = renameWorkspaceHandler;
        _saveTemporaryWorkspaceHandler = saveTemporaryWorkspaceHandler;
        _updateWorkspaceDescriptionHandler = updateWorkspaceDescriptionHandler;
        _dialogService = dialogService;
        _view = view;
        _logger = logger;
        _applicationSecuritySession = applicationSecuritySession;
        _securitySessionRevision =
            securitySessionRevision ??
            applicationSecuritySession.CurrentSnapshot.Revision;

        if (IsTemporary)
        {
            _view.SetTemporaryDocumentPresentation(
                workspaceName ?? UiMessages.DocumentWorkspaceFallbackName);
        }
        else
        {
            _view.SetWorkspaceIdentity(
                workspaceName ?? UiMessages.Workspace,
                description);
        }

        _view.DocumentActivated += OnDocumentActivated;
        _view.NavigationModeChanged += OnNavigationModeChanged;
        _view.AddDocumentsRequested += OnAddDocumentsRequested;
        _view.RemoveDocumentsRequested += OnRemoveDocumentsRequested;
        _view.RemoveDocumentRequested += OnRemoveDocumentRequested;
        _view.UpdateWorkspaceDetailsRequested += OnUpdateWorkspaceDetailsRequested;
        _view.SaveAsWorkspaceRequested += OnSaveAsWorkspaceRequested;
        _view.DocumentInformationRequested += OnDocumentInformationRequested;
        _view.DeleteWorkspaceRequested += OnDeleteWorkspaceRequested;
        _view.CloseWorkspaceRequested += OnCloseWorkspaceRequested;
        _view.CloseDocumentRequested += OnCloseDocumentRequested;

        _applicationSecuritySession.StateChanged +=
            OnApplicationSecuritySessionStateChanged;

        // Close the construction race: the owning revision may have been
        // revoked between factory validation and event subscription.
        ApplicationSecuritySessionSnapshot currentSnapshot =
            _applicationSecuritySession.CurrentSnapshot;

        if (!IsSessionRevisionCurrent(_securitySessionRevision))
        {
            OnApplicationSecuritySessionStateChanged(currentSnapshot);
        }
    }

    public Guid WorkspaceId { get; }

    public WorkspaceType WorkspaceType { get; private set; }

    public bool IsTemporary =>
        WorkspaceType == WorkspaceType.Temporary;

    public string? WorkspaceName { get; private set; }

    public string? WorkspaceDescription { get; private set; }

    public WorkspacePresentationNavigationMode NavigationMode { get; private set; } =
        WorkspacePresentationNavigationMode.Tabs;

    public Guid? ActiveDocumentId { get; private set; }

    public IReadOnlyCollection<Guid> WorkspaceDocumentIds =>
        _workspaceDocumentIds;

    public IReadOnlyCollection<Guid> PresentedDocumentIds =>
        _documentPresentations.Keys;

    public IReadOnlyCollection<WorkspaceDocumentPresentation> DocumentPresentations =>
        _documentPresentations.Values;

    internal long SecuritySessionRevision => _securitySessionRevision;

    public event EventHandler<DocumentRemovedEventArgs> DocumentRemoved = null!;

    public event EventHandler WorkspaceDocumentsUpdated = null!;

    public event EventHandler WorkspaceDetailsUpdated = null!;

    public event EventHandler WorkspaceDeleteRequested = null!;

    public event EventHandler WorkspaceCloseRequested = null!;

    public WorkspaceDocumentPresentation? GetDocumentPresentation(
        Guid documentId)
    {
        ThrowIfDisposed();

        return _documentPresentations.TryGetValue(
            documentId,
            out WorkspaceDocumentPresentation? presentation)
                ? presentation
                : null;
    }

    public bool ContainsDocument(Guid documentId)
    {
        ThrowIfDisposed();

        return _documentPresentations.ContainsKey(documentId);
    }

    public bool IsWorkspaceDocument(Guid documentId)
    {
        ThrowIfDisposed();

        return _workspaceDocumentIds.Contains(documentId);
    }

    public async Task SetWorkspaceDocumentsAsync(
        IEnumerable<Guid> documentIds,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(documentIds);

        ApplicationSecuritySessionSnapshot operationSnapshot =
            _applicationSecuritySession.CurrentSnapshot;

        if (!IsSessionRevisionCurrent(operationSnapshot.Revision))
        {
            throw new OperationCanceledException(
                "Workspace presentation is not authorized for its owning session revision.",
                cancellationToken);
        }

        Guid[] workspaceDocumentIds =
            [.. documentIds];

        foreach (Guid documentId in workspaceDocumentIds)
        {
            if (documentId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Workspace document ID cannot be empty.",
                    nameof(documentIds));
            }
        }

        HashSet<Guid> requestedDocumentIds =
            workspaceDocumentIds.ToHashSet();

        IReadOnlyList<DeskVaultDocument> documents =
            await _listDocumentsHandler.HandleAsync(
                new ListDocumentsQuery(),
                cancellationToken);

        // Workspace disposal can occur while the document lookup is pending.
        // A result obtained under an older session revision must never
        // be published, including when the user has since re-authenticated.
        if (!IsSessionRevisionCurrent(operationSnapshot.Revision))
        {
            return;
        }

        Dictionary<Guid, string> workspaceDocuments =
            documents
                .Where(document =>
                    requestedDocumentIds.Contains(document.Id))
                .ToDictionary(
                    document => document.Id,
                    document => document.FileName);

        // Commit membership only after the session revision is verified.
        _workspaceDocumentIds.Clear();

        foreach (Guid documentId in workspaceDocumentIds)
        {
            _workspaceDocumentIds.Add(documentId);
        }

        _view.SetWorkspaceDocuments(
            workspaceDocuments);
    }

    public async Task OpenDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        ApplicationSecuritySessionSnapshot operationSnapshot =
            _applicationSecuritySession.CurrentSnapshot;

        if (!IsSessionRevisionCurrent(operationSnapshot.Revision))
        {
            throw new OperationCanceledException(
                "Workspace presentation is not authorized for its owning session revision.",
                cancellationToken);
        }

        bool presentationCreated = false;

        WorkspaceDocumentPresentation presentation;

        if (_documentPresentations.TryGetValue(
                documentId,
                out WorkspaceDocumentPresentation? existingPresentation))
        {
            presentation = existingPresentation;
        }
        else
        {
            presentation = GetOrCreateDocumentPresentation(
                documentId);

            presentationCreated = true;
        }

        try
        {
            await presentation.Presenter.OpenAsync(
                documentId,
                cancellationToken);

            // The workspace may have been locked, re-authenticated,
            // or disposed while the child document was being opened.
            if (!IsSessionRevisionCurrent(operationSnapshot.Revision))
            {
                throw new OperationCanceledException(
                    "The workspace document-open operation was invalidated.",
                    cancellationToken);
            }

            if (IsTemporary &&
                !string.IsNullOrWhiteSpace(presentation.FileName))
            {
                _view.SetTemporaryDocumentPresentation(
                    presentation.FileName);
            }

            _view.AddDocumentPresentation(
                presentation);

            ActivateDocumentPresentation(
                documentId);

            _logger.LogInformation(
                LogMessages.WorkspacePresentationDocumentOpened,
                WorkspaceId,
                documentId);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            if (presentationCreated)
            {
                RemoveFailedDocumentPresentation(
                    documentId,
                    presentation);
            }

            _logger.LogSafeFailure(
                LogLevel.Error,
                DiagnosticOperationCodes.WorkspacePresentationDocumentOpenFailure,
                LogMessages.WorkspacePresentationDocumentOpenSafeFailure,
                exception);

            throw;
        }
        catch
        {
            if (presentationCreated)
            {
                RemoveFailedDocumentPresentation(
                    documentId,
                    presentation);
            }

            throw;
        }
    }

    public WorkspaceDocumentPresentation OpenDocument(
        Guid documentId)
    {
        ThrowIfDisposed();

        if (!IsProtectedAccessAvailable())
        {
            throw new OperationCanceledException(
                "Workspace presentation is not authorized for the current session.");
        }

        WorkspaceDocumentPresentation presentation =
            GetOrCreateDocumentPresentation(documentId);

        _view.AddDocumentPresentation(
            presentation);

        ActivateDocumentPresentation(
            documentId);

        return presentation;
    }

    public bool ActivateDocument(Guid documentId)
    {
        ThrowIfDisposed();

        if (!IsProtectedAccessAvailable())
        {
            return false;
        }

        if (!_documentPresentations.ContainsKey(documentId))
        {
            return false;
        }

        ActivateDocumentPresentation(
            documentId);

        return true;
    }

    public bool Activate()
    {
        ThrowIfDisposed();

        if (!IsProtectedAccessAvailable())
        {
            return false;
        }

        _view.ActivateWorkspace();

        if (ActiveDocumentId is not Guid documentId)
        {
            return false;
        }

        return ActivateDocument(documentId);
    }

    public void SetNavigationMode(
        WorkspacePresentationNavigationMode navigationMode)
    {
        ThrowIfDisposed();

        if (!IsProtectedAccessAvailable())
        {
            return;
        }

        if (IsTemporary)
        {
            return;
        }

        if (NavigationMode == navigationMode)
        {
            return;
        }

        NavigationMode = navigationMode;

        _view.SetNavigationMode(
            navigationMode);
    }

    public bool CloseDocumentPresentation(
        Guid documentId)
    {
        ThrowIfDisposed();

        if (!_documentPresentations.Remove(
                documentId,
                out WorkspaceDocumentPresentation? presentation))
        {
            return false;
        }

        presentation.Presenter.DocumentRemoved -=
            OnDocumentRemoved;

        _view.RemoveDocumentPresentation(
            documentId);

        presentation.Dispose();

        if (ActiveDocumentId == documentId)
        {
            ActiveDocumentId =
                _documentPresentations.Keys.FirstOrDefault();

            if (ActiveDocumentId == Guid.Empty)
            {
                ActiveDocumentId = null;
            }

            if (ActiveDocumentId is Guid nextDocumentId)
            {
                ActivateDocumentPresentation(
                    nextDocumentId);
            }
        }

        _logger.LogInformation(
            LogMessages.WorkspacePresentationDocumentClosed,
            WorkspaceId,
            documentId);

        return true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

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

        // Detach workspace event handlers independently.
        TryCleanup(() =>
            _applicationSecuritySession.StateChanged -=
                OnApplicationSecuritySessionStateChanged);

        TryCleanup(() =>
            _view.DocumentActivated -= OnDocumentActivated);

        TryCleanup(() =>
            _view.NavigationModeChanged -= OnNavigationModeChanged);

        TryCleanup(() =>
            _view.AddDocumentsRequested -= OnAddDocumentsRequested);

        TryCleanup(() =>
            _view.RemoveDocumentsRequested -= OnRemoveDocumentsRequested);

        TryCleanup(() =>
            _view.RemoveDocumentRequested -= OnRemoveDocumentRequested);

        TryCleanup(() =>
            _view.UpdateWorkspaceDetailsRequested -= OnUpdateWorkspaceDetailsRequested);

        TryCleanup(() =>
            _view.SaveAsWorkspaceRequested -= OnSaveAsWorkspaceRequested);

        TryCleanup(() =>
            _view.DocumentInformationRequested -= OnDocumentInformationRequested);

        TryCleanup(() =>
            _view.DeleteWorkspaceRequested -= OnDeleteWorkspaceRequested);

        TryCleanup(() =>
            _view.CloseWorkspaceRequested -= OnCloseWorkspaceRequested);

        TryCleanup(() =>
            _view.CloseDocumentRequested -= OnCloseDocumentRequested);

        // Snapshot and clear retained workspace state before disposing children.
        WorkspaceDocumentPresentation[] documentPresentations =
            _documentPresentations.Values.ToArray();

        _documentPresentations.Clear();
        _workspaceDocumentIds.Clear();

        ActiveDocumentId = null;
        WorkspaceName = null;
        WorkspaceDescription = null;

        // One failing document presentation must not prevent cleanup of others.
        foreach (WorkspaceDocumentPresentation presentation
            in documentPresentations)
        {
            TryCleanup(() =>
                presentation.Presenter.DocumentRemoved -= OnDocumentRemoved);

            TryCleanup(presentation.Dispose);
        }

        // Always attempt workspace-view cleanup and disposal.
        TryCleanup(_view.ClearPresentation);
        TryCleanup(_view.Dispose);

        _logger.LogDebug(
            LogMessages.WorkspacePresentationDisposed,
            WorkspaceId);

        // Preserve the original exception and stack trace.
        if (disposalException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo
                .Capture(disposalException)
                .Throw();
        }
    }

    private WorkspaceDocumentPresentation GetOrCreateDocumentPresentation(
        Guid documentId)
    {
        if (_documentPresentations.TryGetValue(
                documentId,
                out WorkspaceDocumentPresentation? existing))
        {
            return existing;
        }

        WorkspaceDocumentPresentation presentation =
            _documentPresentationFactory.Create(
                WorkspaceId,
                documentId,
                _securitySessionRevision);

        presentation.Presenter.DocumentRemoved +=
            OnDocumentRemoved;

        _documentPresentations.Add(
            documentId,
            presentation);

        return presentation;
    }

    private void RemoveFailedDocumentPresentation(
        Guid documentId,
        WorkspaceDocumentPresentation presentation)
    {
        if (!_documentPresentations.Remove(
                documentId,
                out WorkspaceDocumentPresentation? registeredPresentation))
        {
            return;
        }

        registeredPresentation.Presenter.DocumentRemoved -=
            OnDocumentRemoved;

        registeredPresentation.Dispose();
    }

    private void ActivateDocumentPresentation(
        Guid documentId)
    {
        WorkspaceDocumentPresentation presentation =
            _documentPresentations[documentId];

        _view.ActivateDocumentPresentation(
            documentId);

        presentation.Activate();

        ActiveDocumentId = documentId;
    }

    private async void OnAddDocumentsRequested(
        object? sender,
        EventArgs e)
    {
        ApplicationSecuritySessionSnapshot operationSnapshot =
            _applicationSecuritySession.CurrentSnapshot;

        if (!IsSessionRevisionCurrent(operationSnapshot.Revision) ||
            _disposed ||
            IsTemporary)
        {
            return;
        }

        try
        {
            IReadOnlyList<DeskVault.Domain.Documents.Document> documents =
                await _listDocumentsHandler.HandleAsync(
                    new ListDocumentsQuery());

            if (!IsSessionRevisionCurrent(operationSnapshot.Revision))
            {
                return;
            }

            WorkspaceDocumentSelectionResult? result =
                _dialogService.ShowDocumentPicker(
                    documents,
                    _workspaceDocumentIds,
                    operationSnapshot.Revision);

            // A modal picker may have remained open across a lock transition.
            if (!IsSessionRevisionCurrent(operationSnapshot.Revision) ||
                result is null)
            {
                return;
            }

            List<Guid> addedDocumentIds = [];

            foreach (Guid documentId in result.SelectedDocumentIds)
            {
                if (!IsSessionRevisionCurrent(operationSnapshot.Revision))
                {
                    return;
                }

                AddDocumentToWorkspaceResult addResult =
                    await _addDocumentToWorkspaceHandler.HandleAsync(
                        new AddDocumentToWorkspaceCommand(
                            WorkspaceId,
                            documentId));

                if (!IsSessionRevisionCurrent(operationSnapshot.Revision))
                {
                    return;
                }

                if (addResult.Status !=
                    AddDocumentToWorkspaceResultStatus.Success)
                {
                    _logger.LogWarning(
                        LogMessages.WorkspacePresentationDocumentAddRejected,
                        WorkspaceId,
                        documentId);

                    continue;
                }

                _workspaceDocumentIds.Add(documentId);
                addedDocumentIds.Add(documentId);

                _logger.LogInformation(
                    LogMessages.WorkspacePresentationDocumentAdded,
                    WorkspaceId,
                    documentId);
            }

            if (addedDocumentIds.Count == 0 ||
                !IsSessionRevisionCurrent(operationSnapshot.Revision))
            {
                return;
            }

            await SetWorkspaceDocumentsAsync(
                _workspaceDocumentIds);

            if (!IsSessionRevisionCurrent(operationSnapshot.Revision))
            {
                return;
            }

            foreach (Guid documentId in addedDocumentIds)
            {
                if (!IsSessionRevisionCurrent(operationSnapshot.Revision))
                {
                    return;
                }

                await OpenDocumentAsync(documentId);

                if (!IsSessionRevisionCurrent(operationSnapshot.Revision))
                {
                    return;
                }
            }

            if (IsSessionRevisionCurrent(operationSnapshot.Revision))
            {
                WorkspaceDocumentsUpdated?.Invoke(
                    this,
                    EventArgs.Empty);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Do not report an obsolete operation as a current-session failure.
            if (!IsSessionRevisionCurrent(operationSnapshot.Revision))
            {
                return;
            }

            _logger.LogSafeFailure(
                LogLevel.Error,
                DiagnosticOperationCodes.WorkspacePresentationAddDocumentsFailure,
                LogMessages.WorkspacePresentationAddDocumentsSafeFailure,
                exception);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async void OnRemoveDocumentsRequested(
        object? sender,
        EventArgs e)
    {
        ApplicationSecuritySessionSnapshot operationSnapshot =
            _applicationSecuritySession.CurrentSnapshot;

        if (!IsSessionRevisionCurrent(operationSnapshot.Revision) ||
            _disposed ||
            IsTemporary)
        {
            return;
        }

        try
        {
            IReadOnlyList<DeskVault.Domain.Documents.Document> documents =
                await _listDocumentsHandler.HandleAsync(
                    new ListDocumentsQuery());

            if (!IsSessionRevisionCurrent(operationSnapshot.Revision))
            {
                return;
            }

            IReadOnlyList<DeskVault.Domain.Documents.Document> workspaceDocuments =
                documents
                    .Where(document =>
                        _workspaceDocumentIds.Contains(document.Id))
                    .OrderBy(document => document.FileName)
                    .ToList();

            WorkspaceDocumentSelectionResult? result =
                _dialogService.ShowDocumentRemoval(
                    workspaceDocuments,
                    operationSnapshot.Revision);

            // A modal dialog may return after the session has changed.
            if (!IsSessionRevisionCurrent(operationSnapshot.Revision) ||
                result is null)
            {
                return;
            }

            foreach (Guid documentId in result.SelectedDocumentIds)
            {
                if (!IsSessionRevisionCurrent(operationSnapshot.Revision))
                {
                    return;
                }

                RemoveDocumentFromWorkspaceResult removeResult =
                    await _removeDocumentFromWorkspaceHandler.HandleAsync(
                        new RemoveDocumentFromWorkspaceCommand(
                            WorkspaceId,
                            documentId));

                if (!IsSessionRevisionCurrent(operationSnapshot.Revision))
                {
                    return;
                }

                if (removeResult.Status !=
                    RemoveDocumentFromWorkspaceResultStatus.Success)
                {
                    _logger.LogWarning(
                        LogMessages.WorkspacePresentationDocumentRemoveRejected,
                        WorkspaceId,
                        documentId);

                    continue;
                }

                _workspaceDocumentIds.Remove(documentId);

                CloseDocumentPresentation(
                    documentId);

                _logger.LogInformation(
                    LogMessages.WorkspacePresentationDocumentRemoved,
                    WorkspaceId,
                    documentId);
            }

            if (!IsSessionRevisionCurrent(operationSnapshot.Revision))
            {
                return;
            }

            await SetWorkspaceDocumentsAsync(
                _workspaceDocumentIds);

            if (!IsSessionRevisionCurrent(operationSnapshot.Revision))
            {
                return;
            }

            WorkspaceDocumentsUpdated?.Invoke(
                this,
                EventArgs.Empty);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            if (!IsSessionRevisionCurrent(operationSnapshot.Revision))
            {
                return;
            }

            _logger.LogSafeFailure(
                LogLevel.Error,
                DiagnosticOperationCodes.WorkspacePresentationRemoveDocumentsFailure,
                LogMessages.WorkspacePresentationRemoveDocumentsSafeFailure,
                exception);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void OnRemoveDocumentRequested(
        object? sender,
        EventArgs e)
    {
        if (!IsProtectedAccessAvailable() ||
            !IsTemporary)
        {
            return;
        }

        WorkspaceDocumentPresentation? presentation =
            GetActiveDocumentPresentation();

        if (presentation is null)
        {
            return;
        }

        presentation.Presenter.RequestRemoveDocument();
    }

    private async void OnSaveAsWorkspaceRequested(
        object? sender,
        EventArgs e)
    {
        ApplicationSecuritySessionSnapshot operationSnapshot =
            _applicationSecuritySession.CurrentSnapshot;

        if (!IsSessionRevisionCurrent(operationSnapshot.Revision) ||
            _disposed ||
            !IsTemporary)
        {
            return;
        }

        try
        {
            string currentName =
                WorkspaceName ?? string.Empty;

            WorkspaceDetailsDialogResult? dialogResult =
                _dialogService.ShowWorkspaceDetails(
                    currentName,
                    WorkspaceDescription,
                    UiMessages.SaveAsWorkspace,
                    operationSnapshot.Revision);

            // The session may have changed while the modal dialog was open.
            if (!IsSessionRevisionCurrent(operationSnapshot.Revision) ||
                dialogResult is null)
            {
                return;
            }

            SaveTemporaryWorkspaceResult result =
                await _saveTemporaryWorkspaceHandler.HandleAsync(
                    new SaveTemporaryWorkspaceCommand(
                        WorkspaceId,
                        dialogResult.WorkspaceName,
                        dialogResult.WorkspaceDescription));

            if (!IsSessionRevisionCurrent(operationSnapshot.Revision))
            {
                return;
            }

            if (result.Status !=
                SaveTemporaryWorkspaceResultStatus.Success ||
                result.Workspace is null)
            {
                _logger.LogWarning(
                    LogMessages.WorkspacePresentationSaveAsRejected,
                    WorkspaceId);

                return;
            }

            WorkspaceType =
                result.Workspace.TypeOfWorkspace;

            WorkspaceName =
                result.Workspace.Name;

            WorkspaceDescription =
                result.Workspace.Description;

            _view.SetWorkspaceIdentity(
                WorkspaceName ?? UiMessages.Workspace,
                WorkspaceDescription);

            WorkspaceDetailsUpdated?.Invoke(
                this,
                EventArgs.Empty);

            _logger.LogInformation(
                LogMessages.WorkspacePresentationSavedAsWorkspace,
                WorkspaceId);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            if (!IsSessionRevisionCurrent(operationSnapshot.Revision))
            {
                return;
            }

            _logger.LogSafeFailure(
                LogLevel.Error,
                DiagnosticOperationCodes.WorkspacePresentationSaveAsWorkspaceFailure,
                LogMessages.WorkspacePresentationSaveAsWorkspaceSafeFailure,
                exception);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void OnDocumentInformationRequested(
        object? sender,
        EventArgs e)
    {
        if (!IsProtectedAccessAvailable())
        {
            return;
        }

        WorkspaceDocumentPresentation? presentation =
            GetActiveDocumentPresentation();

        presentation?.Presenter.ShowDocumentInformation();
    }

    private void OnUpdateWorkspaceDetailsRequested(
        object? sender,
        EventArgs e)
    {
        if (!IsProtectedAccessAvailable() ||
            IsTemporary)
        {
            return;
        }

        UpdateWorkspaceDetailsAsync();
    }

    private async void UpdateWorkspaceDetailsAsync()
    {
        ApplicationSecuritySessionSnapshot operationSnapshot =
            _applicationSecuritySession.CurrentSnapshot;

        if (!IsSessionRevisionCurrent(operationSnapshot.Revision) ||
            _disposed)
        {
            return;
        }

        try
        {
            string currentName =
                WorkspaceName ?? string.Empty;

            WorkspaceDetailsDialogResult? dialogResult =
                _dialogService.ShowWorkspaceDetails(
                    currentName,
                    WorkspaceDescription,
                    UiMessages.UpdateWorkspaceDetailsTitle,
                    operationSnapshot.Revision);

            // The session may have changed while the modal dialog was open.
            if (!IsSessionRevisionCurrent(operationSnapshot.Revision) ||
                dialogResult is null)
            {
                return;
            }

            string updatedName =
                dialogResult.WorkspaceName;

            string? updatedDescription =
                dialogResult.WorkspaceDescription;

            bool nameChanged =
                !string.Equals(
                    currentName,
                    updatedName,
                    StringComparison.Ordinal);

            bool descriptionChanged =
                !string.Equals(
                    WorkspaceDescription,
                    updatedDescription,
                    StringComparison.Ordinal);

            if (!nameChanged && !descriptionChanged)
            {
                return;
            }

            bool updateSucceeded = true;

            if (nameChanged)
            {
                RenameWorkspaceResult renameResult =
                    await _renameWorkspaceHandler.HandleAsync(
                        new RenameWorkspaceCommand(
                            WorkspaceId,
                            updatedName));

                if (!IsSessionRevisionCurrent(operationSnapshot.Revision))
                {
                    return;
                }

                if (renameResult.Status ==
                    RenameWorkspaceResultStatus.Success)
                {
                    WorkspaceName =
                        updatedName;
                }
                else
                {
                    updateSucceeded = false;
                }
            }

            if (descriptionChanged)
            {
                if (!IsSessionRevisionCurrent(operationSnapshot.Revision))
                {
                    return;
                }

                UpdateWorkspaceDescriptionResult descriptionResult =
                    await _updateWorkspaceDescriptionHandler.HandleAsync(
                        new UpdateWorkspaceDescriptionCommand(
                            WorkspaceId,
                            updatedDescription));

                if (!IsSessionRevisionCurrent(operationSnapshot.Revision))
                {
                    return;
                }

                if (descriptionResult.Status ==
                    UpdateWorkspaceDescriptionResultStatus.Success)
                {
                    WorkspaceDescription =
                        updatedDescription;
                }
                else
                {
                    updateSucceeded = false;
                }
            }

            if (!IsSessionRevisionCurrent(operationSnapshot.Revision))
            {
                return;
            }

            _view.SetWorkspaceIdentity(
                WorkspaceName ?? UiMessages.Workspace,
                WorkspaceDescription);

            if (updateSucceeded)
            {
                WorkspaceDetailsUpdated?.Invoke(
                    this,
                    EventArgs.Empty);

                _logger.LogInformation(
                    LogMessages.WorkspacePresentationDetailsUpdated,
                    WorkspaceId);
            }
            else
            {
                _logger.LogWarning(
                    LogMessages.WorkspacePresentationDetailsUpdateRejected,
                    WorkspaceId);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            if (!IsSessionRevisionCurrent(operationSnapshot.Revision))
            {
                return;
            }

            _logger.LogSafeFailure(
                LogLevel.Error,
                DiagnosticOperationCodes.WorkspacePresentationUpdateDetailsFailure,
                LogMessages.WorkspacePresentationUpdateDetailsSafeFailure,
                exception);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void OnDeleteWorkspaceRequested(
        object? sender,
        EventArgs e)
    {
        if (!IsProtectedAccessAvailable() ||
            IsTemporary)
        {
            return;
        }

        WorkspaceDeleteRequested?.Invoke(
            this,
            EventArgs.Empty);
    }

    private void OnCloseWorkspaceRequested(
        object? sender,
        EventArgs e)
    {
        if (!IsProtectedAccessAvailable())
        {
            return;
        }

        WorkspaceCloseRequested?.Invoke(
            this,
            EventArgs.Empty);
    }

    private void OnCloseDocumentRequested(
        object? sender,
        EventArgs e)
    {
        if (!IsProtectedAccessAvailable() ||
            !IsTemporary)
        {
            return;
        }

        WorkspaceCloseRequested?.Invoke(
            this,
            EventArgs.Empty);
    }

    private async void OnDocumentActivated(
        object? sender,
        DocumentActivatedEventArgs e)
    {
        ApplicationSecuritySessionSnapshot operationSnapshot =
            _applicationSecuritySession.CurrentSnapshot;

        if (!IsSessionRevisionCurrent(operationSnapshot.Revision) ||
            _disposed)
        {
            return;
        }

        try
        {
            if (_documentPresentations.ContainsKey(
                    e.DocumentId))
            {
                if (!IsSessionRevisionCurrent(operationSnapshot.Revision))
                {
                    return;
                }

                ActiveDocumentId = e.DocumentId;
                return;
            }

            if (!_workspaceDocumentIds.Contains(
                    e.DocumentId))
            {
                return;
            }

            if (!IsSessionRevisionCurrent(operationSnapshot.Revision))
            {
                return;
            }

            await OpenDocumentAsync(
                e.DocumentId);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            if (!IsSessionRevisionCurrent(operationSnapshot.Revision))
            {
                return;
            }

            _logger.LogSafeFailure(
                LogLevel.Error,
                DiagnosticOperationCodes.WorkspacePresentationActivateDocumentFailure,
                LogMessages.WorkspacePresentationActivateDocumentSafeFailure,
                exception);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void OnNavigationModeChanged(
        object? sender,
        NavigationModeChangedEventArgs e)
    {
        if (IsTemporary)
        {
            return;
        }

        NavigationMode = e.NavigationMode;
    }

    private void OnDocumentRemoved(
        object? sender,
        EventArgs e)
    {
        if (sender is not Presenters.DocumentWorkspacePresenter presenter)
        {
            return;
        }

        WorkspaceDocumentPresentation? presentation =
            _documentPresentations.Values.FirstOrDefault(
                candidate =>
                    ReferenceEquals(
                        candidate.Presenter,
                        presenter));

        if (presentation is null)
        {
            return;
        }

        Guid documentId =
            presentation.DocumentId;

        CloseDocumentPresentation(
            documentId);

        _workspaceDocumentIds.Remove(
            documentId);

        DocumentRemoved?.Invoke(
            this,
            new DocumentRemovedEventArgs(
                documentId));

        WorkspaceDocumentsUpdated?.Invoke(
            this,
            EventArgs.Empty);

        if (IsTemporary)
        {
            WorkspaceCloseRequested?.Invoke(
                this,
                EventArgs.Empty);
        }
    }

    private WorkspaceDocumentPresentation? GetActiveDocumentPresentation()
    {
        if (ActiveDocumentId is not Guid documentId)
        {
            return null;
        }

        return GetDocumentPresentation(
            documentId);
    }

    private void OnApplicationSecuritySessionStateChanged(
        ApplicationSecuritySessionSnapshot notification)
    {
        // Ignore notifications that predate the presentation's owning session.
        if (notification.Revision < _securitySessionRevision ||
            (notification.Revision == _securitySessionRevision &&
             notification.IsProtectedAccessAvailable))
        {
            return;
        }

        try
        {
            _view.PostToUi(() =>
            {
                if (_disposed ||
                    IsSessionRevisionCurrent(_securitySessionRevision))
                {
                    return;
                }

                try
                {
                    // A workspace presentation is permanently bound to the
                    // revision that created it. Dispose it on its owning UI
                    // thread rather than leaving stale tabs or metadata visible.
                    Dispose();
                }
                catch (Exception exception)
                {
                    _logger.LogError(
                        "Workspace presentation cleanup after session revocation failed. FailureType={FailureType}",
                        exception.GetType().Name);
                }
            });
        }
        catch (Exception exception)
        {
            _logger.LogError(
                "Workspace presentation could not schedule session-revocation cleanup. FailureType={FailureType}",
                exception.GetType().Name);
        }
    }

    private bool IsProtectedAccessAvailable()
    {
        return IsSessionRevisionCurrent(
            _securitySessionRevision);
    }

    private bool IsSessionRevisionCurrent(
        long expectedRevision)
    {
        if (_disposed ||
            expectedRevision != _securitySessionRevision)
        {
            return false;
        }

        ApplicationSecuritySessionSnapshot currentSnapshot =
            _applicationSecuritySession.CurrentSnapshot;

        return currentSnapshot.IsProtectedAccessAvailable &&
               currentSnapshot.Revision == _securitySessionRevision;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);
    }
}

public sealed class DocumentRemovedEventArgs : EventArgs
{
    public DocumentRemovedEventArgs(Guid documentId)
    {
        DocumentId = documentId;
    }

    public Guid DocumentId { get; }
}

public sealed class DocumentActivatedEventArgs : EventArgs
{
    public DocumentActivatedEventArgs(Guid documentId)
    {
        DocumentId = documentId;
    }

    public Guid DocumentId { get; }
}

public sealed class NavigationModeChangedEventArgs : EventArgs
{
    public NavigationModeChangedEventArgs(
        WorkspacePresentationNavigationMode navigationMode)
    {
        NavigationMode = navigationMode;
    }

    public WorkspacePresentationNavigationMode NavigationMode { get; }
}
