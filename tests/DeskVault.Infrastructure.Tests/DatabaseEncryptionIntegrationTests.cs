using DeskVault.Infrastructure;
using DeskVault.Infrastructure.Persistence;
using DeskVault.Infrastructure.Persistence.Context;
using DeskVault.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DeskVault.Infrastructure.Tests;

public sealed class DatabaseEncryptionIntegrationTests
{
    [Fact]
    public async Task AddInfrastructure_InitializesDatabaseUsingDatabaseEncryptionKey()
    {
        string rootDirectory =
            CreateTemporaryDirectory();

        try
        {
            var services =
                new ServiceCollection();

            services.AddLogging();

            IConfiguration configuration =
                new ConfigurationBuilder()
                    .Build();

            services.AddSingleton(
                new DeskVaultDataPaths(
                    rootDirectory));

            services.AddInfrastructure(
                configuration);

            await using ServiceProvider serviceProvider =
                services.BuildServiceProvider();

            DeskVaultDataPaths paths =
                serviceProvider.GetRequiredService<DeskVaultDataPaths>();

            string databasePath =
                paths.DatabasePath;

            string databaseKeyPath =
                Path.Combine(
                    rootDirectory,
                    "Security",
                    "database.key");

            var initializer =
                serviceProvider.GetRequiredService<DatabaseInitializer>();

            await initializer.InitializeAsync();

            Assert.True(
                File.Exists(databasePath));

            Assert.True(
                File.Exists(databaseKeyPath));

            byte[] protectedDatabaseKey =
                await File.ReadAllBytesAsync(
                    databaseKeyPath);

            Assert.NotEmpty(
                protectedDatabaseKey);

            var factory =
                serviceProvider.GetRequiredService<
                    IDbContextFactory<DeskVaultDbContext>>();

            await using var dbContext =
                await factory.CreateDbContextAsync();

            Assert.True(
                await dbContext.Database.CanConnectAsync());

            await dbContext.Database.CloseConnectionAsync();
        }
        finally
        {
            DeleteTemporaryDirectory(
                rootDirectory);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        string directory =
            Path.Combine(
                Path.GetTempPath(),
                "DeskVaultTests",
                Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(
            directory);

        return directory;
    }

    private static void DeleteTemporaryDirectory(
        string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(
                directory,
                recursive: true);
        }
    }
}
