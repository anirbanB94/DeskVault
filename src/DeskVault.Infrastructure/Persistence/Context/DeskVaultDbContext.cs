using DeskVault.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeskVault.Infrastructure.Persistence.Context;

public sealed class DeskVaultDbContext : DbContext
{
    public DeskVaultDbContext(
        DbContextOptions<DeskVaultDbContext> options)
        : base(options)
    {
    }

    public DbSet<DocumentEntity> Documents =>
        Set<DocumentEntity>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(DeskVaultDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
