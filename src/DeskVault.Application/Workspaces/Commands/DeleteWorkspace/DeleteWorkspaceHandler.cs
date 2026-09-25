using DeskVault.Application.Interfaces;
using DeskVault.Domain.Workspaces;
using DeskVault.Shared.Resources;
using Microsoft.Extensions.Logging;

namespace DeskVault.Application.Workspaces.Commands.DeleteWorkspace;

public sealed class DeleteWorkspaceHandler
{
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IActiveWorkspaceRegistry _activeWorkspaceRegistry;
    private readonly ILogger<DeleteWorkspaceHandler> _logger;

    public DeleteWorkspaceHandler(
        IWorkspaceRepository workspaceRepository,
        IActiveWorkspaceRegistry activeWorkspaceRegistry,
        ILogger<DeleteWorkspaceHandler> logger)
    {
        _workspaceRepository = workspaceRepository;
        _activeWorkspaceRegistry = activeWorkspaceRegistry;
        _logger = logger;
    }

    public async Task<DeleteWorkspaceResult> HandleAsync(
        DeleteWorkspaceCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        Workspace? workspace =
            _activeWorkspaceRegistry.Get(command.WorkspaceId);

        if (workspace is null)
        {
            _logger.LogDebug(
                LogMessages.WorkspaceDeletionNotFound);

            return new DeleteWorkspaceResult(
                DeleteWorkspaceResultStatus.WorkspaceNotFound,
                null,
                "The workspace is not active.");
        }

        if (workspace.TypeOfWorkspace ==
            WorkspaceType.Persistent)
        {
            await _workspaceRepository.DeleteAsync(
                workspace.Id,
                cancellationToken);
        }

        _activeWorkspaceRegistry.Remove(workspace.Id);

        _logger.LogInformation(
            LogMessages.WorkspaceDeletionCompleted);

        return new DeleteWorkspaceResult(
            DeleteWorkspaceResultStatus.Success,
            workspace,
            "The workspace was deleted.");
    }
}
