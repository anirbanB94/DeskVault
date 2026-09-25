using DeskVault.Application.Interfaces;
using DeskVault.Domain.Workspaces;
using DeskVault.Shared.Resources;
using Microsoft.Extensions.Logging;

namespace DeskVault.Application.Workspaces.Commands.ActivateWorkspace;

public sealed class ActivateWorkspaceHandler
{
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IActiveWorkspaceRegistry _activeWorkspaceRegistry;
    private readonly ILogger<ActivateWorkspaceHandler> _logger;

    public ActivateWorkspaceHandler(
        IWorkspaceRepository workspaceRepository,
        IActiveWorkspaceRegistry activeWorkspaceRegistry,
        ILogger<ActivateWorkspaceHandler> logger)
    {
        _workspaceRepository = workspaceRepository;
        _activeWorkspaceRegistry = activeWorkspaceRegistry;
        _logger = logger;
    }

    public async Task<ActivateWorkspaceResult> HandleAsync(
        ActivateWorkspaceCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        Workspace? activeWorkspace =
            _activeWorkspaceRegistry.Get(command.WorkspaceId);

        if (activeWorkspace is not null)
        {
            _logger.LogDebug(
                LogMessages.WorkspaceActivationAlreadyActive);

            return new ActivateWorkspaceResult(
                ActivateWorkspaceResultStatus.AlreadyActive,
                activeWorkspace,
                "The workspace is already active.");
        }

        Workspace? workspace =
            await _workspaceRepository.GetByIdAsync(
                command.WorkspaceId,
                cancellationToken);

        if (workspace is null)
        {
            _logger.LogWarning(
                LogMessages.WorkspaceActivationNotFound);

            return new ActivateWorkspaceResult(
                ActivateWorkspaceResultStatus.WorkspaceNotFound,
                null,
                "The workspace could not be found.");
        }

        _activeWorkspaceRegistry.Add(workspace);

        _logger.LogInformation(
            LogMessages.WorkspaceActivationCompleted);

        return new ActivateWorkspaceResult(
            ActivateWorkspaceResultStatus.Activated,
            workspace,
            "The workspace was activated.");
    }
}
