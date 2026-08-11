using DeskVault.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeskVault.Infrastructure.Persistence.Configurations;

public sealed class DocumentEntityConfiguration
    : IEntityTypeConfiguration<DocumentEntity>
{
    public void Configure(
        EntityTypeBuilder<DocumentEntity> builder)
    {
        builder.ToTable("Documents");

        builder.HasKey(
            document => document.Id);

        builder.Property(
                document => document.Id)
            .ValueGeneratedNever();

        builder.Property(
                document => document.FileName)
            .IsRequired()
            .HasMaxLength(260);

        builder.Property(
                document => document.DisplayName)
            .IsRequired()
            .HasMaxLength(260);

        builder.Property(
                document => document.Sha256Hash)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(
                document => document.ImportedAt)
            .IsRequired();

        builder.Property(
                document => document.Status)
            .IsRequired();

        builder.Property(
                document => document.StoredFilePath)
            .IsRequired()
            .HasMaxLength(1024);

        builder.HasIndex(
                document => document.Sha256Hash)
            .IsUnique();

        builder.HasIndex(
                document => document.ImportedAt);
    }
}
