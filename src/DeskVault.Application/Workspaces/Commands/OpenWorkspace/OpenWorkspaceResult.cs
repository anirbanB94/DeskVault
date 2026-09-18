using DeskVault.Domain.Workspaces;

namespace DeskVault.Application.Workspaces.Commands.OpenWorkspace;

public sealed record OpenWorkspaceResult(
    OpenWorkspaceResultStatus Status,
    Workspace? Workspace,
    IReadOnlyCollection<Guid> MissingDocumentIds,
    string Description);
