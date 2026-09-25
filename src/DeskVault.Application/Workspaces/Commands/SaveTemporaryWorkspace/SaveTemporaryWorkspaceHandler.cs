using DeskVault.Application.Interfaces;
using DeskVault.Domain.Workspaces;
using DeskVault.Shared.Resources;
using Microsoft.Extensions.Logging;

namespace DeskVault.Application.Workspaces.Commands.SaveTemporaryWorkspace;

public sealed class SaveTemporaryWorkspaceHandler
{
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IActiveWorkspaceRegistry _activeWorkspaceRegistry;
    private readonly ILogger<SaveTemporaryWorkspaceHandler> _logger;

    public SaveTemporaryWorkspaceHandler(
        IWorkspaceRepository workspaceRepository,
        IActiveWorkspaceRegistry activeWorkspaceRegistry,
        ILogger<SaveTemporaryWorkspaceHandler> logger)
    {
        _workspaceRepository = workspaceRepository;
        _activeWorkspaceRegistry = activeWorkspaceRegistry;
        _logger = logger;
    }

    public async Task<SaveTemporaryWorkspaceResult> HandleAsync(
        SaveTemporaryWorkspaceCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        Workspace? workspace =
            _activeWorkspaceRegistry.Get(command.WorkspaceId);

        if (workspace is null)
        {
            _logger.LogDebug(
                LogMessages.WorkspaceSaveTemporaryNotFound);

            return new SaveTemporaryWorkspaceResult(
                SaveTemporaryWorkspaceResultStatus.WorkspaceNotFound,
                null,
                "The workspace is not active.");
        }

        if (workspace.TypeOfWorkspace != WorkspaceType.Temporary)
        {
            _logger.LogDebug(
                LogMessages.WorkspaceSaveTemporaryNotTemporary);

            return new SaveTemporaryWorkspaceResult(
                SaveTemporaryWorkspaceResultStatus.WorkspaceNotTemporary,
                workspace,
                "Only temporary workspaces can be saved as persistent.");
        }

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            _logger.LogDebug(
                LogMessages.WorkspaceSaveTemporaryNameRequired);

            return new SaveTemporaryWorkspaceResult(
                SaveTemporaryWorkspaceResultStatus.NameRequired,
                workspace,
                "A workspace name is required.");
        }

        Workspace persistentWorkspace =
            Workspace.Restore(
                workspace.Id,
                command.Name,
                command.Description,
                WorkspaceType.Persistent,
                workspace.Memberships,
                workspace.LastActiveDocumentId,
                workspace.LastUpdated);

        await _workspaceRepository.AddAsync(
            persistentWorkspace,
            cancellationToken);

        _activeWorkspaceRegistry.Replace(
            persistentWorkspace);

        _logger.LogInformation(
            LogMessages.WorkspaceSaveTemporaryCompleted);

        return new SaveTemporaryWorkspaceResult(
            SaveTemporaryWorkspaceResultStatus.Success,
            persistentWorkspace,
            "The temporary workspace was saved as persistent.");
    }
}
