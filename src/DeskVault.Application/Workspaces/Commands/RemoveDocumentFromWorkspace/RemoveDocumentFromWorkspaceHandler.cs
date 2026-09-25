using DeskVault.Application.Interfaces;
using DeskVault.Domain.Workspaces;
using DeskVault.Shared.Resources;
using Microsoft.Extensions.Logging;

namespace DeskVault.Application.Workspaces.Commands.RemoveDocumentFromWorkspace;

public sealed class RemoveDocumentFromWorkspaceHandler
{
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IActiveWorkspaceRegistry _activeWorkspaceRegistry;
    private readonly ILogger<RemoveDocumentFromWorkspaceHandler> _logger;

    public RemoveDocumentFromWorkspaceHandler(
        IWorkspaceRepository workspaceRepository,
        IActiveWorkspaceRegistry activeWorkspaceRegistry,
        ILogger<RemoveDocumentFromWorkspaceHandler> logger)
    {
        _workspaceRepository = workspaceRepository;
        _activeWorkspaceRegistry = activeWorkspaceRegistry;
        _logger = logger;
    }

    public async Task<RemoveDocumentFromWorkspaceResult> HandleAsync(
        RemoveDocumentFromWorkspaceCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        Workspace? workspace =
            _activeWorkspaceRegistry.Get(command.WorkspaceId);

        if (workspace is null)
        {
            _logger.LogDebug(
                LogMessages.WorkspaceDocumentRemovalWorkspaceNotFound);

            return new RemoveDocumentFromWorkspaceResult(
                RemoveDocumentFromWorkspaceResultStatus.WorkspaceNotFound,
                null,
                "The workspace is not active.");
        }

        if (!workspace.Memberships.Any(
                membership => membership.DocumentId == command.DocumentId))
        {
            _logger.LogDebug(
                LogMessages.WorkspaceDocumentRemovalNotMember);

            return new RemoveDocumentFromWorkspaceResult(
                RemoveDocumentFromWorkspaceResultStatus.DocumentNotMember,
                workspace,
                "The document is not a member of the workspace.");
        }

        if (workspace.TypeOfWorkspace == WorkspaceType.Temporary)
        {
            workspace.RemoveDocument(command.DocumentId);

            _logger.LogInformation(
                LogMessages.WorkspaceDocumentRemovalCompleted);

            return new RemoveDocumentFromWorkspaceResult(
                RemoveDocumentFromWorkspaceResultStatus.Success,
                workspace,
                "Document removed from the workspace.");
        }

        Workspace updatedWorkspace = CreateUpdatedWorkspace(workspace);
        updatedWorkspace.RemoveDocument(command.DocumentId);

        await _workspaceRepository.UpdateAsync(
            updatedWorkspace,
            cancellationToken);

        _activeWorkspaceRegistry.Replace(updatedWorkspace);

        _logger.LogInformation(
            LogMessages.WorkspaceDocumentRemovalCompleted);

        return new RemoveDocumentFromWorkspaceResult(
            RemoveDocumentFromWorkspaceResultStatus.Success,
            updatedWorkspace,
            "Document removed from the workspace.");
    }

    private static Workspace CreateUpdatedWorkspace(
        Workspace workspace)
    {
        return Workspace.Restore(
            workspace.Id,
            workspace.Name,
            workspace.Description,
            workspace.TypeOfWorkspace,
            workspace.Memberships,
            workspace.LastActiveDocumentId,
            workspace.LastUpdated);
    }
}
