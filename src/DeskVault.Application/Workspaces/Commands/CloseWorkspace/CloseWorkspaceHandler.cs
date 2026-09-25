using DeskVault.Application.Interfaces;
using DeskVault.Domain.Workspaces;
using DeskVault.Shared.Resources;
using Microsoft.Extensions.Logging;

namespace DeskVault.Application.Workspaces.Commands.CloseWorkspace;

public sealed class CloseWorkspaceHandler
{
    private readonly IActiveWorkspaceRegistry _activeWorkspaceRegistry;
    private readonly ILogger<CloseWorkspaceHandler> _logger;

    public CloseWorkspaceHandler(
        IActiveWorkspaceRegistry activeWorkspaceRegistry,
        ILogger<CloseWorkspaceHandler> logger)
    {
        _activeWorkspaceRegistry = activeWorkspaceRegistry;
        _logger = logger;
    }

    public Task<CloseWorkspaceResult> HandleAsync(
        CloseWorkspaceCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        Workspace? workspace =
            _activeWorkspaceRegistry.Get(command.WorkspaceId);

        if (workspace is null)
        {
            _logger.LogDebug(
                LogMessages.WorkspaceCloseNotFound);

            return Task.FromResult(
                new CloseWorkspaceResult(
                    CloseWorkspaceResultStatus.WorkspaceNotFound,
                    null,
                    "The workspace is not active."));
        }

        _activeWorkspaceRegistry.Remove(workspace.Id);

        _logger.LogInformation(
            LogMessages.WorkspaceCloseCompleted);

        return Task.FromResult(
            new CloseWorkspaceResult(
                CloseWorkspaceResultStatus.Success,
                workspace,
                "The workspace was closed."));
    }
}
