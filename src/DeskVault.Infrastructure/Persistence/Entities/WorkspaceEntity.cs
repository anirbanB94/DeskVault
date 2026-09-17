namespace DeskVault.Infrastructure.Persistence.Entities;

public sealed class WorkspaceEntity
{
    public Guid Id { get; set; }

    public string? Name { get; set; }

    public int Type { get; set; }

    public Guid? LastActiveDocumentId { get; set; }
}
