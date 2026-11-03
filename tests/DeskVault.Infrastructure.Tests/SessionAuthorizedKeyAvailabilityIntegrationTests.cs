using System.Security.Cryptography;
using DeskVault.Application;
using DeskVault.Application.Interfaces;
using DeskVault.Application.Security.Sessions;
using DeskVault.Infrastructure.Persistence;
using DeskVault.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeskVault.Infrastructure.Tests;

public sealed class SessionAuthorizedKeyAvailabilityIntegrationTests
{
    [Fact]
    public async Task ProductionRegistration_WhenUnauthenticatedAndKeysAreMissing_DeniesRetrievalWithoutCreatingSecurityArtifacts()
    {
        string rootDirectory =
            CreateTemporaryDirectory();

        try
        {
            //Arrange
            var dataPaths =
                new DeskVaultDataPaths(rootDirectory);

            var documentProvisioning =
                new CountingDocumentKeyProvisioningService(dataPaths);

            var databaseProvisioning =
                new CountingDatabaseKeyProvisioningService(dataPaths);

            await using ServiceProvider serviceProvider =
                CreateServiceProvider(
                    dataPaths,
                    documentProvisioning,
                    databaseProvisioning);

            IApplicationSecuritySession securitySession =
                serviceProvider.GetRequiredService<IApplicationSecuritySession>();

            Assert.Equal(
                ApplicationSecuritySessionState.Unauthenticated,
                securitySession.CurrentSnapshot.State);

            // Act
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => serviceProvider
                    .GetRequiredService<IEncryptionKeyService>()
                    .GetKeyAsync(TestContext.Current.CancellationToken));

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => serviceProvider
                    .GetRequiredService<IDatabaseEncryptionKeyService>()
                    .GetKeyAsync(TestContext.Current.CancellationToken));

            // Assert
            Assert.Equal(
                0,
                documentProvisioning.CallCount);

            Assert.Equal(
                0,
                databaseProvisioning.CallCount);

            Assert.False(
                Directory.Exists(dataPaths.SecurityDirectory));

            Assert.False(
                File.Exists(
                    Path.Combine(dataPaths.SecurityDirectory, "master.key")));

            Assert.False(
                File.Exists(
                    Path.Combine(dataPaths.SecurityDirectory, "database.key")));
        }
        finally
        {
            DeleteTemporaryDirectory(rootDirectory);
        }
    }

    [Fact]
    public async Task ProductionRegistration_WhenSessionLocks_DeniesBothRetrievalsWithoutProvisioningOrChangingExistingKeyArtifacts_AndReauthenticationRestoresAccess()
    {
        string rootDirectory =
            CreateTemporaryDirectory();

        EncryptionKeyMaterial? documentKeyBeforeLock = null;
        EncryptionKeyMaterial? databaseKeyBeforeLock = null;
        EncryptionKeyMaterial? documentKeyAfterUnlock = null;
        EncryptionKeyMaterial? databaseKeyAfterUnlock = null;

        byte[]? expectedDocumentKeyBytes = null;
        byte[]? expectedDatabaseKeyBytes = null;

        Dictionary<string, byte[]>? artifactSnapshot = null;

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(rootDirectory);

            var documentProvisioning =
                new CountingDocumentKeyProvisioningService(dataPaths);

            var databaseProvisioning =
                new CountingDatabaseKeyProvisioningService(dataPaths);

            await using ServiceProvider serviceProvider =
                CreateServiceProvider(
                    dataPaths,
                    documentProvisioning,
                    databaseProvisioning);

            IApplicationSecuritySession securitySession =
                serviceProvider.GetRequiredService<IApplicationSecuritySession>();

            IEncryptionKeyService documentKeyService =
                serviceProvider.GetRequiredService<IEncryptionKeyService>();

            IDatabaseEncryptionKeyService databaseKeyService =
                serviceProvider.GetRequiredService<IDatabaseEncryptionKeyService>();

            Assert.IsType<SessionAuthorizedEncryptionKeyService>(
                documentKeyService);

            Assert.IsType<SessionAuthorizedDatabaseEncryptionKeyService>(
                databaseKeyService);

            AuthenticateSession(securitySession);

            // Initialize a real vault through the production lifecycle.
            await serviceProvider
                .GetRequiredService<DatabaseInitializer>()
                .InitializeAsync(TestContext.Current.CancellationToken);

            string documentKeyPath =
                Path.Combine(
                    dataPaths.SecurityDirectory,
                    "master.key");

            string databaseKeyPath =
                Path.Combine(
                    dataPaths.SecurityDirectory,
                    "database.key");

            Assert.True(File.Exists(documentKeyPath));
            Assert.True(File.Exists(databaseKeyPath));

            Assert.True(
                documentProvisioning.CallCount > 0);

            Assert.True(
                databaseProvisioning.CallCount > 0);

            // Capture protected artifacts and provisioning counts before denial.
            artifactSnapshot =
                CaptureSecurityArtifacts(dataPaths.SecurityDirectory);

            Assert.Contains("master.key", artifactSnapshot.Keys);
            Assert.Contains("database.key", artifactSnapshot.Keys);

            int documentProvisioningCalls =
                documentProvisioning.CallCount;

            int databaseProvisioningCalls =
                databaseProvisioning.CallCount;

            // Verify authenticated retrieval uses the persisted keys.
            documentKeyBeforeLock =
                await documentKeyService.GetKeyAsync(
                    TestContext.Current.CancellationToken);

            databaseKeyBeforeLock =
                await databaseKeyService.GetKeyAsync(
                    TestContext.Current.CancellationToken);

            expectedDocumentKeyBytes =
                (byte[])documentKeyBeforeLock.KeyBytes.Clone();

            expectedDatabaseKeyBytes =
                (byte[])databaseKeyBeforeLock.KeyBytes.Clone();

            // Act: withdraw authorization.
            securitySession.Lock();

            Assert.Equal(
                ApplicationSecuritySessionState.Locked,
                securitySession.CurrentSnapshot.State);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => documentKeyService.GetKeyAsync(
                    TestContext.Current.CancellationToken));

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => databaseKeyService.GetKeyAsync(
                    TestContext.Current.CancellationToken));

            // Denial must not invoke either provisioning service.
            Assert.Equal(
                documentProvisioningCalls,
                documentProvisioning.CallCount);

            Assert.Equal(
                databaseProvisioningCalls,
                databaseProvisioning.CallCount);

            // Neither existing key nor temporary key artifacts may change.
            AssertSecurityArtifactsUnchanged(
                artifactSnapshot,
                dataPaths.SecurityDirectory);

            // Re-establish authorization through the Application session.
            AuthenticateSession(securitySession);

            Assert.Equal(
                ApplicationSecuritySessionState.Authenticated,
                securitySession.CurrentSnapshot.State);

            // Assert: both production retrieval paths work again.
            documentKeyAfterUnlock =
                await documentKeyService.GetKeyAsync(
                    TestContext.Current.CancellationToken);

            databaseKeyAfterUnlock =
                await databaseKeyService.GetKeyAsync(
                    TestContext.Current.CancellationToken);

            Assert.Equal(
                expectedDocumentKeyBytes,
                documentKeyAfterUnlock.KeyBytes);

            Assert.Equal(
                expectedDatabaseKeyBytes,
                databaseKeyAfterUnlock.KeyBytes);

            Assert.Equal(
                documentKeyBeforeLock.KeyId,
                documentKeyAfterUnlock.KeyId);

            Assert.Equal(
                databaseKeyBeforeLock.KeyId,
                databaseKeyAfterUnlock.KeyId);

            Assert.Equal(
                documentKeyBeforeLock.KeyVersion,
                documentKeyAfterUnlock.KeyVersion);

            Assert.Equal(
                databaseKeyBeforeLock.KeyVersion,
                databaseKeyAfterUnlock.KeyVersion);

            Assert.Equal(
                documentProvisioningCalls,
                documentProvisioning.CallCount);

            Assert.Equal(
                databaseProvisioningCalls,
                databaseProvisioning.CallCount);

            AssertSecurityArtifactsUnchanged(
                artifactSnapshot,
                dataPaths.SecurityDirectory);
        }
        finally
        {
            ClearKeyMaterial(
                documentKeyBeforeLock,
                databaseKeyBeforeLock,
                documentKeyAfterUnlock,
                databaseKeyAfterUnlock);

            ClearBuffer(expectedDocumentKeyBytes);
            ClearBuffer(expectedDatabaseKeyBytes);

            ClearSecurityArtifactSnapshot(artifactSnapshot);

            DeleteTemporaryDirectory(rootDirectory);
        }
    }

    private static ServiceProvider CreateServiceProvider(
        DeskVaultDataPaths dataPaths,
        CountingDocumentKeyProvisioningService documentProvisioning,
        CountingDatabaseKeyProvisioningService databaseProvisioning)
    {
        var services =
            new ServiceCollection();

        services.AddLogging();

        IConfiguration configuration =
            new ConfigurationBuilder()
                .Build();

        services.AddSingleton(dataPaths);

        services.AddApplication();

        services.AddInfrastructure(configuration);

        // Instrument the production provisioning implementations.
        // The strict retrieval registrations remain unchanged.
        services.AddSingleton<IEncryptionKeyProvisioningService>(
            documentProvisioning);

        services.AddSingleton<IDatabaseEncryptionKeyProvisioningService>(
            databaseProvisioning);

        return services.BuildServiceProvider();
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

    private static Dictionary<string, byte[]> CaptureSecurityArtifacts(
        string securityDirectory)
    {
        if (!Directory.Exists(securityDirectory))
        {
            return new Dictionary<string, byte[]>(
                StringComparer.Ordinal);
        }

        return Directory.GetFiles(
                securityDirectory,
                "*",
                SearchOption.AllDirectories)
            .ToDictionary(
                path => Path.GetRelativePath(securityDirectory, path),
                File.ReadAllBytes,
                StringComparer.Ordinal);
    }

    private static void AssertSecurityArtifactsUnchanged(
        IReadOnlyDictionary<string, byte[]> expected,
        string securityDirectory)
    {
        Dictionary<string, byte[]> actual =
            CaptureSecurityArtifacts(securityDirectory);

        try
        {
            string[] expectedPaths =
                expected.Keys
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToArray();

            string[] actualPaths =
                actual.Keys
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToArray();

            Assert.Equal(
                expectedPaths,
                actualPaths);

            foreach (string relativePath in expectedPaths)
            {
                Assert.True(
                    expected[relativePath].AsSpan().SequenceEqual(
                        actual[relativePath]),
                    $"Security artifact changed unexpectedly: {relativePath}");
            }
        }
        finally
        {
            ClearSecurityArtifactSnapshot(actual);
        }
    }

    private static void ClearSecurityArtifactSnapshot(
        Dictionary<string, byte[]>? snapshot)
    {
        if (snapshot is null)
        {
            return;
        }

        foreach (byte[] contents in snapshot.Values)
        {
            CryptographicOperations.ZeroMemory(contents);
        }
    }

    private static void ClearKeyMaterial(
        params EncryptionKeyMaterial?[] materials)
    {
        foreach (EncryptionKeyMaterial? material in materials)
        {
            if (material is not null)
            {
                ClearBuffer(material.KeyBytes);
            }
        }
    }

    private static void ClearBuffer(byte[]? buffer)
    {
        if (buffer is not null)
        {
            CryptographicOperations.ZeroMemory(buffer);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        string directory =
            Path.Combine(
                Path.GetTempPath(),
                "DeskVaultTests",
                Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(directory);

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

    private sealed class CountingDocumentKeyProvisioningService
        : IEncryptionKeyProvisioningService
    {
        private readonly IEncryptionKeyProvisioningService _innerService;
        private int _callCount;

        public CountingDocumentKeyProvisioningService(
            DeskVaultDataPaths dataPaths)
        {
            _innerService =
                new WindowsEncryptionKeyProvisioningService(
                    dataPaths,
                    NullLogger<WindowsEncryptionKeyService>.Instance);
        }

        public int CallCount =>
            Volatile.Read(ref _callCount);

        public Task<EncryptionKeyMaterial> GetOrCreateKeyAsync(
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _callCount);

            return _innerService.GetOrCreateKeyAsync(
                cancellationToken);
        }
    }

    private sealed class CountingDatabaseKeyProvisioningService
        : IDatabaseEncryptionKeyProvisioningService
    {
        private readonly IDatabaseEncryptionKeyProvisioningService _innerService;
        private int _callCount;

        public CountingDatabaseKeyProvisioningService(
            DeskVaultDataPaths dataPaths)
        {
            _innerService =
                new WindowsDatabaseEncryptionKeyProvisioningService(
                    dataPaths,
                    NullLogger<WindowsDatabaseEncryptionKeyService>.Instance);
        }

        public int CallCount =>
            Volatile.Read(ref _callCount);

        public Task<EncryptionKeyMaterial> GetOrCreateKeyAsync(
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _callCount);

            return _innerService.GetOrCreateKeyAsync(
                cancellationToken);
        }
    }
}
