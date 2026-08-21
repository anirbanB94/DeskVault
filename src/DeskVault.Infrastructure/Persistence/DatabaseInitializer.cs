using DeskVault.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace DeskVault.Infrastructure.Persistence;

public sealed class DatabaseInitializer
{
    private const string InitialMigrationId =
        "20260821094306_InitialCreate";

    private const string EfCoreProductVersion =
        "10.0.10";

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

        if (!await dbContext.Database.CanConnectAsync(
                cancellationToken))
        {
            await dbContext.Database.MigrateAsync(
                cancellationToken);

            return;
        }

        var migrationsTableExists =
            await HasMigrationsHistoryTableAsync(
                dbContext,
                cancellationToken);

        if (!migrationsTableExists)
        {
            await CreateMigrationsHistoryTableAsync(
                dbContext,
                cancellationToken);

            await RecordInitialMigrationAsync(
                dbContext,
                cancellationToken);
        }

        await dbContext.Database.MigrateAsync(
            cancellationToken);
    }

    private static async Task<bool> HasMigrationsHistoryTableAsync(
        DeskVaultDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var connection =
            dbContext.Database.GetDbConnection();

        await connection.OpenAsync(
            cancellationToken);

        await using var command =
            connection.CreateCommand();

        command.CommandText =
            """
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE type = 'table'
              AND name = '__EFMigrationsHistory';
            """;

        var result =
            await command.ExecuteScalarAsync(
                cancellationToken);

        return Convert.ToInt32(result) > 0;
    }

    private static async Task CreateMigrationsHistoryTableAsync(
        DeskVaultDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE "__EFMigrationsHistory" (
                "MigrationId" TEXT NOT NULL
                    CONSTRAINT "PK___EFMigrationsHistory"
                    PRIMARY KEY,
                "ProductVersion" TEXT NOT NULL
            );
            """,
            cancellationToken);
    }

    private static async Task RecordInitialMigrationAsync(
        DeskVaultDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO "__EFMigrationsHistory"
                ("MigrationId", "ProductVersion")
            VALUES
                ({InitialMigrationId}, {EfCoreProductVersion});
            """,
            cancellationToken);
    }
}
