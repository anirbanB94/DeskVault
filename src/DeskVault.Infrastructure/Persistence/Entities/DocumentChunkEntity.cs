namespace DeskVault.Infrastructure.Persistence.Entities;

public sealed class DocumentChunkEntity
{
    public Guid Id { get; set; }

    public Guid DocumentId { get; set; }

    public int Order { get; set; }

    public string Text { get; set; } = string.Empty;

    public string ContentHash { get; set; } = string.Empty;

    public long ProcessingGeneration { get; set; }

    public string? ChunkingRuleVersion { get; set; }

    public int? SourceLocationStartLine { get; set; }

    public int? SourceLocationEndLine { get; set; }
}
