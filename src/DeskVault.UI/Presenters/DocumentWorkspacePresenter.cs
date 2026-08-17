using DeskVault.Application.Documents.Commands.RemoveDocument;
using DeskVault.Application.Documents.Queries.GetDocument;
using DeskVault.UI.Resources;
using DeskVault.UI.Services;
using DeskVault.UI.Views;

namespace DeskVault.UI.Presenters;

public sealed class DocumentWorkspacePresenter :
    IDocumentWorkspace
{
    private readonly IDocumentWorkspaceView _view;

    private readonly IDocumentViewer _documentViewer;

    private readonly GetDocumentHandler _getDocumentHandler;

    private readonly RemoveDocumentHandler _removeDocumentHandler;

    private GetDocumentResult? _currentDocument;

    private Stream? _currentDocumentStream;

    private string? _currentFileName;

    public event EventHandler DocumentRemoved = null!;

    public DocumentWorkspacePresenter(
        IDocumentWorkspaceView view,
        IDocumentViewer documentViewer,
        GetDocumentHandler getDocumentHandler,
        RemoveDocumentHandler removeDocumentHandler)
    {
        _view = view;
        _documentViewer = documentViewer;
        _getDocumentHandler = getDocumentHandler;
        _removeDocumentHandler = removeDocumentHandler;

        _view.OpenExternallyRequested +=
            OnOpenExternallyRequested;

        _view.DocumentInformationRequested +=
            OnDocumentInformationRequested;

        _view.RemoveDocumentRequested +=
            OnRemoveDocumentRequested;

        _view.CloseWorkspaceRequested +=
            OnCloseWorkspaceRequested;
    }


    public async Task OpenAsync(
        Guid documentId,
        Stream documentStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {

        _currentDocument = await _getDocumentHandler.HandleAsync(
            new GetDocumentQuery(documentId),
            cancellationToken);

        _currentDocumentStream?.Dispose();

        _currentDocumentStream = documentStream;
        _currentFileName = fileName;

        try
        {

            await _view.ShowDocumentAsync(
                documentStream,
                fileName,
                cancellationToken);
        }
        catch (NotSupportedException)
        {
            _view.ShowUnsupportedPreview(
                UiMessages.UnsupportedDocumentPreviewMessage);
        }
    }

    private async void OnOpenExternallyRequested(
        object? sender,
        EventArgs e)
    {
        if (_currentDocumentStream is null ||
            string.IsNullOrWhiteSpace(_currentFileName))
        {
            return;
        }

        try
        {
            _currentDocumentStream.Position = 0;

            await _documentViewer.OpenAsync(
                _currentDocumentStream,
                _currentFileName);
        }
        catch (Exception)
        {
            _view.ShowError(
                UiMessages.UnableToOpenDocument,
                UiMessages.OpenDocumentTitle);
        }
    }

    private void OnDocumentInformationRequested(
        object? sender,
        EventArgs e)
    {
        if (_currentDocument is null)
        {
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

    private async void OnRemoveDocumentRequested(
        object? sender,
        EventArgs e)
    {
        if (_currentDocument is null)
        {
            return;
        }

        if (!_view.ConfirmRemoval(
            _currentDocument.FileName))
        {
            return;
        }

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

                _view.CloseWorkspace();

                DocumentRemoved?.Invoke(
                    this,
                    EventArgs.Empty);

                return;
            }

            _view.ShowError(
                result.Message,
                UiMessages.RemoveFailedTitle);
        }
        catch (Exception)
        {
            _view.ShowError(
                UiMessages.UnableToRemoveDocument,
                UiMessages.DeskVaultTitle);
        }
    }

    private void OnCloseWorkspaceRequested(
        object? sender,
        EventArgs e)
    {
        _view.CloseWorkspace();
    }

}
