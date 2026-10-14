using System.Security.Cryptography;
using DeskVault.Infrastructure.Persistence;
using DeskVault.Infrastructure.Persistence.Context;
using DeskVault.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeskVault.Infrastructure.Tests;

public sealed class WindowsDatabaseEncryptionKeyServiceIntegrationTests
{
    [Fact]
    public async Task ConcurrentKeyProvisioning_InitializesEncryptedDatabaseUsingAuthoritativeKey()
    {
        // Arrange
        string rootDirectory =
            CreateTemporaryDirectory();

        EncryptionKeyMaterial[]? initializedKeyMaterials = null;
        byte[]? protectedKeyFromFile = null;
        byte[]? unprotectedPayload = null;
        EncryptionKeyMaterial? authoritativeKeyMaterial = null;
        EncryptionKeyMaterial? retrievedKeyMaterial = null;

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(
                    rootDirectory);

            const int concurrentCallCount = 8;

            var startGate =
                new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

            Task<EncryptionKeyMaterial>[] initializationTasks =
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
                                            NullLogger<WindowsDatabaseEncryptionKeyService>.Instance);

                                    return await service.GetOrCreateKeyAsync();
                                }))
                    .ToArray();

            // Act
            startGate.SetResult(true);

            initializedKeyMaterials =
                await Task.WhenAll(
                    initializationTasks);

            string databaseKeyPath =
                Path.Combine(
                    dataPaths.SecurityDirectory,
                    "database.key");

            // Assert
            Assert.True(
                File.Exists(databaseKeyPath));

            protectedKeyFromFile =
                await File.ReadAllBytesAsync(
                    databaseKeyPath);

            unprotectedPayload =
                ProtectedData.Unprotect(
                    protectedKeyFromFile,
                    null,
                    DataProtectionScope.CurrentUser);

            authoritativeKeyMaterial =
                ProtectedEncryptionKeyPayload.Decode(
                    unprotectedPayload,
                    protectedKeyFromFile,
                    EncryptionKeyPurpose.Database);

            Assert.Equal(
                32,
                authoritativeKeyMaterial.KeyBytes.Length);

            Assert.NotEqual(
                Guid.Empty,
                authoritativeKeyMaterial.KeyId);

            Assert.Equal(
                1,
                authoritativeKeyMaterial.KeyVersion);

            Assert.Equal(
                concurrentCallCount,
                initializedKeyMaterials.Length);

            Assert.All(
                initializedKeyMaterials,
                keyMaterial =>
                {
                    Assert.Equal(
                        authoritativeKeyMaterial.KeyBytes,
                        keyMaterial.KeyBytes);

                    Assert.Equal(
                        authoritativeKeyMaterial.KeyId,
                        keyMaterial.KeyId);

                    Assert.Equal(
                        authoritativeKeyMaterial.KeyVersion,
                        keyMaterial.KeyVersion);
                });

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

            retrievedKeyMaterial =
                await databaseKeyService.GetKeyAsync();

            Assert.Equal(
                authoritativeKeyMaterial.KeyBytes,
                retrievedKeyMaterial.KeyBytes);

            Assert.Equal(
                authoritativeKeyMaterial.KeyId,
                retrievedKeyMaterial.KeyId);

            Assert.Equal(
                authoritativeKeyMaterial.KeyVersion,
                retrievedKeyMaterial.KeyVersion);

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
            if (initializedKeyMaterials is not null)
            {
                foreach (EncryptionKeyMaterial keyMaterial in initializedKeyMaterials)
                {
                    CryptographicOperations.ZeroMemory(
                        keyMaterial.KeyBytes);
                }
            }

            if (protectedKeyFromFile is not null)
            {
                CryptographicOperations.ZeroMemory(
                    protectedKeyFromFile);
            }

            if (unprotectedPayload is not null)
            {
                CryptographicOperations.ZeroMemory(
                    unprotectedPayload);
            }

            if (authoritativeKeyMaterial is not null)
            {
                CryptographicOperations.ZeroMemory(
                    authoritativeKeyMaterial.KeyBytes);
            }

            if (retrievedKeyMaterial is not null)
            {
                CryptographicOperations.ZeroMemory(
                    retrievedKeyMaterial.KeyBytes);
            }

            DeleteTemporaryDirectory(
                rootDirectory);
        }
    }

    [Fact]
    public async Task ExistingEncryptedDatabase_WhenLegacyKeyArtifactIsUsed_RemainsReadableWithoutRewritingKey()
    {
        string rootDirectory =
            CreateTemporaryDirectory();

        EncryptionKeyMaterial? provisionedKey = null;
        EncryptionKeyMaterial? firstLegacyRead = null;
        EncryptionKeyMaterial? secondLegacyRead = null;

        byte[]? legacyProtectedArtifact = null;
        byte[]? artifactBeforeRetrieval = null;
        byte[]? artifactAfterRetrieval = null;

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(
                    rootDirectory);

            var services =
                new ServiceCollection();

            services.AddLogging();

            IConfiguration configuration =
                new ConfigurationBuilder()
                    .Build();

            services.AddSingleton(dataPaths);

            services.AddInfrastructure(configuration);

            await using ServiceProvider serviceProvider =
                services.BuildServiceProvider();

            var databaseKeyService =
                serviceProvider.GetRequiredService<
                    IDatabaseEncryptionKeyService>();

            var initializer =
                serviceProvider.GetRequiredService<
                    DatabaseInitializer>();

            var factory =
                serviceProvider.GetRequiredService<
                    IDbContextFactory<DeskVaultDbContext>>();

            // Arrange: create an encrypted database using the current key.
            provisionedKey =
                await databaseKeyService.GetOrCreateKeyAsync();

            await initializer.InitializeAsync();

            await using (var initialContext =
                await factory.CreateDbContextAsync())
            {
                Assert.True(
                    await initialContext.Database.CanConnectAsync());

                await initialContext.Database.CloseConnectionAsync();
            }

            string databaseKeyPath =
                Path.Combine(
                    dataPaths.SecurityDirectory,
                    "database.key");

            // Simulate the original protected raw-key artifact while preserving
            // the exact key bytes used to encrypt the existing database.
            legacyProtectedArtifact =
                ProtectedData.Protect(
                    provisionedKey.KeyBytes,
                    null,
                    DataProtectionScope.CurrentUser);

            await File.WriteAllBytesAsync(
                databaseKeyPath,
                legacyProtectedArtifact);

            artifactBeforeRetrieval =
                await File.ReadAllBytesAsync(
                    databaseKeyPath);

            // Act: retrieve the legacy key twice without provisioning a new one.
            firstLegacyRead =
                await databaseKeyService.GetKeyAsync();

            secondLegacyRead =
                await databaseKeyService.GetKeyAsync();

            // Assert: legacy identity/version is stable across retrieval.
            Assert.Equal(
                provisionedKey.KeyBytes,
                firstLegacyRead.KeyBytes);

            Assert.Equal(
                0,
                firstLegacyRead.KeyVersion);

            Assert.NotEqual(
                Guid.Empty,
                firstLegacyRead.KeyId);

            Assert.Equal(
                firstLegacyRead.KeyId,
                secondLegacyRead.KeyId);

            Assert.Equal(
                firstLegacyRead.KeyVersion,
                secondLegacyRead.KeyVersion);

            Assert.Equal(
                firstLegacyRead.KeyBytes,
                secondLegacyRead.KeyBytes);

            // The existing database must remain accessible with the legacy key.
            await initializer.InitializeAsync();

            await using (var existingContext =
                await factory.CreateDbContextAsync())
            {
                Assert.True(
                    await existingContext.Database.CanConnectAsync());

                await existingContext.Database.CloseConnectionAsync();
            }

            artifactAfterRetrieval =
                await File.ReadAllBytesAsync(
                    databaseKeyPath);

            // Retrieval and initialization must not rewrite the legacy artifact.
            Assert.Equal(
                artifactBeforeRetrieval,
                artifactAfterRetrieval);

            Assert.Empty(
                Directory.GetFiles(
                    dataPaths.SecurityDirectory,
                    "database.key.*.tmp"));
        }
        finally
        {
            if (provisionedKey is not null)
            {
                CryptographicOperations.ZeroMemory(
                    provisionedKey.KeyBytes);
            }

            if (firstLegacyRead is not null)
            {
                CryptographicOperations.ZeroMemory(
                    firstLegacyRead.KeyBytes);
            }

            if (secondLegacyRead is not null)
            {
                CryptographicOperations.ZeroMemory(
                    secondLegacyRead.KeyBytes);
            }

            if (legacyProtectedArtifact is not null)
            {
                CryptographicOperations.ZeroMemory(
                    legacyProtectedArtifact);
            }

            if (artifactBeforeRetrieval is not null)
            {
                CryptographicOperations.ZeroMemory(
                    artifactBeforeRetrieval);
            }

            if (artifactAfterRetrieval is not null)
            {
                CryptographicOperations.ZeroMemory(
                    artifactAfterRetrieval);
            }

            DeleteTemporaryDirectory(
                rootDirectory);
        }
    }

    [Fact]
    public async Task DatabaseAccess_WhenDatabaseKeyIsMissing_ThrowsWithoutCreatingReplacementKey()
    {
        // Arrange
        string rootDirectory =
            CreateTemporaryDirectory();

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(
                    rootDirectory);

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

            var initializer =
                serviceProvider.GetRequiredService<
                    DatabaseInitializer>();

            await initializer.InitializeAsync();

            string databaseKeyPath =
                Path.Combine(
                    dataPaths.SecurityDirectory,
                    "database.key");

            Assert.True(
                File.Exists(dataPaths.DatabasePath));

            Assert.True(
                File.Exists(databaseKeyPath));

            // Remove the canonical key after successful initialization.
            File.Delete(
                databaseKeyPath);

            string[] securityFilesBeforeAccess =
                Directory.GetFiles(
                        dataPaths.SecurityDirectory)
                    .OrderBy(
                        path => path,
                        StringComparer.Ordinal)
                    .ToArray();

            var factory =
                serviceProvider.GetRequiredService<
                    IDbContextFactory<DeskVaultDbContext>>();

            // Act and assert: asynchronous access must fail without provisioning.
            await Assert.ThrowsAsync<CryptographicException>(
                () =>
                    factory.CreateDbContextAsync());

            Assert.False(
                File.Exists(databaseKeyPath));

            Assert.Equal(
                securityFilesBeforeAccess,
                Directory.GetFiles(
                        dataPaths.SecurityDirectory)
                    .OrderBy(
                        path => path,
                        StringComparer.Ordinal)
                    .ToArray());

            Assert.True(
                File.Exists(dataPaths.DatabasePath));

            // Synchronous access must enforce the same strict retrieval behavior.
            Assert.Throws<CryptographicException>(
                () =>
                    factory.CreateDbContext());

            Assert.False(
                File.Exists(databaseKeyPath));

            Assert.Equal(
                securityFilesBeforeAccess,
                Directory.GetFiles(
                        dataPaths.SecurityDirectory)
                    .OrderBy(
                        path => path,
                        StringComparer.Ordinal)
                    .ToArray());
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
