using DeskVault.Application.Documents.Commands.RemoveDocument;
using DeskVault.Application.Documents.Queries.GetDocument;
using DeskVault.Application.Documents.Queries.OpenDocument;
using DeskVault.UI.Forms;
using DeskVault.UI.Presenters;
using DeskVault.UI.Rendering;
using DeskVault.UI.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace DeskVault.UI.Services.Workspace;

public sealed class WorkspaceDocumentPresentationFactory :
    IWorkspaceDocumentPresentationFactory
{
    private readonly IDocumentContentRendererResolver _rendererResolver;
    private readonly IDocumentViewer _documentViewer;
    private readonly GetDocumentHandler _getDocumentHandler;
    private readonly OpenDocumentHandler _openDocumentHandler;
    private readonly RemoveDocumentHandler _removeDocumentHandler;
    private readonly ILogger<DocumentWorkspacePresenter> _logger;

    public WorkspaceDocumentPresentationFactory(
        IDocumentContentRendererResolver rendererResolver,
        IDocumentViewer documentViewer,
        GetDocumentHandler getDocumentHandler,
        OpenDocumentHandler openDocumentHandler,
        RemoveDocumentHandler removeDocumentHandler,
        ILogger<DocumentWorkspacePresenter> logger)
    {
        ArgumentNullException.ThrowIfNull(rendererResolver);
        ArgumentNullException.ThrowIfNull(documentViewer);
        ArgumentNullException.ThrowIfNull(getDocumentHandler);
        ArgumentNullException.ThrowIfNull(openDocumentHandler);
        ArgumentNullException.ThrowIfNull(removeDocumentHandler);
        ArgumentNullException.ThrowIfNull(logger);

        _rendererResolver = rendererResolver;
        _documentViewer = documentViewer;
        _getDocumentHandler = getDocumentHandler;
        _openDocumentHandler = openDocumentHandler;
        _removeDocumentHandler = removeDocumentHandler;
        _logger = logger;
    }

    public WorkspaceDocumentPresentation Create(
        Guid workspaceId,
        Guid documentId)
    {
        var view =
            new DocumentViewForm(
                _rendererResolver);

        var presenter =
            new DocumentWorkspacePresenter(
                workspaceId,
                view,
                _documentViewer,
                _getDocumentHandler,
                _openDocumentHandler,
                _removeDocumentHandler,
                _logger);

        return new WorkspaceDocumentPresentation(
            documentId,
            presenter,
            view);
    }
}
