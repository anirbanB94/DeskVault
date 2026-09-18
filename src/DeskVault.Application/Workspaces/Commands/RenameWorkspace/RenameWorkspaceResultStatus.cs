namespace DeskVault.Application.Workspaces.Commands.RenameWorkspace;

public enum RenameWorkspaceResultStatus
{
    Success = 0,
    WorkspaceNotFound = 1,
    WorkspaceNotPersistent = 2,
    NameRequired = 3
}
