using DeskVault.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace DeskVault.Infrastructure.Persistence;

public sealed class DatabaseInitializer
{
    private readonly IDbContextFactory<DeskVaultDbContext> _dbContextFactory;

    public DatabaseInitializer(
        IDbContextFactory<DeskVaultDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task InitializeAsync(
        CancellationToken cancellationToken = default)
    {
        await using var dbContext =
            await _dbContextFactory.CreateDbContextAsync(
                cancellationToken);

        await dbContext.Database.EnsureCreatedAsync(
            cancellationToken);
    }
}
