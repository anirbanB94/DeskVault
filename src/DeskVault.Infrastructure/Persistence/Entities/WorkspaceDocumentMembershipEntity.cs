namespace DeskVault.Infrastructure.Persistence.Entities;

public sealed class WorkspaceDocumentMembershipEntity
{
    public Guid WorkspaceId { get; set; }

    public Guid DocumentId { get; set; }

    public int Order { get; set; }
}
