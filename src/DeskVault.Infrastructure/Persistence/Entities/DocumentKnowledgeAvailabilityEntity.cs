namespace DeskVault.Infrastructure.Persistence.Entities;

public sealed class DocumentKnowledgeAvailabilityEntity
{
    public Guid DocumentId { get; set; }

    public int Representation { get; set; }

    public int State { get; set; }

    public long? LastAvailableProcessingGeneration { get; set; }
}
