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

    private GetDocumentResult? _currentDocument;

    private Stream? _currentDocumentStream;

    private string? _currentFileName;

    public DocumentWorkspacePresenter(
        IDocumentWorkspaceView view,
        IDocumentViewer documentViewer,
        GetDocumentHandler getDocumentHandler)
    {
        _view = view;
        _documentViewer = documentViewer;
        _getDocumentHandler = getDocumentHandler;

        _view.OpenExternallyRequested +=
            OnOpenExternallyRequested;

        _view.DocumentInformationRequested +=
            OnDocumentInformationRequested;
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

}
