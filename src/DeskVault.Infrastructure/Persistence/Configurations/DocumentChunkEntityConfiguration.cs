using DeskVault.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeskVault.Infrastructure.Persistence.Configurations;

public sealed class DocumentChunkEntityConfiguration
    : IEntityTypeConfiguration<DocumentChunkEntity>
{
    public void Configure(
        EntityTypeBuilder<DocumentChunkEntity> builder)
    {
        builder.ToTable(
            "DocumentChunks",
            tableBuilder =>
                tableBuilder.HasCheckConstraint(
                    "CK_DocumentChunks_SourceLocationRange",
                    """
                    ("SourceLocationStartLine" IS NULL
                        AND "SourceLocationEndLine" IS NULL)
                    OR
                    ("SourceLocationStartLine" IS NOT NULL
                        AND "SourceLocationEndLine" IS NOT NULL
                        AND "SourceLocationStartLine" > 0
                        AND "SourceLocationEndLine" >= "SourceLocationStartLine")
                    """));

        builder.HasKey(
            chunk => chunk.Id);

        builder.Property(
                chunk => chunk.Id)
            .ValueGeneratedNever();

        builder.Property(
                chunk => chunk.DocumentId)
            .IsRequired();

        builder.Property(
                chunk => chunk.Order)
            .IsRequired();

        builder.Property(
                chunk => chunk.Text)
            .IsRequired();

        builder.Property(
                chunk => chunk.ContentHash)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(
                chunk => chunk.ProcessingGeneration)
            .IsRequired();

        builder.Property(
                chunk => chunk.SourceLocationStartLine)
            .IsRequired(false);

        builder.Property(
                chunk => chunk.SourceLocationEndLine)
            .IsRequired(false);

        builder.HasOne<DocumentEntity>()
            .WithMany()
            .HasForeignKey(
                chunk => chunk.DocumentId)
            .OnDelete(
                DeleteBehavior.Cascade);

        builder.HasIndex(
                chunk => new
                {
                    chunk.DocumentId,
                    chunk.Order
                })
            .IsUnique();

        builder.HasIndex(
            chunk => chunk.DocumentId);
    }
}
