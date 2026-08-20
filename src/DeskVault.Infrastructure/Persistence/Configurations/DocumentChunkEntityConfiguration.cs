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
        builder.ToTable("DocumentChunks");

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
