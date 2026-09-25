using DeskVault.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeskVault.Infrastructure.Persistence.Configurations;

public sealed class WorkspaceEntityConfiguration
    : IEntityTypeConfiguration<WorkspaceEntity>
{
    public void Configure(
        EntityTypeBuilder<WorkspaceEntity> builder)
    {
        builder.ToTable("Workspaces");

        builder.HasKey(
            workspace => workspace.Id);

        builder.Property(
                workspace => workspace.Id)
            .ValueGeneratedNever();

        builder.Property(
                workspace => workspace.Name)
            .HasMaxLength(260);

        builder.Property(
                workspace => workspace.Description)
            .HasMaxLength(400);

        builder.Property(
                workspace => workspace.Type)
            .IsRequired();

        builder.Property(
                workspace => workspace.LastActiveDocumentId);

        builder.Property(
                workspace => workspace.LastUpdated)
            .IsRequired();
    }
}
