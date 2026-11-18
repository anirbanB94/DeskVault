namespace DeskVault.Application.Workspaces.Commands.UpdateWorkspaceLastActiveDocument;

/// <summary>
/// Defines the possible outcomes of updating a workspace's last active document.
/// </summary>
public enum UpdateWorkspaceLastActiveDocumentResultStatus
{
    /// <summary>
    /// The last active document was successfully updated and persisted.
    /// </summary>
    Success = 0,

    /// <summary>
    /// The requested workspace is not currently active.
    /// </summary>
    WorkspaceNotFound = 1,

    /// <summary>
    /// The requested workspace is temporary and does not support persistent active-document state.
    /// </summary>
    WorkspaceNotPersistent = 2,

    /// <summary>
    /// The requested document is not a member of the workspace.
    /// </summary>
    DocumentNotMember = 3,

    /// <summary>
    /// The requested document is already the last active document.
    /// No persistence update is necessary.
    /// </summary>
    Unchanged = 4
}
