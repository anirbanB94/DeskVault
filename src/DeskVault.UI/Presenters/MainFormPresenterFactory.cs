using DeskVault.Application.Documents.Commands.ImportDocument;
using DeskVault.Application.Documents.Commands.RemoveDocument;
using DeskVault.Application.Documents.Queries.ListDocuments;
using DeskVault.Application.Documents.Queries.OpenDocument;
using DeskVault.Application.Documents.Queries.SearchDocuments;
using DeskVault.UI.Services;
using DeskVault.UI.Views;

namespace DeskVault.UI.Presenters;

public sealed class MainFormPresenterFactory :
    IMainFormPresenterFactory
{
    private readonly ImportDocumentHandler _importDocumentHandler;
    private readonly RemoveDocumentHandler _removeDocumentHandler;
    private readonly OpenDocumentHandler _openDocumentHandler;
    private readonly ListDocumentsHandler _listDocumentsHandler;
    private readonly SearchDocumentsHandler _searchDocumentsHandler;
    private readonly IDocumentWorkspace _documentWorkspace;

    public MainFormPresenterFactory(
        ImportDocumentHandler importDocumentHandler,
        RemoveDocumentHandler removeDocumentHandler,
        OpenDocumentHandler openDocumentHandler,
        ListDocumentsHandler listDocumentsHandler,
        SearchDocumentsHandler searchDocumentsHandler,
        IDocumentWorkspace documentWorkspace)
    {
        _importDocumentHandler = importDocumentHandler;
        _removeDocumentHandler = removeDocumentHandler;
        _openDocumentHandler = openDocumentHandler;
        _listDocumentsHandler = listDocumentsHandler;
        _searchDocumentsHandler = searchDocumentsHandler;
        _documentWorkspace = documentWorkspace;
    }

    public MainFormPresenter Create(
        IMainFormView view)
    {
        return new MainFormPresenter(
            view,
            _importDocumentHandler,
            _removeDocumentHandler,
            _openDocumentHandler,
            _listDocumentsHandler,
            _searchDocumentsHandler,
            _documentWorkspace);
    }
}
