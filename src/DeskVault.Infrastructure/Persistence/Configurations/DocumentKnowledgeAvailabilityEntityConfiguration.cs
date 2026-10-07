using DeskVault.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeskVault.Infrastructure.Persistence.Configurations;

public sealed class DocumentKnowledgeAvailabilityEntityConfiguration
    : IEntityTypeConfiguration<DocumentKnowledgeAvailabilityEntity>
{
    public void Configure(
        EntityTypeBuilder<DocumentKnowledgeAvailabilityEntity> builder)
    {
        builder.ToTable(
            "DocumentKnowledgeAvailabilities",
            tableBuilder =>
                tableBuilder.HasCheckConstraint(
                    "CK_DocumentKnowledgeAvailabilities_LastAvailableProcessingGeneration_NonNegative",
                    """
                    "LastAvailableProcessingGeneration" IS NULL
                    OR
                    "LastAvailableProcessingGeneration" >= 0
                    """));

        builder.HasKey(
            availability =>
                new
                {
                    availability.DocumentId,
                    availability.Representation
                });

        builder.Property(
                availability => availability.DocumentId)
            .ValueGeneratedNever();

        builder.Property(
                availability => availability.Representation)
            .IsRequired();

        builder.Property(
                availability => availability.State)
            .IsRequired();

        builder.Property(
                availability =>
                    availability.LastAvailableProcessingGeneration)
            .IsRequired(false);

        builder.HasOne<DocumentEntity>()
            .WithMany()
            .HasForeignKey(
                availability =>
                    availability.DocumentId)
            .OnDelete(
                DeleteBehavior.Cascade);
    }
}
