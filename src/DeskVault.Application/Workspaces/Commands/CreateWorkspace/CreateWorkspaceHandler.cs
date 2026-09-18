using DeskVault.Application.Interfaces;
using DeskVault.Domain.Workspaces;

namespace DeskVault.Application.Workspaces.Commands.CreateWorkspace;

public sealed class CreateWorkspaceHandler
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IActiveWorkspaceRegistry _activeWorkspaceRegistry;

    public CreateWorkspaceHandler(
        IDocumentRepository documentRepository,
        IWorkspaceRepository workspaceRepository,
        IActiveWorkspaceRegistry activeWorkspaceRegistry)
    {
        _documentRepository = documentRepository;
        _workspaceRepository = workspaceRepository;
        _activeWorkspaceRegistry = activeWorkspaceRegistry;
    }

    public async Task<CreateWorkspaceResult> HandleAsync(
        CreateWorkspaceCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        IReadOnlyList<Guid> documentIds =
            command.DocumentIds?
                .Distinct()
                .ToList()
            ?? [];

        var missingDocumentIds = new List<Guid>();

        foreach (Guid documentId in documentIds)
        {
            var document =
                await _documentRepository.GetByIdAsync(
                    documentId,
                    cancellationToken);

            if (document is null)
            {
                missingDocumentIds.Add(documentId);
            }
        }

        if (missingDocumentIds.Count > 0)
        {
            return new CreateWorkspaceResult(
                CreateWorkspaceResultStatus.DocumentNotFound,
                null,
                missingDocumentIds,
                "One or more requested documents could not be found.");
        }

        Workspace workspace = command.IsPersistent
            ? Workspace.CreatePersistent(
                Guid.NewGuid(),
                command.Name ?? string.Empty)
            : Workspace.CreateTemporary(Guid.NewGuid());

        foreach (Guid documentId in documentIds)
        {
            workspace.AddDocument(documentId);
        }

        if (workspace.TypeOfWorkspace ==
            WorkspaceType.Persistent)
        {
            await _workspaceRepository.AddAsync(
                workspace,
                cancellationToken);
        }

        _activeWorkspaceRegistry.Add(workspace);

        return new CreateWorkspaceResult(
            CreateWorkspaceResultStatus.Success,
            workspace,
            [],
            "Workspace created successfully.");
    }
}
