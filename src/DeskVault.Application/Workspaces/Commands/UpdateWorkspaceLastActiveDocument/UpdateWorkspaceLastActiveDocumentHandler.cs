using DeskVault.Application.Interfaces;
using DeskVault.Domain.Workspaces;
using DeskVault.Shared.Resources;
using Microsoft.Extensions.Logging;

namespace DeskVault.Application.Workspaces.Commands.UpdateWorkspaceLastActiveDocument;

/// <summary>
/// Handles requests to update and persist the last active document
/// of an active persistent workspace.
/// </summary>
public sealed class UpdateWorkspaceLastActiveDocumentHandler
{
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IActiveWorkspaceRegistry _activeWorkspaceRegistry;
    private readonly ILogger<UpdateWorkspaceLastActiveDocumentHandler> _logger;

    public UpdateWorkspaceLastActiveDocumentHandler(
        IWorkspaceRepository workspaceRepository,
        IActiveWorkspaceRegistry activeWorkspaceRegistry,
        ILogger<UpdateWorkspaceLastActiveDocumentHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(workspaceRepository);
        ArgumentNullException.ThrowIfNull(activeWorkspaceRegistry);
        ArgumentNullException.ThrowIfNull(logger);

        _workspaceRepository = workspaceRepository;
        _activeWorkspaceRegistry = activeWorkspaceRegistry;
        _logger = logger;
    }

    /// <summary>
    /// Updates the last active document of an active persistent workspace.
    /// </summary>
    /// <param name="command">
    /// The workspace and document identifiers for the requested update.
    /// </param>
    /// <param name="cancellationToken">
    /// A token used to cancel the persistence operation.
    /// </param>
    /// <returns>
    /// The result describing whether the active document was updated,
    /// already unchanged, or could not be updated.
    /// </returns>
    /// <remarks>
    /// The updated workspace is persisted before replacing the active
    /// workspace registry entry. A failed or cancelled persistence
    /// operation therefore does not publish the updated workspace state.
    /// </remarks>
    public async Task<UpdateWorkspaceLastActiveDocumentResult> HandleAsync(
        UpdateWorkspaceLastActiveDocumentCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        cancellationToken.ThrowIfCancellationRequested();

        Workspace? workspace =
            _activeWorkspaceRegistry.Get(command.WorkspaceId);

        if (workspace is null)
        {
            _logger.LogDebug(
                LogMessages.WorkspaceLastActiveDocumentUpdateWorkspaceNotFound);

            return new UpdateWorkspaceLastActiveDocumentResult(
                UpdateWorkspaceLastActiveDocumentResultStatus.WorkspaceNotFound,
                null,
                "The workspace is not active.");
        }

        if (workspace.TypeOfWorkspace != WorkspaceType.Persistent)
        {
            _logger.LogDebug(
                LogMessages.WorkspaceLastActiveDocumentUpdateNotPersistent);

            return new UpdateWorkspaceLastActiveDocumentResult(
                UpdateWorkspaceLastActiveDocumentResultStatus.WorkspaceNotPersistent,
                workspace,
                "Only persistent workspaces can retain an active document.");
        }

        if (!workspace.Memberships.Any(
                membership =>
                    membership.DocumentId == command.DocumentId))
        {
            _logger.LogDebug(
                LogMessages.WorkspaceLastActiveDocumentUpdateNotMember);

            return new UpdateWorkspaceLastActiveDocumentResult(
                UpdateWorkspaceLastActiveDocumentResultStatus.DocumentNotMember,
                workspace,
                "The document is not a member of the workspace.");
        }

        if (workspace.LastActiveDocumentId == command.DocumentId)
        {
            _logger.LogDebug(
                LogMessages.WorkspaceLastActiveDocumentUpdateUnchanged);

            return new UpdateWorkspaceLastActiveDocumentResult(
                UpdateWorkspaceLastActiveDocumentResultStatus.Unchanged,
                workspace,
                "The active document has not changed.");
        }

        Workspace updatedWorkspace =
            Workspace.Restore(
                workspace.Id,
                workspace.Name,
                workspace.Description,
                workspace.TypeOfWorkspace,
                workspace.Memberships,
                workspace.LastActiveDocumentId,
                workspace.LastUpdated);

        updatedWorkspace.SetLastActiveDocument(
            command.DocumentId);

        await _workspaceRepository.UpdateAsync(
            updatedWorkspace,
            cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        // Publish the new in-memory state only after persistence succeeds.
        _activeWorkspaceRegistry.Replace(
            updatedWorkspace);

        _logger.LogInformation(
            LogMessages.WorkspaceLastActiveDocumentUpdateCompleted);

        return new UpdateWorkspaceLastActiveDocumentResult(
            UpdateWorkspaceLastActiveDocumentResultStatus.Success,
            updatedWorkspace,
            "The active workspace document was updated.");
    }
}
