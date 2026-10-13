using System.Security.Cryptography;
using DeskVault.Infrastructure.Persistence;
using DeskVault.Infrastructure.Persistence.Context;
using DeskVault.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DeskVault.Infrastructure.Tests;

public sealed class WindowsDatabaseEncryptionKeyServiceIntegrationTests
{
    [Fact]
    public async Task ConcurrentKeyProvisioning_InitializesEncryptedDatabaseUsingAuthoritativeKey()
    {
        string rootDirectory =
            CreateTemporaryDirectory();

        byte[][]? initializedKeys = null;
        byte[]? protectedKeyFromFile = null;
        byte[]? authoritativeKey = null;
        byte[]? retrievedKey = null;

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(
                    rootDirectory);

            const int concurrentCallCount = 8;

            var startGate =
                new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

            Task<byte[]>[] initializationTasks =
                Enumerable
                    .Range(0, concurrentCallCount)
                    .Select(
                        _ =>
                            Task.Run(
                                async () =>
                                {
                                    await startGate.Task;

                                    var service =
                                        new WindowsDatabaseEncryptionKeyService(
                                            dataPaths,
                                            Microsoft.Extensions.Logging.Abstractions
                                                .NullLogger<WindowsDatabaseEncryptionKeyService>
                                                .Instance);

                                    return await service.GetOrCreateKeyAsync();
                                }))
                    .ToArray();

            startGate.SetResult(true);

            initializedKeys =
                await Task.WhenAll(
                    initializationTasks);

            string databaseKeyPath =
                Path.Combine(
                    dataPaths.SecurityDirectory,
                    "database.key");

            Assert.True(
                File.Exists(databaseKeyPath));

            protectedKeyFromFile =
                await File.ReadAllBytesAsync(
                    databaseKeyPath);

            authoritativeKey =
                ProtectedData.Unprotect(
                    protectedKeyFromFile,
                    null,
                    DataProtectionScope.CurrentUser);

            Assert.Equal(
                32,
                authoritativeKey.Length);

            Assert.All(
                initializedKeys,
                key =>
                    Assert.Equal(
                        authoritativeKey,
                        key));

            // Exercise the production Infrastructure registration and
            // initialize the database using the persisted key.
            var services =
                new ServiceCollection();

            services.AddLogging();

            IConfiguration configuration =
                new ConfigurationBuilder()
                    .Build();

            services.AddSingleton(
                dataPaths);

            services.AddInfrastructure(
                configuration);

            await using ServiceProvider serviceProvider =
                services.BuildServiceProvider();

            var databaseKeyService =
                serviceProvider.GetRequiredService<
                    IDatabaseEncryptionKeyService>();

            retrievedKey =
                await databaseKeyService.GetKeyAsync();

            Assert.Equal(
                authoritativeKey,
                retrievedKey);

            var initializer =
                serviceProvider.GetRequiredService<
                    DatabaseInitializer>();

            await initializer.InitializeAsync();

            Assert.True(
                File.Exists(
                    dataPaths.DatabasePath));

            Assert.True(
                File.Exists(
                    databaseKeyPath));

            Assert.Empty(
                Directory.GetFiles(
                    dataPaths.SecurityDirectory,
                    "database.key.*.tmp"));

            var factory =
                serviceProvider.GetRequiredService<
                    IDbContextFactory<DeskVaultDbContext>>();

            await using (var dbContext =
                await factory.CreateDbContextAsync())
            {
                Assert.True(
                    await dbContext.Database.CanConnectAsync());

                await dbContext.Database.CloseConnectionAsync();
            }

            // Verify that startup can reuse the existing encrypted database
            // and retrieve its already-persisted authoritative key.
            await initializer.InitializeAsync();

            await using var existingVaultDbContext =
                await factory.CreateDbContextAsync();

            Assert.True(
                await existingVaultDbContext.Database.CanConnectAsync());

            await existingVaultDbContext.Database.CloseConnectionAsync();
        }
        finally
        {
            if (initializedKeys is not null)
            {
                foreach (byte[] key in initializedKeys)
                {
                    CryptographicOperations.ZeroMemory(
                        key);
                }
            }

            if (protectedKeyFromFile is not null)
            {
                CryptographicOperations.ZeroMemory(
                    protectedKeyFromFile);
            }

            if (authoritativeKey is not null)
            {
                CryptographicOperations.ZeroMemory(
                    authoritativeKey);
            }

            if (retrievedKey is not null)
            {
                CryptographicOperations.ZeroMemory(
                    retrievedKey);
            }

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
