using DomainDocument = DeskVault.Domain.Documents.Document;
using DeskVault.UI.Forms;
using DeskVault.UI.Services.Interfaces;

namespace DeskVault.UI.Services.Workspace;

public sealed class WorkspaceDialogService :
    IWorkspaceDialogService
{
    public WorkspaceDetailsDialogResult? ShowWorkspaceDetails(
        string workspaceName,
        string? workspaceDescription,
        string title)
    {
        using WorkspaceDetailsDialog dialog =
            new(
                workspaceName,
                workspaceDescription)
            {
                Text = title
            };

        if (dialog.ShowDialog() != DialogResult.OK)
        {
            return null;
        }

        return new WorkspaceDetailsDialogResult(
            dialog.WorkspaceName,
            dialog.WorkspaceDescription);
    }

    public WorkspaceDocumentSelectionResult? ShowDocumentPicker(
        IReadOnlyList<DomainDocument> documents,
        IEnumerable<Guid> workspaceDocumentIds)
    {
        using WorkspaceDocumentPickerDialog dialog =
            new(
                documents,
                workspaceDocumentIds);

        if (dialog.ShowDialog() != DialogResult.OK)
        {
            return null;
        }

        return new WorkspaceDocumentSelectionResult(
            dialog.SelectedDocumentIds);
    }

    public WorkspaceDocumentSelectionResult? ShowDocumentRemoval(
        IReadOnlyList<DomainDocument> documents)
    {
        using WorkspaceDocumentRemovalDialog dialog =
            new(documents);

        if (dialog.ShowDialog() != DialogResult.OK)
        {
            return null;
        }

        return new WorkspaceDocumentSelectionResult(
            dialog.SelectedDocumentIds);
    }
}
