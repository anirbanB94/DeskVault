using DeskVault.Application.Documents.Queries.ListDocuments;
using DeskVault.Application.Workspaces.Commands.AddDocumentToWorkspace;
using DeskVault.Application.Workspaces.Commands.RemoveDocumentFromWorkspace;
using DeskVault.Application.Workspaces.Commands.RenameWorkspace;
using DeskVault.Application.Workspaces.Commands.SaveTemporaryWorkspace;
using DeskVault.Application.Workspaces.Commands.UpdateWorkspaceDescription;
using DeskVault.Domain.Workspaces;
using DeskVault.UI.Resources;
using DeskVault.UI.Services.Interfaces;
using DeskVault.UI.Views;
using Microsoft.Extensions.Logging;

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
        ILogger<WorkspacePresentation> logger)
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

        _workspaceDocumentIds.Clear();

        foreach (Guid documentId in workspaceDocumentIds)
        {
            _workspaceDocumentIds.Add(documentId);
        }

        IReadOnlyList<DeskVault.Domain.Documents.Document> documents =
            await _listDocumentsHandler.HandleAsync(
                new ListDocumentsQuery(),
                cancellationToken);

        Dictionary<Guid, string> workspaceDocuments =
            documents
                .Where(document =>
                    _workspaceDocumentIds.Contains(document.Id))
                .ToDictionary(
                    document => document.Id,
                    document => document.FileName);

        _view.SetWorkspaceDocuments(
            workspaceDocuments);
    }

    public async Task OpenDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

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
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            if (presentationCreated)
            {
                RemoveFailedDocumentPresentation(
                    documentId,
                    presentation);
            }

            _logger.LogError(
                exception,
                LogMessages.WorkspacePresentationDocumentOpenFailed,
                WorkspaceId,
                documentId,
                exception.Message);

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

    public WorkspaceDocumentPresentation OpenDocument(
        Guid documentId)
    {
        ThrowIfDisposed();

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

        _view.DocumentActivated -=
            OnDocumentActivated;

        _view.NavigationModeChanged -=
            OnNavigationModeChanged;

        _view.AddDocumentsRequested -=
            OnAddDocumentsRequested;

        _view.RemoveDocumentsRequested -=
            OnRemoveDocumentsRequested;

        _view.RemoveDocumentRequested -=
            OnRemoveDocumentRequested;

        _view.UpdateWorkspaceDetailsRequested -=
            OnUpdateWorkspaceDetailsRequested;

        _view.SaveAsWorkspaceRequested -=
            OnSaveAsWorkspaceRequested;

        _view.DocumentInformationRequested -=
            OnDocumentInformationRequested;

        _view.DeleteWorkspaceRequested -=
            OnDeleteWorkspaceRequested;

        _view.CloseWorkspaceRequested -=
            OnCloseWorkspaceRequested;

        _view.CloseDocumentRequested -=
            OnCloseDocumentRequested;

        foreach (WorkspaceDocumentPresentation presentation in
                 _documentPresentations.Values)
        {
            presentation.Presenter.DocumentRemoved -=
                OnDocumentRemoved;

            presentation.Dispose();
        }

        _documentPresentations.Clear();
        _workspaceDocumentIds.Clear();
        ActiveDocumentId = null;

        _view.CloseWorkspace();

        _logger.LogDebug(
            LogMessages.WorkspacePresentationDisposed,
            WorkspaceId);
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
                documentId);

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
        try
        {
            if (IsTemporary)
            {
                return;
            }

            IReadOnlyList<DeskVault.Domain.Documents.Document> documents =
                await _listDocumentsHandler.HandleAsync(
                    new ListDocumentsQuery());

            WorkspaceDocumentSelectionResult? result =
                _dialogService.ShowDocumentPicker(
                    documents,
                    _workspaceDocumentIds);

            if (result is null)
            {
                return;
            }

            List<Guid> addedDocumentIds = [];

            foreach (Guid documentId in result.SelectedDocumentIds)
            {
                AddDocumentToWorkspaceResult addResult =
                    await _addDocumentToWorkspaceHandler.HandleAsync(
                        new AddDocumentToWorkspaceCommand(
                            WorkspaceId,
                            documentId));

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

            if (addedDocumentIds.Count == 0)
            {
                return;
            }

            await SetWorkspaceDocumentsAsync(
                _workspaceDocumentIds);

            foreach (Guid documentId in addedDocumentIds)
            {
                await OpenDocumentAsync(
                    documentId);
            }

            WorkspaceDocumentsUpdated?.Invoke(
                this,
                EventArgs.Empty);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(
                exception,
                LogMessages.WorkspacePresentationOperationFailed,
                WorkspaceId,
                LogMessages.WorkspacePresentationAddDocumentsOperation,
                exception.Message);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async void OnRemoveDocumentsRequested(
        object? sender,
        EventArgs e)
    {
        try
        {
            if (IsTemporary)
            {
                return;
            }

            IReadOnlyList<DeskVault.Domain.Documents.Document> documents =
                await _listDocumentsHandler.HandleAsync(
                    new ListDocumentsQuery());

            IReadOnlyList<DeskVault.Domain.Documents.Document> workspaceDocuments =
                documents
                    .Where(document =>
                        _workspaceDocumentIds.Contains(document.Id))
                    .OrderBy(document => document.FileName)
                    .ToList();

            WorkspaceDocumentSelectionResult? result =
                _dialogService.ShowDocumentRemoval(
                    workspaceDocuments);

            if (result is null)
            {
                return;
            }

            foreach (Guid documentId in result.SelectedDocumentIds)
            {
                RemoveDocumentFromWorkspaceResult removeResult =
                    await _removeDocumentFromWorkspaceHandler.HandleAsync(
                        new RemoveDocumentFromWorkspaceCommand(
                            WorkspaceId,
                            documentId));

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

            await SetWorkspaceDocumentsAsync(
                _workspaceDocumentIds);

            WorkspaceDocumentsUpdated?.Invoke(
                this,
                EventArgs.Empty);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(
                exception,
                LogMessages.WorkspacePresentationOperationFailed,
                WorkspaceId,
                LogMessages.WorkspacePresentationRemoveDocumentsOperation,
                exception.Message);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void OnRemoveDocumentRequested(
        object? sender,
        EventArgs e)
    {
        if (!IsTemporary)
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
        try
        {
            if (!IsTemporary)
            {
                return;
            }

            string currentName =
                WorkspaceName ?? string.Empty;

            WorkspaceDetailsDialogResult? dialogResult =
                _dialogService.ShowWorkspaceDetails(
                    currentName,
                    WorkspaceDescription,
                    UiMessages.SaveAsWorkspace);

            if (dialogResult is null)
            {
                return;
            }

            SaveTemporaryWorkspaceResult result =
                await _saveTemporaryWorkspaceHandler.HandleAsync(
                    new SaveTemporaryWorkspaceCommand(
                        WorkspaceId,
                        dialogResult.WorkspaceName,
                        dialogResult.WorkspaceDescription));

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
            _logger.LogError(
                exception,
                LogMessages.WorkspacePresentationOperationFailed,
                WorkspaceId,
                LogMessages.WorkspacePresentationSaveAsWorkspaceOperation,
                exception.Message);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void OnDocumentInformationRequested(
        object? sender,
        EventArgs e)
    {
        WorkspaceDocumentPresentation? presentation =
            GetActiveDocumentPresentation();

        presentation?.Presenter.ShowDocumentInformation();
    }

    private void OnUpdateWorkspaceDetailsRequested(
        object? sender,
        EventArgs e)
    {
        if (IsTemporary)
        {
            return;
        }

        UpdateWorkspaceDetailsAsync();
    }

    private async void UpdateWorkspaceDetailsAsync()
    {
        try
        {
            string currentName =
                WorkspaceName ?? string.Empty;

            WorkspaceDetailsDialogResult? dialogResult =
                _dialogService.ShowWorkspaceDetails(
                    currentName,
                    WorkspaceDescription,
                    UiMessages.UpdateWorkspaceDetailsTitle);

            if (dialogResult is null)
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
                UpdateWorkspaceDescriptionResult descriptionResult =
                    await _updateWorkspaceDescriptionHandler.HandleAsync(
                        new UpdateWorkspaceDescriptionCommand(
                            WorkspaceId,
                            updatedDescription));

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
            _logger.LogError(
                exception,
                LogMessages.WorkspacePresentationOperationFailed,
                WorkspaceId,
                LogMessages.WorkspacePresentationUpdateDetailsOperation,
                exception.Message);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void OnDeleteWorkspaceRequested(
        object? sender,
        EventArgs e)
    {
        if (IsTemporary)
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
        WorkspaceCloseRequested?.Invoke(
            this,
            EventArgs.Empty);
    }

    private void OnCloseDocumentRequested(
        object? sender,
        EventArgs e)
    {
        if (!IsTemporary)
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
        try
        {
            if (_documentPresentations.ContainsKey(
                    e.DocumentId))
            {
                ActiveDocumentId = e.DocumentId;
                return;
            }

            if (!_workspaceDocumentIds.Contains(
                    e.DocumentId))
            {
                return;
            }

            await OpenDocumentAsync(
                e.DocumentId);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(
                exception,
                LogMessages.WorkspacePresentationOperationFailed,
                WorkspaceId,
                LogMessages.WorkspacePresentationActivateDocumentOperation,
                exception.Message);
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
