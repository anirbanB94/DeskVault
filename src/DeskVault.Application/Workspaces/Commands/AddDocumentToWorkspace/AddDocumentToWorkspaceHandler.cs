using DeskVault.Application.Interfaces;
using DeskVault.Domain.Workspaces;
using DeskVault.Shared.Resources;
using Microsoft.Extensions.Logging;

namespace DeskVault.Application.Workspaces.Commands.AddDocumentToWorkspace;

public sealed class AddDocumentToWorkspaceHandler
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IActiveWorkspaceRegistry _activeWorkspaceRegistry;
    private readonly ILogger<AddDocumentToWorkspaceHandler> _logger;

    public AddDocumentToWorkspaceHandler(
        IDocumentRepository documentRepository,
        IWorkspaceRepository workspaceRepository,
        IActiveWorkspaceRegistry activeWorkspaceRegistry,
        ILogger<AddDocumentToWorkspaceHandler> logger)
    {
        _documentRepository = documentRepository;
        _workspaceRepository = workspaceRepository;
        _activeWorkspaceRegistry = activeWorkspaceRegistry;
        _logger = logger;
    }

    public async Task<AddDocumentToWorkspaceResult> HandleAsync(
        AddDocumentToWorkspaceCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        Workspace? workspace =
            _activeWorkspaceRegistry.Get(command.WorkspaceId);

        if (workspace is null)
        {
            _logger.LogDebug(
                LogMessages.WorkspaceDocumentAdditionWorkspaceNotFound);

            return new AddDocumentToWorkspaceResult(
                AddDocumentToWorkspaceResultStatus.WorkspaceNotFound,
                null,
                "The workspace is not active.");
        }

        if (workspace.Memberships.Any(
                membership => membership.DocumentId == command.DocumentId))
        {
            _logger.LogDebug(
                LogMessages.WorkspaceDocumentAdditionAlreadyMember);

            return new AddDocumentToWorkspaceResult(
                AddDocumentToWorkspaceResultStatus.AlreadyMember,
                workspace,
                "The document is already a member of the workspace.");
        }

        var document =
            await _documentRepository.GetByIdAsync(
                command.DocumentId,
                cancellationToken);

        if (document is null)
        {
            _logger.LogWarning(
                LogMessages.WorkspaceDocumentAdditionDocumentNotFound);

            return new AddDocumentToWorkspaceResult(
                AddDocumentToWorkspaceResultStatus.DocumentNotFound,
                workspace,
                "The document could not be found.");
        }

        if (workspace.TypeOfWorkspace == WorkspaceType.Temporary)
        {
            workspace.AddDocument(command.DocumentId);

            _logger.LogInformation(
                LogMessages.WorkspaceDocumentAdditionCompleted);

            return new AddDocumentToWorkspaceResult(
                AddDocumentToWorkspaceResultStatus.Success,
                workspace,
                "Document added to the workspace.");
        }

        Workspace updatedWorkspace = CreateUpdatedWorkspace(workspace);
        updatedWorkspace.AddDocument(command.DocumentId);

        await _workspaceRepository.UpdateAsync(
            updatedWorkspace,
            cancellationToken);

        _activeWorkspaceRegistry.Replace(updatedWorkspace);

        _logger.LogInformation(
            LogMessages.WorkspaceDocumentAdditionCompleted);

        return new AddDocumentToWorkspaceResult(
            AddDocumentToWorkspaceResultStatus.Success,
            updatedWorkspace,
            "Document added to the workspace.");
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
