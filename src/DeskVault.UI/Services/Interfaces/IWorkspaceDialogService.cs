using DomainDocument = DeskVault.Domain.Documents.Document;

namespace DeskVault.UI.Services.Interfaces;

public interface IWorkspaceDialogService
{
    WorkspaceDetailsDialogResult? ShowWorkspaceDetails(
        string workspaceName,
        string? workspaceDescription,
        string title);

    WorkspaceDocumentSelectionResult? ShowDocumentPicker(
        IReadOnlyList<DomainDocument> documents,
        IEnumerable<Guid> workspaceDocumentIds);

    WorkspaceDocumentSelectionResult? ShowDocumentRemoval(
        IReadOnlyList<DomainDocument> documents);
}

public sealed record WorkspaceDetailsDialogResult(
    string WorkspaceName,
    string? WorkspaceDescription);

public sealed record WorkspaceDocumentSelectionResult(
    IReadOnlyList<Guid> SelectedDocumentIds);
