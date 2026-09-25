using DeskVault.Application.Interfaces;
using DeskVault.Domain.Workspaces;
using DeskVault.Shared.Resources;
using Microsoft.Extensions.Logging;

namespace DeskVault.Application.Workspaces.Commands.RenameWorkspace;

public sealed class RenameWorkspaceHandler
{
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IActiveWorkspaceRegistry _activeWorkspaceRegistry;
    private readonly ILogger<RenameWorkspaceHandler> _logger;

    public RenameWorkspaceHandler(
        IWorkspaceRepository workspaceRepository,
        IActiveWorkspaceRegistry activeWorkspaceRegistry,
        ILogger<RenameWorkspaceHandler> logger)
    {
        _workspaceRepository = workspaceRepository;
        _activeWorkspaceRegistry = activeWorkspaceRegistry;
        _logger = logger;
    }

    public async Task<RenameWorkspaceResult> HandleAsync(
        RenameWorkspaceCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        Workspace? workspace =
            _activeWorkspaceRegistry.Get(command.WorkspaceId);

        if (workspace is null)
        {
            _logger.LogDebug(
                LogMessages.WorkspaceRenameWorkspaceNotFound);

            return new RenameWorkspaceResult(
                RenameWorkspaceResultStatus.WorkspaceNotFound,
                null,
                "The workspace is not active.");
        }

        if (workspace.TypeOfWorkspace != WorkspaceType.Persistent)
        {
            _logger.LogDebug(
                LogMessages.WorkspaceRenameNotPersistent);

            return new RenameWorkspaceResult(
                RenameWorkspaceResultStatus.WorkspaceNotPersistent,
                workspace,
                "Only persistent workspaces can be renamed.");
        }

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            _logger.LogDebug(
                LogMessages.WorkspaceRenameNameRequired);

            return new RenameWorkspaceResult(
                RenameWorkspaceResultStatus.NameRequired,
                workspace,
                "A workspace name is required.");
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

        updatedWorkspace.Rename(command.Name);

        await _workspaceRepository.UpdateAsync(
            updatedWorkspace,
            cancellationToken);

        _activeWorkspaceRegistry.Replace(
            updatedWorkspace);

        _logger.LogInformation(
            LogMessages.WorkspaceRenameCompleted);

        return new RenameWorkspaceResult(
            RenameWorkspaceResultStatus.Success,
            updatedWorkspace,
            "The workspace was renamed.");
    }
}
