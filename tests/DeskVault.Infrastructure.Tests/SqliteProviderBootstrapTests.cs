using DeskVault.Infrastructure.Persistence.Context;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DeskVault.Infrastructure.Tests;

public sealed class SqliteProviderBootstrapTests
{
    [Fact]
    public async Task EnsureInitialized_WhenCalledRepeatedly_PreservesScalarFunctionCallbacksForOpenConnections()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        // Arrange: initialize the process-wide provider and create an open
        // connection whose database context registers DeskVault's scalar function.
        DeskVault.Infrastructure.Persistence.SqliteProviderBootstrap
            .EnsureInitialized();

        await using var connection =
            new SqliteConnection("Data Source=:memory:");

        await connection.OpenAsync(cancellationToken);

        DbContextOptions<DeskVaultDbContext> options =
            new DbContextOptionsBuilder<DeskVaultDbContext>()
                .UseSqlite(connection)
                .Options;

        await using var dbContext =
            new DeskVaultDbContext(options);

        // Act: repeat initialization while the connection still owns a
        // registered callback, then force collection to exercise its lifetime.
        DeskVault.Infrastructure.Persistence.SqliteProviderBootstrap
            .EnsureInitialized();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        await using var command = connection.CreateCommand();

        command.CommandText =
            "SELECT dv_contains_canonicalized('searchable marker', 'searchable');";

        object? result =
            await command.ExecuteScalarAsync(cancellationToken);

        // Assert: the callback remains callable after repeated initialization
        // and a full collection cycle.
        Assert.Equal(
            1L,
            Convert.ToInt64(result));
    }
}