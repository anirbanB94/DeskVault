namespace DeskVault.Application.Workspaces.Commands.UpdateWorkspaceLastActiveDocument;

/// <summary>
/// Represents a request to update the last active document
/// of an active persistent workspace.
/// </summary>
/// <param name="WorkspaceId">
/// The identifier of the workspace whose active document is being updated.
/// </param>
/// <param name="DocumentId">
/// The identifier of the document to retain as the last active workspace document.
/// </param>
public sealed record UpdateWorkspaceLastActiveDocumentCommand(
    Guid WorkspaceId,
    Guid DocumentId);
