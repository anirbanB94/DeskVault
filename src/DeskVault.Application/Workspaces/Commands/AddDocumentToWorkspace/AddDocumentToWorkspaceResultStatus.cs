namespace DeskVault.Application.Workspaces.Commands.AddDocumentToWorkspace;

public enum AddDocumentToWorkspaceResultStatus
{
    Success = 0,
    WorkspaceNotFound = 1,
    DocumentNotFound = 2,
    AlreadyMember = 3
}
