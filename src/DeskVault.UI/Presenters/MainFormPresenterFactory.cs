using DeskVault.Application.Documents.Commands.ImportDocument;
using DeskVault.Application.Documents.Commands.RemoveDocument;
using DeskVault.Application.Documents.Extraction;
using DeskVault.Application.Documents.Queries.ListDocuments;
using DeskVault.Application.Documents.Queries.SearchDocuments;
using DeskVault.Application.Interfaces;
using DeskVault.Application.Workspaces.Commands.CloseWorkspace;
using DeskVault.Application.Workspaces.Commands.CreateWorkspace;
using DeskVault.Application.Workspaces.Commands.DeleteWorkspace;
using DeskVault.Application.Workspaces.Commands.OpenWorkspace;
using DeskVault.Application.Workspaces.Queries.GetWorkspaces;
using DeskVault.UI.Services.Interfaces;
using DeskVault.UI.Views;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DeskVault.UI.Presenters;

public sealed class MainFormPresenterFactory :
    IMainFormPresenterFactory
{
    private readonly ImportDocumentHandler _importDocumentHandler;
    private readonly RemoveDocumentHandler _removeDocumentHandler;
    private readonly ListDocumentsHandler _listDocumentsHandler;
    private readonly SearchDocumentsHandler _searchDocumentsHandler;
    private readonly CreateWorkspaceHandler _createWorkspaceHandler;
    private readonly DeleteWorkspaceHandler _deleteWorkspaceHandler;
    private readonly GetWorkspacesHandler _getWorkspacesHandler;
    private readonly OpenWorkspaceHandler _openWorkspaceHandler;
    private readonly CloseWorkspaceHandler _closeWorkspaceHandler;
    private readonly IWorkspacePresentationManager _workspacePresentationManager;
    private readonly IDocumentProcessingService _documentProcessingService;
    private readonly DocumentTextExtractorResolver _documentTextExtractorResolver;
    private readonly ILogger<MainFormPresenter> _logger;
    private readonly IOptions<SearchOptions> _searchOptions;

    public MainFormPresenterFactory(
        ImportDocumentHandler importDocumentHandler,
        RemoveDocumentHandler removeDocumentHandler,
        ListDocumentsHandler listDocumentsHandler,
        SearchDocumentsHandler searchDocumentsHandler,
        CreateWorkspaceHandler createWorkspaceHandler,
        DeleteWorkspaceHandler deleteWorkspaceHandler,
        GetWorkspacesHandler getWorkspacesHandler,
        OpenWorkspaceHandler openWorkspaceHandler,
        CloseWorkspaceHandler closeWorkspaceHandler,
        IWorkspacePresentationManager workspacePresentationManager,
        IDocumentProcessingService documentProcessingService,
        DocumentTextExtractorResolver documentTextExtractorResolver,
        ILogger<MainFormPresenter> logger,
        IOptions<SearchOptions> searchOptions)
    {
        ArgumentNullException.ThrowIfNull(importDocumentHandler);
        ArgumentNullException.ThrowIfNull(removeDocumentHandler);
        ArgumentNullException.ThrowIfNull(listDocumentsHandler);
        ArgumentNullException.ThrowIfNull(searchDocumentsHandler);
        ArgumentNullException.ThrowIfNull(createWorkspaceHandler);
        ArgumentNullException.ThrowIfNull(deleteWorkspaceHandler);
        ArgumentNullException.ThrowIfNull(getWorkspacesHandler);
        ArgumentNullException.ThrowIfNull(openWorkspaceHandler);
        ArgumentNullException.ThrowIfNull(closeWorkspaceHandler);
        ArgumentNullException.ThrowIfNull(workspacePresentationManager);
        ArgumentNullException.ThrowIfNull(documentProcessingService);
        ArgumentNullException.ThrowIfNull(documentTextExtractorResolver);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(searchOptions);

        _importDocumentHandler = importDocumentHandler;
        _removeDocumentHandler = removeDocumentHandler;
        _listDocumentsHandler = listDocumentsHandler;
        _searchDocumentsHandler = searchDocumentsHandler;
        _createWorkspaceHandler = createWorkspaceHandler;
        _deleteWorkspaceHandler = deleteWorkspaceHandler;
        _getWorkspacesHandler = getWorkspacesHandler;
        _openWorkspaceHandler = openWorkspaceHandler;
        _closeWorkspaceHandler = closeWorkspaceHandler;
        _workspacePresentationManager = workspacePresentationManager;
        _documentProcessingService = documentProcessingService;
        _documentTextExtractorResolver = documentTextExtractorResolver;
        _logger = logger;
        _searchOptions = searchOptions;
    }

    public MainFormPresenter Create(
        IMainFormView view)
    {
        ArgumentNullException.ThrowIfNull(view);

        return new MainFormPresenter(
            view,
            _importDocumentHandler,
            _removeDocumentHandler,
            _listDocumentsHandler,
            _searchDocumentsHandler,
            _createWorkspaceHandler,
            _deleteWorkspaceHandler,
            _getWorkspacesHandler,
            _openWorkspaceHandler,
            _closeWorkspaceHandler,
            _workspacePresentationManager,
            _documentProcessingService,
            _documentTextExtractorResolver,
            _logger,
            _searchOptions);
    }
}
