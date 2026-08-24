using DeskVault.Infrastructure.Persistence.Context;
using DeskVault.Shared.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DeskVault.Infrastructure.Persistence;

public sealed class DatabaseInitializer
{
    private const string InitialMigrationId =
        "20260821094306_InitialCreate";

    private const string EfCoreProductVersion =
        "10.0.10";

    private readonly IDbContextFactory<DeskVaultDbContext> _dbContextFactory;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(
        IDbContextFactory<DeskVaultDbContext> dbContextFactory,
        ILogger<DatabaseInitializer> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task InitializeAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation(
            LogMessages.DatabaseInitializationStarted);

        try
        {
            await using var dbContext =
                await _dbContextFactory.CreateDbContextAsync(
                    cancellationToken);

            if (!await dbContext.Database.CanConnectAsync(
                    cancellationToken))
            {
                _logger.LogInformation(
                    LogMessages.DatabaseConnectionUnavailable);

                await dbContext.Database.MigrateAsync(
                    cancellationToken);

                _logger.LogInformation(
                    LogMessages.DatabaseInitializationCompleted);

                return;
            }

            _logger.LogInformation(
                LogMessages.DatabaseConnectionAvailable);

            var migrationsTableExists =
                await HasMigrationsHistoryTableAsync(
                    dbContext,
                    cancellationToken);

            if (!migrationsTableExists)
            {
                _logger.LogInformation(
                    LogMessages.DatabaseMigrationsHistoryInitializing);

                await CreateMigrationsHistoryTableAsync(
                    dbContext,
                    cancellationToken);

                await RecordInitialMigrationAsync(
                    dbContext,
                    cancellationToken);
            }

            await dbContext.Database.MigrateAsync(
                cancellationToken);

            _logger.LogInformation(
                LogMessages.DatabaseInitializationCompleted);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                LogMessages.DatabaseInitializationFailed);

            throw;
        }
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
