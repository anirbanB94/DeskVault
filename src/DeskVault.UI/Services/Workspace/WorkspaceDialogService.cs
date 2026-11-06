using DomainDocument = DeskVault.Domain.Documents.Document;
using DeskVault.UI.Forms;
using DeskVault.UI.Services.Interfaces;

namespace DeskVault.UI.Services.Workspace;

public sealed class WorkspaceDialogService :
    IWorkspaceDialogService
{
    private readonly ISessionBoundDialogCoordinator _dialogCoordinator;

    public WorkspaceDialogService(
        ISessionBoundDialogCoordinator dialogCoordinator)
    {
        ArgumentNullException.ThrowIfNull(dialogCoordinator);

        _dialogCoordinator = dialogCoordinator;
    }

    public WorkspaceDetailsDialogResult? ShowWorkspaceDetails(
        string workspaceName,
        string? workspaceDescription,
        string title,
        long expectedSessionRevision = 0)
    {
        using WorkspaceDetailsDialog dialog =
            new(
                workspaceName,
                workspaceDescription)
            {
                Text = title
            };

        if (_dialogCoordinator.ShowDialog(
                dialog,
                expectedSessionRevision) != DialogResult.OK)
        {
            return null;
        }

        return new WorkspaceDetailsDialogResult(
            dialog.WorkspaceName,
            dialog.WorkspaceDescription);
    }

    public WorkspaceDocumentSelectionResult? ShowDocumentPicker(
        IReadOnlyList<DomainDocument> documents,
        IEnumerable<Guid> workspaceDocumentIds,
        long expectedSessionRevision = 0)
    {
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(workspaceDocumentIds);

        using WorkspaceDocumentPickerDialog dialog =
            new(
                documents,
                workspaceDocumentIds);

        if (_dialogCoordinator.ShowDialog(
                dialog,
                expectedSessionRevision) != DialogResult.OK)
        {
            return null;
        }

        return new WorkspaceDocumentSelectionResult(
            dialog.SelectedDocumentIds);
    }

    public WorkspaceDocumentSelectionResult? ShowDocumentRemoval(
        IReadOnlyList<DomainDocument> documents,
        long expectedSessionRevision = 0)
    {
        ArgumentNullException.ThrowIfNull(documents);

        using WorkspaceDocumentRemovalDialog dialog =
            new(documents);

        if (_dialogCoordinator.ShowDialog(
                dialog,
                expectedSessionRevision) != DialogResult.OK)
        {
            return null;
        }

        return new WorkspaceDocumentSelectionResult(
            dialog.SelectedDocumentIds);
    }
}
