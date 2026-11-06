using DomainDocument = DeskVault.Domain.Documents.Document;

namespace DeskVault.UI.Services.Interfaces;

public interface IWorkspaceDialogService
{
    WorkspaceDetailsDialogResult? ShowWorkspaceDetails(
        string workspaceName,
        string? workspaceDescription,
        string title,
        long expectedSessionRevision = 0);

    WorkspaceDocumentSelectionResult? ShowDocumentPicker(
        IReadOnlyList<DomainDocument> documents,
        IEnumerable<Guid> workspaceDocumentIds,
        long expectedSessionRevision = 0);

    WorkspaceDocumentSelectionResult? ShowDocumentRemoval(
        IReadOnlyList<DomainDocument> documents,
        long expectedSessionRevision = 0);
}

public sealed record WorkspaceDetailsDialogResult(
    string WorkspaceName,
    string? WorkspaceDescription);

public sealed record WorkspaceDocumentSelectionResult(
    IReadOnlyList<Guid> SelectedDocumentIds);
