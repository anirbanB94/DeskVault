using DeskVault.Application.Documents.Queries.ListDocuments;
using DeskVault.Application.Interfaces;
using DeskVault.Application.Workspaces.Commands.AddDocumentToWorkspace;
using DeskVault.Application.Workspaces.Commands.RemoveDocumentFromWorkspace;
using DeskVault.Application.Workspaces.Commands.RenameWorkspace;
using DeskVault.Application.Workspaces.Commands.SaveTemporaryWorkspace;
using DeskVault.Application.Workspaces.Commands.UpdateWorkspaceDescription;
using DeskVault.Domain.Workspaces;
using DeskVault.UI.Forms;
using DeskVault.UI.Services.Interfaces;
using DeskVault.UI.Views;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeskVault.UI.Services.Workspace;

public sealed class WorkspacePresentationFactory : IWorkspacePresentationFactory
{
    private readonly IWorkspaceDocumentPresentationFactory _documentPresentationFactory;
    private readonly ListDocumentsHandler _listDocumentsHandler;
    private readonly AddDocumentToWorkspaceHandler _addDocumentToWorkspaceHandler;
    private readonly RemoveDocumentFromWorkspaceHandler _removeDocumentFromWorkspaceHandler;
    private readonly RenameWorkspaceHandler _renameWorkspaceHandler;
    private readonly SaveTemporaryWorkspaceHandler _saveTemporaryWorkspaceHandler;
    private readonly UpdateWorkspaceDescriptionHandler _updateWorkspaceDescriptionHandler;
    private readonly IWorkspaceDialogService _dialogService;
    private readonly IApplicationSecuritySession _applicationSecuritySession;

    public WorkspacePresentationFactory(
        IWorkspaceDocumentPresentationFactory documentPresentationFactory,
        ListDocumentsHandler listDocumentsHandler,
        AddDocumentToWorkspaceHandler addDocumentToWorkspaceHandler,
        RemoveDocumentFromWorkspaceHandler removeDocumentFromWorkspaceHandler,
        RenameWorkspaceHandler renameWorkspaceHandler,
        SaveTemporaryWorkspaceHandler saveTemporaryWorkspaceHandler,
        UpdateWorkspaceDescriptionHandler updateWorkspaceDescriptionHandler,
        IWorkspaceDialogService dialogService,
        IApplicationSecuritySession applicationSecuritySession)
    {
        ArgumentNullException.ThrowIfNull(documentPresentationFactory);
        ArgumentNullException.ThrowIfNull(listDocumentsHandler);
        ArgumentNullException.ThrowIfNull(addDocumentToWorkspaceHandler);
        ArgumentNullException.ThrowIfNull(removeDocumentFromWorkspaceHandler);
        ArgumentNullException.ThrowIfNull(renameWorkspaceHandler);
        ArgumentNullException.ThrowIfNull(saveTemporaryWorkspaceHandler);
        ArgumentNullException.ThrowIfNull(updateWorkspaceDescriptionHandler);
        ArgumentNullException.ThrowIfNull(dialogService);
        ArgumentNullException.ThrowIfNull(applicationSecuritySession);

        _documentPresentationFactory = documentPresentationFactory;
        _listDocumentsHandler = listDocumentsHandler;
        _addDocumentToWorkspaceHandler = addDocumentToWorkspaceHandler;
        _removeDocumentFromWorkspaceHandler = removeDocumentFromWorkspaceHandler;
        _renameWorkspaceHandler = renameWorkspaceHandler;
        _saveTemporaryWorkspaceHandler = saveTemporaryWorkspaceHandler;
        _updateWorkspaceDescriptionHandler = updateWorkspaceDescriptionHandler;
        _dialogService = dialogService;
        _applicationSecuritySession = applicationSecuritySession;
    }

    public WorkspacePresentation Create(
        Guid workspaceId,
        WorkspaceType workspaceType,
        string? workspaceName,
        string? description,
        long securitySessionRevision)
    {
        IWorkspacePresentationView view =
            new WorkspaceViewForm();

        return new WorkspacePresentation(
            workspaceId,
            workspaceType,
            workspaceName,
            description,
            _documentPresentationFactory,
            _listDocumentsHandler,
            _addDocumentToWorkspaceHandler,
            _removeDocumentFromWorkspaceHandler,
            _renameWorkspaceHandler,
            _saveTemporaryWorkspaceHandler,
            _updateWorkspaceDescriptionHandler,
            _dialogService,
            view,
            NullLogger<WorkspacePresentation>.Instance,
            _applicationSecuritySession,
            securitySessionRevision);
    }
}
