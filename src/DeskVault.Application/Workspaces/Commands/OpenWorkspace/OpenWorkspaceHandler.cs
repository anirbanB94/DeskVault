using DeskVault.Application.Interfaces;
using DeskVault.Domain.Workspaces;

namespace DeskVault.Application.Workspaces.Commands.OpenWorkspace;

public sealed class OpenWorkspaceHandler
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IActiveWorkspaceRegistry _activeWorkspaceRegistry;

    public OpenWorkspaceHandler(
        IDocumentRepository documentRepository,
        IWorkspaceRepository workspaceRepository,
        IActiveWorkspaceRegistry activeWorkspaceRegistry)
    {
        _documentRepository = documentRepository;
        _workspaceRepository = workspaceRepository;
        _activeWorkspaceRegistry = activeWorkspaceRegistry;
    }

    public async Task<OpenWorkspaceResult> HandleAsync(
        OpenWorkspaceCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        Workspace? activeWorkspace =
            _activeWorkspaceRegistry.Get(command.WorkspaceId);

        if (activeWorkspace is not null)
        {
            return new OpenWorkspaceResult(
                OpenWorkspaceResultStatus.AlreadyActive,
                activeWorkspace,
                [],
                "The workspace is already active.");
        }

        Workspace? workspace =
            await _workspaceRepository.GetByIdAsync(
                command.WorkspaceId,
                cancellationToken);

        if (workspace is null)
        {
            return new OpenWorkspaceResult(
                OpenWorkspaceResultStatus.WorkspaceNotFound,
                null,
                [],
                "The workspace could not be found.");
        }

        var validMemberships = new List<WorkspaceDocumentMembership>();
        var missingDocumentIds = new List<Guid>();

        foreach (WorkspaceDocumentMembership membership in
                 workspace.Memberships.OrderBy(
                     membership => membership.Order))
        {
            var document =
                await _documentRepository.GetByIdAsync(
                    membership.DocumentId,
                    cancellationToken);

            if (document is null)
            {
                missingDocumentIds.Add(membership.DocumentId);
                continue;
            }

            validMemberships.Add(membership);
        }

        Guid? lastActiveDocumentId =
            workspace.LastActiveDocumentId;

        if (lastActiveDocumentId.HasValue &&
            !validMemberships.Any(
                membership =>
                    membership.DocumentId ==
                    lastActiveDocumentId.Value))
        {
            lastActiveDocumentId = null;
        }

        Workspace restoredWorkspace =
            Workspace.Restore(
                workspace.Id,
                workspace.Name,
                workspace.TypeOfWorkspace,
                validMemberships,
                lastActiveDocumentId);

        _activeWorkspaceRegistry.Add(restoredWorkspace);

        return new OpenWorkspaceResult(
            OpenWorkspaceResultStatus.Activated,
            restoredWorkspace,
            missingDocumentIds,
            missingDocumentIds.Count == 0
                ? "The workspace was opened."
                : "The workspace was opened with missing documents.");
    }
}
