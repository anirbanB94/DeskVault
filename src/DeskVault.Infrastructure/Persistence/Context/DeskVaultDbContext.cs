using DeskVault.Infrastructure.Persistence.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DeskVault.Infrastructure.Persistence.Context;

public sealed class DeskVaultDbContext : DbContext
{
    public DeskVaultDbContext(
        DbContextOptions<DeskVaultDbContext> options)
        : base(options)
    {
        if (Database.GetDbConnection() is SqliteConnection connection)
        {
            connection.CreateFunction<string, string, bool>(
                SqliteSearchFunctions.ContainsCanonicalizedFunctionName,
                SqliteSearchFunctions.ContainsCanonicalized,
                isDeterministic: true);
        }
    }

    public DbSet<DocumentEntity> Documents =>
        Set<DocumentEntity>();

    public DbSet<DocumentKnowledgeAvailabilityEntity>
        DocumentKnowledgeAvailabilities =>
        Set<DocumentKnowledgeAvailabilityEntity>();

    public DbSet<DocumentChunkEntity> DocumentChunks =>
        Set<DocumentChunkEntity>();

    public DbSet<WorkspaceEntity> Workspaces =>
        Set<WorkspaceEntity>();

    public DbSet<WorkspaceDocumentMembershipEntity> WorkspaceDocumentMemberships =>
        Set<WorkspaceDocumentMembershipEntity>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(DeskVaultDbContext).Assembly);

        modelBuilder
            .HasDbFunction(
                typeof(SqliteSearchFunctions).GetMethod(
                    nameof(
                        SqliteSearchFunctions.ContainsCanonicalized),
                    [typeof(string), typeof(string)])!)
            .HasName(
                SqliteSearchFunctions.ContainsCanonicalizedFunctionName);

        base.OnModelCreating(modelBuilder);
    }
}
