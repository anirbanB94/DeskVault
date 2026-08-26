using DeskVault.Application.Documents.Commands.ImportDocument;
using DeskVault.Application.Documents.Commands.RemoveDocument;
using DeskVault.Application.Documents.Queries.ListDocuments;
using DeskVault.Application.Documents.Queries.OpenDocument;
using DeskVault.Application.Documents.Queries.SearchDocuments;
using DeskVault.Application.Interfaces;
using DeskVault.UI.Services;
using DeskVault.UI.Views;
using Microsoft.Extensions.Logging;

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
    private readonly IDocumentProcessingService _documentProcessingService;
    private readonly ILogger<MainFormPresenter> _logger;

    public MainFormPresenterFactory(
        ImportDocumentHandler importDocumentHandler,
        RemoveDocumentHandler removeDocumentHandler,
        OpenDocumentHandler openDocumentHandler,
        ListDocumentsHandler listDocumentsHandler,
        SearchDocumentsHandler searchDocumentsHandler,
        IDocumentWorkspace documentWorkspace,
        IDocumentProcessingService documentProcessingService,
        ILogger<MainFormPresenter> logger)
    {
        _importDocumentHandler = importDocumentHandler;
        _removeDocumentHandler = removeDocumentHandler;
        _openDocumentHandler = openDocumentHandler;
        _listDocumentsHandler = listDocumentsHandler;
        _searchDocumentsHandler = searchDocumentsHandler;
        _documentWorkspace = documentWorkspace;
        _documentProcessingService = documentProcessingService;
        _logger = logger;
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
            _documentWorkspace,
            _documentProcessingService,
            _logger);
    }
}
