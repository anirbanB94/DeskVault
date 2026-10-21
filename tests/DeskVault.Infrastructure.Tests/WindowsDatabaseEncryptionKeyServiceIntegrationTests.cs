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
    public async Task DatabaseInitializer_WhenDatabaseIsMissingButDocumentArtifactsExist_FailsWithoutProvisioningKeys()
    {
        string rootDirectory =
            CreateTemporaryDirectory();

        byte[] existingArtifactContents =
            [0x44, 0x56, 0x41, 0x55, 0x4C, 0x54];

        byte[]? artifactContentsAfterInitialization = null;

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(
                    rootDirectory);

            Directory.CreateDirectory(
                dataPaths.DocumentsDirectory);

            Guid documentId =
                Guid.NewGuid();

            string documentArtifactPath =
                Path.Combine(
                    dataPaths.DocumentsDirectory,
                    $"{documentId}.dvault");

            await File.WriteAllBytesAsync(documentArtifactPath, existingArtifactContents, TestContext.Current.CancellationToken);

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

            var initializer =
                serviceProvider.GetRequiredService<
                    DatabaseInitializer>();

            // Act: missing database plus existing document data
            // must not be treated as a new vault.
            await Assert.ThrowsAsync<InvalidOperationException>(
                () =>
                    initializer.InitializeAsync(TestContext.Current.CancellationToken));

            artifactContentsAfterInitialization =
                await File.ReadAllBytesAsync(documentArtifactPath, TestContext.Current.CancellationToken);

            // Assert: no database or replacement key is provisioned.
            Assert.False(
                File.Exists(
                    dataPaths.DatabasePath));

            Assert.False(
                File.Exists(
                    Path.Combine(
                        dataPaths.SecurityDirectory,
                        "master.key")));

            Assert.False(
                File.Exists(
                    Path.Combine(
                        dataPaths.SecurityDirectory,
                        "database.key")));

            // The existing document artifact must remain unchanged.
            Assert.Equal(
                existingArtifactContents,
                artifactContentsAfterInitialization);

            Assert.Equal(
                new[] { documentArtifactPath },
                Directory.GetFiles(
                    dataPaths.DocumentsDirectory));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                existingArtifactContents);

            if (artifactContentsAfterInitialization is not null)
            {
                CryptographicOperations.ZeroMemory(
                    artifactContentsAfterInitialization);
            }

            DeleteTemporaryDirectory(
                rootDirectory);
        }
    }

    [Fact]
    public async Task DatabaseInitializer_WhenInitializingNewVault_ExplicitlyProvisionsBothEncryptionKeys()
    {
        string rootDirectory =
            CreateTemporaryDirectory();

        EncryptionKeyMaterial? documentKey = null;
        EncryptionKeyMaterial? databaseKey = null;

        byte[]? documentArtifactBeforeRestart = null;
        byte[]? documentArtifactAfterRestart = null;
        byte[]? databaseArtifactBeforeRestart = null;
        byte[]? databaseArtifactAfterRestart = null;

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

            var initializer =
                serviceProvider.GetRequiredService<
                    DatabaseInitializer>();

            // Act: initialize a new vault through the production boundary.
            await initializer.InitializeAsync(TestContext.Current.CancellationToken);

            string documentKeyPath =
                Path.Combine(
                    dataPaths.SecurityDirectory,
                    "master.key");

            string databaseKeyPath =
                Path.Combine(
                    dataPaths.SecurityDirectory,
                    "database.key");

            // Both keys and the encrypted database must be initialized.
            Assert.True(
                File.Exists(dataPaths.DatabasePath));

            Assert.True(
                File.Exists(documentKeyPath));

            Assert.True(
                File.Exists(databaseKeyPath));

            Assert.Equal(
                2,
                Directory.GetFiles(
                    dataPaths.SecurityDirectory).Length);

            var documentKeyService =
                serviceProvider.GetRequiredService<
                    IEncryptionKeyService>();

            var databaseKeyService =
                serviceProvider.GetRequiredService<
                    IDatabaseEncryptionKeyService>();

            documentKey =
                await documentKeyService.GetKeyAsync(TestContext.Current.CancellationToken);

            databaseKey =
                await databaseKeyService.GetKeyAsync(TestContext.Current.CancellationToken);

            Assert.NotEqual(
                Guid.Empty,
                documentKey.KeyId);

            Assert.NotEqual(
                Guid.Empty,
                databaseKey.KeyId);

            Assert.Equal(
                1,
                documentKey.KeyVersion);

            Assert.Equal(
                1,
                databaseKey.KeyVersion);

            // Document and database keys remain separate key materials.
            Assert.False(
                documentKey.KeyBytes.SequenceEqual(
                    databaseKey.KeyBytes));

            Assert.NotEqual(
                documentKey.KeyId,
                databaseKey.KeyId);

            documentArtifactBeforeRestart =
                await File.ReadAllBytesAsync(documentKeyPath, TestContext.Current.CancellationToken);

            databaseArtifactBeforeRestart =
                await File.ReadAllBytesAsync(databaseKeyPath, TestContext.Current.CancellationToken);

            // Repeated startup must reuse existing key state.
            await initializer.InitializeAsync(TestContext.Current.CancellationToken);

            documentArtifactAfterRestart =
                await File.ReadAllBytesAsync(documentKeyPath, TestContext.Current.CancellationToken);

            databaseArtifactAfterRestart =
                await File.ReadAllBytesAsync(databaseKeyPath, TestContext.Current.CancellationToken);

            Assert.Equal(
                documentArtifactBeforeRestart,
                documentArtifactAfterRestart);

            Assert.Equal(
                databaseArtifactBeforeRestart,
                databaseArtifactAfterRestart);

            Assert.Empty(
                Directory.GetFiles(
                    dataPaths.SecurityDirectory,
                    "*.tmp"));
        }
        finally
        {
            if (documentKey is not null)
            {
                CryptographicOperations.ZeroMemory(
                    documentKey.KeyBytes);
            }

            if (databaseKey is not null)
            {
                CryptographicOperations.ZeroMemory(
                    databaseKey.KeyBytes);
            }

            if (documentArtifactBeforeRestart is not null)
            {
                CryptographicOperations.ZeroMemory(
                    documentArtifactBeforeRestart);
            }

            if (documentArtifactAfterRestart is not null)
            {
                CryptographicOperations.ZeroMemory(
                    documentArtifactAfterRestart);
            }

            if (databaseArtifactBeforeRestart is not null)
            {
                CryptographicOperations.ZeroMemory(
                    databaseArtifactBeforeRestart);
            }

            if (databaseArtifactAfterRestart is not null)
            {
                CryptographicOperations.ZeroMemory(
                    databaseArtifactAfterRestart);
            }

            DeleteTemporaryDirectory(
                rootDirectory);
        }
    }

    [Fact]
    public async Task DatabaseInitializer_WhenExistingEmptyVaultHasNoDocumentKey_ProvisionsInitialDocumentKey()
    {
        string rootDirectory =
            CreateTemporaryDirectory();

        EncryptionKeyMaterial? databaseKey = null;
        EncryptionKeyMaterial? documentKey = null;

        byte[]? databaseArtifactBeforeInitialization = null;
        byte[]? databaseArtifactAfterInitialization = null;

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(
                    rootDirectory);

            var databaseKeyService =
                new WindowsDatabaseEncryptionKeyService(
                    dataPaths,
                    NullLogger<WindowsDatabaseEncryptionKeyService>.Instance);

            databaseKey =
                await databaseKeyService.GetOrCreateKeyAsync(TestContext.Current.CancellationToken);

            // Reproduce the valid legacy state: the encrypted database
            // already exists, but document-key provisioning has not occurred.
            var existingDatabaseFactory =
                new EncryptedDeskVaultDbContextFactory(
                    dataPaths,
                    databaseKeyService);

            await using (
                var dbContext =
                    await existingDatabaseFactory.CreateDbContextAsync(TestContext.Current.CancellationToken))
            {
                await dbContext.Database.MigrateAsync(cancellationToken: TestContext.Current.CancellationToken);
            }

            string documentKeyPath =
                Path.Combine(
                    dataPaths.SecurityDirectory,
                    "master.key");

            string databaseKeyPath =
                Path.Combine(
                    dataPaths.SecurityDirectory,
                    "database.key");

            Assert.True(
                File.Exists(dataPaths.DatabasePath));

            Assert.True(
                File.Exists(databaseKeyPath));

            Assert.False(
                File.Exists(documentKeyPath));

            databaseArtifactBeforeInitialization =
                await File.ReadAllBytesAsync(databaseKeyPath, TestContext.Current.CancellationToken);

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

            var initializer =
                serviceProvider.GetRequiredService<
                    DatabaseInitializer>();

            // Act: initialization must recognize this as an empty vault
            // without treating an unusable existing key as replaceable.
            await initializer.InitializeAsync(TestContext.Current.CancellationToken);

            var documentKeyService =
                serviceProvider.GetRequiredService<
                    IEncryptionKeyService>();

            documentKey =
                await documentKeyService.GetKeyAsync(TestContext.Current.CancellationToken);

            databaseArtifactAfterInitialization =
                await File.ReadAllBytesAsync(databaseKeyPath, TestContext.Current.CancellationToken);

            // Assert: the initial document key is now available.
            Assert.True(
                File.Exists(documentKeyPath));

            Assert.NotEqual(
                Guid.Empty,
                documentKey.KeyId);

            Assert.Equal(
                1,
                documentKey.KeyVersion);

            // Existing database-key state remains authoritative.
            Assert.Equal(
                databaseArtifactBeforeInitialization,
                databaseArtifactAfterInitialization);

            Assert.True(
                File.Exists(dataPaths.DatabasePath));

            Assert.Empty(
                Directory.GetFiles(
                    dataPaths.SecurityDirectory,
                    "master.key.*.tmp"));
        }
        finally
        {
            if (databaseKey is not null)
            {
                CryptographicOperations.ZeroMemory(
                    databaseKey.KeyBytes);
            }

            if (documentKey is not null)
            {
                CryptographicOperations.ZeroMemory(
                    documentKey.KeyBytes);
            }

            if (databaseArtifactBeforeInitialization is not null)
            {
                CryptographicOperations.ZeroMemory(
                    databaseArtifactBeforeInitialization);
            }

            if (databaseArtifactAfterInitialization is not null)
            {
                CryptographicOperations.ZeroMemory(
                    databaseArtifactAfterInitialization);
            }

            DeleteTemporaryDirectory(
                rootDirectory);
        }
    }

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
                await File.ReadAllBytesAsync(databaseKeyPath, TestContext.Current.CancellationToken);

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
                await databaseKeyService.GetKeyAsync(TestContext.Current.CancellationToken);

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

            await initializer.InitializeAsync(TestContext.Current.CancellationToken);

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
                await factory.CreateDbContextAsync(TestContext.Current.CancellationToken))
            {
                Assert.True(
                    await dbContext.Database.CanConnectAsync(TestContext.Current.CancellationToken));

                await dbContext.Database.CloseConnectionAsync();
            }

            // Verify that startup can reuse the existing encrypted database
            // and retrieve its already-persisted authoritative key.
            await initializer.InitializeAsync(TestContext.Current.CancellationToken);

            await using var existingVaultDbContext =
                await factory.CreateDbContextAsync(TestContext.Current.CancellationToken);

            Assert.True(
                await existingVaultDbContext.Database.CanConnectAsync(TestContext.Current.CancellationToken));

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

            var databaseKeyProvisioningService =
                serviceProvider.GetRequiredService<
                    IDatabaseEncryptionKeyProvisioningService>();

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
                await databaseKeyProvisioningService.GetOrCreateKeyAsync(TestContext.Current.CancellationToken);

            await initializer.InitializeAsync(TestContext.Current.CancellationToken);

            await using (var initialContext =
                await factory.CreateDbContextAsync(TestContext.Current.CancellationToken))
            {
                Assert.True(
                    await initialContext.Database.CanConnectAsync(TestContext.Current.CancellationToken));

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

            await File.WriteAllBytesAsync(databaseKeyPath, legacyProtectedArtifact, TestContext.Current.CancellationToken);

            artifactBeforeRetrieval =
                await File.ReadAllBytesAsync(databaseKeyPath, TestContext.Current.CancellationToken);

            // Act: retrieve the legacy key twice without provisioning a new one.
            firstLegacyRead =
                await databaseKeyService.GetKeyAsync(TestContext.Current.CancellationToken);

            secondLegacyRead =
                await databaseKeyService.GetKeyAsync(TestContext.Current.CancellationToken);

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
            await initializer.InitializeAsync(TestContext.Current.CancellationToken);

            await using (var existingContext =
                await factory.CreateDbContextAsync(TestContext.Current.CancellationToken))
            {
                Assert.True(
                    await existingContext.Database.CanConnectAsync(TestContext.Current.CancellationToken));

                await existingContext.Database.CloseConnectionAsync();
            }

            artifactAfterRetrieval =
                await File.ReadAllBytesAsync(databaseKeyPath, TestContext.Current.CancellationToken);

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

            await initializer.InitializeAsync(TestContext.Current.CancellationToken);

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
                    factory.CreateDbContextAsync(TestContext.Current.CancellationToken));

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
