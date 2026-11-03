using DeskVault.Application;
using DeskVault.Application.Interfaces;
using DeskVault.Application.Security.Sessions;
using DeskVault.Infrastructure.Persistence;
using DeskVault.Infrastructure.Persistence.Context;
using DeskVault.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

            services.AddApplication();

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

            // Follow production startup ordering:
            // authenticate before database initialization.
            AuthenticateSession(
                serviceProvider.GetRequiredService<IApplicationSecuritySession>());

            var initializer =
                serviceProvider.GetRequiredService<DatabaseInitializer>();

            await initializer.InitializeAsync(
                TestContext.Current.CancellationToken);

            Assert.True(
                File.Exists(databasePath));

            Assert.True(
                File.Exists(databaseKeyPath));

            byte[] protectedDatabaseKey =
                await File.ReadAllBytesAsync(
                    databaseKeyPath,
                    TestContext.Current.CancellationToken);

            Assert.NotEmpty(
                protectedDatabaseKey);

            var factory =
                serviceProvider.GetRequiredService<
                    IDbContextFactory<DeskVaultDbContext>>();

            await using var dbContext =
                await factory.CreateDbContextAsync(
                    TestContext.Current.CancellationToken);

            Assert.True(
                await dbContext.Database.CanConnectAsync(
                    TestContext.Current.CancellationToken));

            await dbContext.Database.CloseConnectionAsync();
        }
        finally
        {
            DeleteTemporaryDirectory(
                rootDirectory);
        }
    }

    [Fact]
    public async Task DatabaseInitializer_WhenInitializationFails_CanRetrySuccessfully()
    {
        // Arrange
        string rootDirectory =
            CreateTemporaryDirectory();

        string databasePath =
            Path.Combine(
                rootDirectory,
                "DeskVault.db");

        string migrationPath =
            databasePath +
            ".migration";

        try
        {
            File.WriteAllText(
                migrationPath,
                "invalid migration artifact");

            var services =
                new ServiceCollection();

            services.AddLogging();

            IConfiguration configuration =
                new ConfigurationBuilder()
                    .Build();

            services.AddSingleton(
                new DeskVaultDataPaths(
                    rootDirectory));

            services.AddApplication();

            services.AddInfrastructure(
                configuration);

            await using ServiceProvider serviceProvider =
                services.BuildServiceProvider();

            DatabaseInitializer initializer =
                serviceProvider.GetRequiredService<DatabaseInitializer>();

            // Authenticate before either initialization attempt.
            AuthenticateSession(
                serviceProvider.GetRequiredService<IApplicationSecuritySession>());

            // Act
            await Assert.ThrowsAsync<InvalidOperationException>(
                () =>
                    initializer.InitializeAsync(
                        TestContext.Current.CancellationToken));

            File.Delete(
                migrationPath);

            await initializer.InitializeAsync(
                TestContext.Current.CancellationToken);

            // Assert
            Assert.True(
                File.Exists(
                    databasePath));

            Assert.False(
                File.Exists(
                    migrationPath));

            string databaseKeyPath =
                Path.Combine(
                    rootDirectory,
                    "Security",
                    "database.key");

            Assert.True(
                File.Exists(
                    databaseKeyPath));

            IDbContextFactory<DeskVaultDbContext> factory =
                serviceProvider.GetRequiredService<
                    IDbContextFactory<DeskVaultDbContext>>();

            await using DeskVaultDbContext dbContext =
                await factory.CreateDbContextAsync(
                    TestContext.Current.CancellationToken);

            Assert.True(
                await dbContext.Database.CanConnectAsync(
                    TestContext.Current.CancellationToken));

            await dbContext.Database.CloseConnectionAsync();
        }
        finally
        {
            DeleteTemporaryDirectory(
                rootDirectory);
        }
    }

    private static void AuthenticateSession(
        IApplicationSecuritySession securitySession)
    {
        ApplicationSecuritySessionAttempt attempt =
            securitySession.BeginAuthentication();

        Assert.True(
            securitySession.TryCompleteAuthentication(
                attempt,
                ApplicationAuthenticationOutcome.Succeeded));
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
