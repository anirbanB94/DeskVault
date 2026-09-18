using DeskVault.Application.Interfaces;
using DeskVault.Domain.Workspaces;

namespace DeskVault.Application.Workspaces.Queries.GetWorkspaces;

public sealed class GetWorkspacesHandler
{
    private readonly IWorkspaceRepository _workspaceRepository;

    public GetWorkspacesHandler(
        IWorkspaceRepository workspaceRepository)
    {
        _workspaceRepository = workspaceRepository;
    }

    public async Task<GetWorkspacesResult> HandleAsync(
        GetWorkspacesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        IReadOnlyList<Workspace> workspaces =
            await _workspaceRepository.GetAllAsync(
                cancellationToken);

        return new GetWorkspacesResult(
            GetWorkspacesResultStatus.Success,
            workspaces,
            "Workspaces were retrieved.");
    }
}
