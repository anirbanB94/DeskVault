using DeskVault.UI.Resources;
using DeskVault.UI.Services;
using DeskVault.UI.Views;

namespace DeskVault.UI.Presenters;

public sealed class DocumentWorkspacePresenter :
    IDocumentWorkspace
{
    private readonly IDocumentWorkspaceView _view;
    private readonly IDocumentViewer _documentViewer;

    private Stream? _currentDocumentStream;
    private string? _currentFileName;

    public DocumentWorkspacePresenter(
        IDocumentWorkspaceView view,
        IDocumentViewer documentViewer)
    {
        _view = view;
        _documentViewer = documentViewer;

        _view.OpenExternallyRequested +=
            OnOpenExternallyRequested;
    }

    public async Task OpenAsync(
        Stream documentStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
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
}
