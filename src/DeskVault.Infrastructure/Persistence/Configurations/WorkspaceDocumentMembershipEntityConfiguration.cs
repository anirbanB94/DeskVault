using DeskVault.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeskVault.Infrastructure.Persistence.Configurations;

public sealed class WorkspaceDocumentMembershipEntityConfiguration
    : IEntityTypeConfiguration<WorkspaceDocumentMembershipEntity>
{
    public void Configure(
        EntityTypeBuilder<WorkspaceDocumentMembershipEntity> builder)
    {
        builder.ToTable("WorkspaceDocumentMemberships");

        builder.HasKey(
            membership => new
            {
                membership.WorkspaceId,
                membership.DocumentId
            });

        builder.Property(
                membership => membership.WorkspaceId)
            .ValueGeneratedNever();

        builder.Property(
                membership => membership.DocumentId)
            .ValueGeneratedNever();

        builder.Property(
                membership => membership.Order)
            .IsRequired();

        builder.HasIndex(
                membership => new
                {
                    membership.WorkspaceId,
                    membership.Order
                })
            .IsUnique();

        builder.HasOne<WorkspaceEntity>()
            .WithMany()
            .HasForeignKey(
                membership => membership.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder.HasOne<DocumentEntity>()
            .WithMany()
            .HasForeignKey(
                membership => membership.DocumentId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
    }
}
