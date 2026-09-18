using DeskVault.Application.Interfaces;
using DeskVault.Domain.Workspaces;

namespace DeskVault.Application.Workspaces.Queries.GetWorkspace;

public sealed class GetWorkspaceHandler
{
    private readonly IWorkspaceRepository _workspaceRepository;

    public GetWorkspaceHandler(
        IWorkspaceRepository workspaceRepository)
    {
        _workspaceRepository = workspaceRepository;
    }

    public async Task<GetWorkspaceResult> HandleAsync(
        GetWorkspaceQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        Workspace? workspace =
            await _workspaceRepository.GetByIdAsync(
                query.WorkspaceId,
                cancellationToken);

        if (workspace is null)
        {
            return new GetWorkspaceResult(
                GetWorkspaceResultStatus.WorkspaceNotFound,
                null,
                "The workspace could not be found.");
        }

        return new GetWorkspaceResult(
            GetWorkspaceResultStatus.Success,
            workspace,
            "The workspace was retrieved.");
    }
}
