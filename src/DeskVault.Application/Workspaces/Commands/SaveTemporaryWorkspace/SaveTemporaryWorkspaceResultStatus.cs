namespace DeskVault.Application.Workspaces.Commands.SaveTemporaryWorkspace;

public enum SaveTemporaryWorkspaceResultStatus
{
    Success = 0,
    WorkspaceNotFound = 1,
    WorkspaceNotTemporary = 2,
    NameRequired = 3
}
