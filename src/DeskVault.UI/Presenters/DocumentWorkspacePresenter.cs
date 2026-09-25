using DeskVault.Application.Documents.Commands.RemoveDocument;
using DeskVault.Application.Documents.Queries.GetDocument;
using DeskVault.Application.Documents.Queries.OpenDocument;
using DeskVault.UI.Resources;
using DeskVault.UI.Services.Interfaces;
using DeskVault.UI.Views;
using Microsoft.Extensions.Logging;

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
    private string? _currentFileName;
    private bool _disposed;

    public Guid WorkspaceId { get; }

    public Guid? CurrentDocumentId =>
        _currentDocument?.Id;

    public string? CurrentDocumentDisplayName =>
        _currentDocument?.DisplayName;

    public string? CurrentDocumentFileName =>
        _currentDocument?.FileName;

    public event EventHandler DocumentRemoved = null!;

    public DocumentWorkspacePresenter(
        Guid workspaceId,
        IDocumentWorkspaceView view,
        IDocumentViewer documentViewer,
        GetDocumentHandler getDocumentHandler,
        OpenDocumentHandler openDocumentHandler,
        RemoveDocumentHandler removeDocumentHandler,
        ILogger<DocumentWorkspacePresenter> logger)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(documentViewer);
        ArgumentNullException.ThrowIfNull(getDocumentHandler);
        ArgumentNullException.ThrowIfNull(openDocumentHandler);
        ArgumentNullException.ThrowIfNull(removeDocumentHandler);
        ArgumentNullException.ThrowIfNull(logger);

        WorkspaceId = workspaceId;
        _view = view;
        _documentViewer = documentViewer;
        _getDocumentHandler = getDocumentHandler;
        _openDocumentHandler = openDocumentHandler;
        _removeDocumentHandler = removeDocumentHandler;
        _logger = logger;

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

        _view.ActivateWorkspace();
    }

    public async Task OpenAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        OpenDocumentResult result =
            await _openDocumentHandler.HandleAsync(
                new OpenDocumentQuery(documentId),
                cancellationToken);

        await OpenAsync(
            documentId,
            result.Content,
            result.FileName,
            cancellationToken);
    }

    public async Task OpenAsync(
        Guid documentId,
        Stream documentStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        _logger.LogInformation(
            LogMessages.DocumentWorkspaceOpenStarted);

        try
        {
            _currentDocument =
                await _getDocumentHandler.HandleAsync(
                    new GetDocumentQuery(documentId),
                    cancellationToken);

            _currentDocumentStream?.Dispose();

            _currentDocumentStream = documentStream;
            _currentFileName = fileName;

            await _view.ShowDocumentAsync(
                documentStream,
                fileName,
                cancellationToken);

            _logger.LogInformation(
                LogMessages.DocumentWorkspaceOpenCompleted);
        }
        catch (NotSupportedException)
        {
            _logger.LogInformation(
                LogMessages.DocumentWorkspacePreviewUnsupported);

            _view.ShowUnsupportedPreview(
                UiMessages.UnsupportedDocumentPreviewMessage);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug(
                LogMessages.DocumentWorkspaceOpenCancelled);

            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                LogMessages.DocumentWorkspaceOpenFailed);

            _view.ShowError(
                UiMessages.UnableToOpenDocument,
                UiMessages.DeskVaultTitle);
        }
    }

    public void ShowDocumentInformation()
    {
        ThrowIfDisposed();

        if (_currentDocument is null)
        {
            _logger.LogDebug(
                LogMessages.DocumentInformationSkippedWithoutDocument);

            return;
        }

        string fileType =
            Path.GetExtension(_currentDocument.FileName)
                .TrimStart('.')
                .ToUpperInvariant();

        _view.ShowDocumentInformation(
            _currentDocument.DisplayName,
            _currentDocument.FileName,
            fileType,
            _currentDocument.ImportedAt,
            _currentDocument.Status.ToString(),
            _currentDocument.Sha256Hash);
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
        if (_currentDocumentStream is null ||
            string.IsNullOrWhiteSpace(_currentFileName))
        {
            _logger.LogDebug(
                LogMessages.DocumentWorkspaceOpenExternallySkipped);

            return;
        }

        _logger.LogInformation(
            LogMessages.DocumentWorkspaceOpenExternallyStarted);

        try
        {
            _currentDocumentStream.Position = 0;

            await _documentViewer.OpenAsync(
                _currentDocumentStream,
                _currentFileName);

            _logger.LogInformation(
                LogMessages.DocumentWorkspaceOpenExternallyCompleted);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                LogMessages.DocumentWorkspaceOpenExternallyFailed);

            _view.ShowError(
                UiMessages.UnableToOpenDocument,
                UiMessages.OpenDocumentTitle);
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
        if (_currentDocument is null)
        {
            _logger.LogDebug(
                LogMessages.DocumentWorkspaceRemovalSkippedWithoutDocument);

            return;
        }

        if (!_view.ConfirmRemoval(
            _currentDocument.FileName))
        {
            _logger.LogDebug(
                LogMessages.DocumentWorkspaceRemovalCancelled);

            return;
        }

        _logger.LogInformation(
            LogMessages.DocumentWorkspaceRemovalStarted);

        try
        {
            var result =
                await _removeDocumentHandler.HandleAsync(
                    new RemoveDocumentCommand(
                        _currentDocument.Id));

            if (result.Status ==
                RemoveDocumentResultStatus.Success)
            {
                _currentDocumentStream?.Dispose();

                _currentDocument = null;
                _currentDocumentStream = null;
                _currentFileName = null;

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
                UiMessages.RemoveFailedTitle);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                LogMessages.DocumentWorkspaceRemovalFailed);

            _view.ShowError(
                UiMessages.UnableToRemoveDocument,
                UiMessages.DeskVaultTitle);
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
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _view.OpenExternallyRequested -=
            OnOpenExternallyRequested;

        _view.DocumentInformationRequested -=
            OnDocumentInformationRequested;

        _view.RemoveDocumentRequested -=
            OnRemoveDocumentRequested;

        _view.CloseWorkspaceRequested -=
            OnCloseWorkspaceRequested;

        _currentDocumentStream?.Dispose();

        _currentDocument = null;
        _currentDocumentStream = null;
        _currentFileName = null;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);
    }
}
