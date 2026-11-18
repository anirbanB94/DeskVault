using DeskVault.Domain.Workspaces;

namespace DeskVault.Application.Workspaces.Commands.UpdateWorkspaceLastActiveDocument;

/// <summary>
/// Represents the outcome of an attempt to update a workspace's last active document.
/// </summary>
/// <param name="Status">
/// The status describing the outcome of the update operation.
/// </param>
/// <param name="Workspace">
/// The workspace associated with the operation, when available.
/// </param>
/// <param name="Description">
/// A description of the operation outcome.
/// </param>
public sealed record UpdateWorkspaceLastActiveDocumentResult(
    UpdateWorkspaceLastActiveDocumentResultStatus Status,
    Workspace? Workspace,
    string Description);
