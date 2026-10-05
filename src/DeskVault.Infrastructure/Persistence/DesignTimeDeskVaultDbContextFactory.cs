using DeskVault.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DeskVault.Infrastructure.Persistence;

public sealed class DesignTimeDeskVaultDbContextFactory
    : IDesignTimeDbContextFactory<DeskVaultDbContext>
{
    public DeskVaultDbContext CreateDbContext(
        string[] args)
    {
        var optionsBuilder =
            new DbContextOptionsBuilder<DeskVaultDbContext>();

        optionsBuilder.UseSqlite(
            "Data Source=designtime-deskvault.db;Pooling=False");

        return new DeskVaultDbContext(
            optionsBuilder.Options);
    }
}
