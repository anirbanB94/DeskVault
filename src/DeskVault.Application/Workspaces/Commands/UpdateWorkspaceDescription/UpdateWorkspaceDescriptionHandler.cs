using DeskVault.Application.Interfaces;
using DeskVault.Domain.Workspaces;
using DeskVault.Shared.Resources;
using Microsoft.Extensions.Logging;

namespace DeskVault.Application.Workspaces.Commands.UpdateWorkspaceDescription;

public sealed class UpdateWorkspaceDescriptionHandler
{
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IActiveWorkspaceRegistry _activeWorkspaceRegistry;
    private readonly ILogger<UpdateWorkspaceDescriptionHandler> _logger;

    public UpdateWorkspaceDescriptionHandler(
        IWorkspaceRepository workspaceRepository,
        IActiveWorkspaceRegistry activeWorkspaceRegistry,
        ILogger<UpdateWorkspaceDescriptionHandler> logger)
    {
        _workspaceRepository = workspaceRepository;
        _activeWorkspaceRegistry = activeWorkspaceRegistry;
        _logger = logger;
    }

    public async Task<UpdateWorkspaceDescriptionResult> HandleAsync(
        UpdateWorkspaceDescriptionCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        Workspace? workspace =
            _activeWorkspaceRegistry.Get(command.WorkspaceId);

        if (workspace is null)
        {
            _logger.LogDebug(
                LogMessages.WorkspaceDescriptionUpdateWorkspaceNotFound);

            return new UpdateWorkspaceDescriptionResult(
                UpdateWorkspaceDescriptionResultStatus.WorkspaceNotFound,
                null,
                "The workspace is not active.");
        }

        if (workspace.TypeOfWorkspace != WorkspaceType.Persistent)
        {
            _logger.LogDebug(
                LogMessages.WorkspaceDescriptionUpdateNotPersistent);

            return new UpdateWorkspaceDescriptionResult(
                UpdateWorkspaceDescriptionResultStatus.WorkspaceNotPersistent,
                workspace,
                "Only persistent workspaces can have a description.");
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

        updatedWorkspace.ChangeDescription(
            command.Description);

        await _workspaceRepository.UpdateAsync(
            updatedWorkspace,
            cancellationToken);

        _activeWorkspaceRegistry.Replace(
            updatedWorkspace);

        _logger.LogInformation(
            LogMessages.WorkspaceDescriptionUpdateCompleted);

        return new UpdateWorkspaceDescriptionResult(
            UpdateWorkspaceDescriptionResultStatus.Success,
            updatedWorkspace,
            "The workspace description was updated.");
    }
}
