using DeskVault.Domain.Workspaces;

namespace DeskVault.Application.Workspaces.Commands.CreateWorkspace;

public sealed record CreateWorkspaceResult(
    CreateWorkspaceResultStatus Status,
    Workspace? Workspace,
    IReadOnlyCollection<Guid> MissingDocumentIds,
    string Description);
