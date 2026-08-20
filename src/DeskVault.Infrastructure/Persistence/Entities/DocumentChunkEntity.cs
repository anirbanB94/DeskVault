namespace DeskVault.Infrastructure.Persistence.Entities;

public sealed class DocumentChunkEntity
{
    public Guid Id { get; set; }

    public Guid DocumentId { get; set; }

    public int Order { get; set; }

    public string Text { get; set; } = string.Empty;
}
