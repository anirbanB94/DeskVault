using System.Security.Cryptography;
using DeskVault.Infrastructure.Persistence;
using DeskVault.Infrastructure.Persistence.Context;
using DeskVault.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace DeskVault.Infrastructure.Tests;

public sealed class EncryptedDeskVaultDbContextFactoryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreateDbContext_WhenContextIsCreated_ClearsAcquiredKeyBufferAndRemainsUsable(
        bool useAsyncFactory)
    {
        // Arrange
        using var fixture =
            new FactoryTestFixture();

        await CreateEncryptedDatabaseAsync(
            fixture.DataPaths.DatabasePath,
            fixture.ServiceBackingKey);

        // Act
        await using DeskVaultDbContext dbContext =
            useAsyncFactory
                ? await fixture.Factory.CreateDbContextAsync(TestContext.Current.CancellationToken) : fixture.Factory.CreateDbContext();

        // Assert
        fixture.AssertKeyBufferWasCleared();

        Assert.True(
            await dbContext.Database.CanConnectAsync(TestContext.Current.CancellationToken));

        await dbContext.Database.CloseConnectionAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreateDbContext_WhenConnectionStringIsInvalid_ClearsAcquiredKeyBufferAndDoesNotExposeKey(
        bool useAsyncFactory)
    {
        // Arrange
        using var fixture =
            new FactoryTestFixture(
                invalidConnectionString: true);

        // Act
        Exception exception;

        if (useAsyncFactory)
        {
            exception =
                await Assert.ThrowsAsync<ArgumentException>(
                    () => fixture.Factory.CreateDbContextAsync(TestContext.Current.CancellationToken));
        }
        else
        {
            exception =
                Assert.Throws<ArgumentException>(
                    () => fixture.Factory.CreateDbContext());
        }

        // Assert
        fixture.AssertKeyBufferWasCleared();

        Assert.DoesNotContain(
            Convert.ToBase64String(
                fixture.ServiceBackingKey),
            exception.ToString(),
            StringComparison.Ordinal);
    }

    private static async Task CreateEncryptedDatabaseAsync(
        string databasePath,
        byte[] databaseKey)
    {
        // Arrange the source database for DeskVault's encryption path.
        await using (var connection =
            new SqliteConnection(
                $"Data Source={databasePath};Pooling=False"))
        {
            await connection.OpenAsync();

            await using var command =
                connection.CreateCommand();

            command.CommandText =
                """
                CREATE TABLE FactoryTest (
                    Id INTEGER PRIMARY KEY
                );
                """;

            await command.ExecuteNonQueryAsync();
        }

        // Act: use the existing database encryption implementation.
        var migrator =
            new SqliteDatabaseEncryptionMigrator();

        await migrator.MigrateAsync(
            databasePath,
            databaseKey);
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

    private sealed class FactoryTestFixture : IDisposable
    {
        private readonly string _temporaryDirectory;
        private readonly List<byte[]> _acquiredKeyBuffers = [];

        public byte[] ServiceBackingKey { get; }

        private byte[] ExpectedBackingKey { get; }

        public DeskVaultDataPaths DataPaths { get; }

        public Mock<IDatabaseEncryptionKeyService> KeyService { get; }

        public EncryptedDeskVaultDbContextFactory Factory { get; }

        public FactoryTestFixture(
            bool invalidConnectionString = false)
        {
            SQLitePCL.Batteries_V2.Init();

            _temporaryDirectory =
                CreateTemporaryDirectory();

            string rootDirectory =
                invalidConnectionString
                    ? $"{_temporaryDirectory};UnsupportedKeyword=invalid"
                    : _temporaryDirectory;

            ServiceBackingKey =
                RandomNumberGenerator.GetBytes(
                    EncryptionKeyMaterial.KeySizeBytes);

            ExpectedBackingKey =
                ServiceBackingKey.ToArray();

            KeyService =
                CreateKeyService(
                    ServiceBackingKey,
                    _acquiredKeyBuffers);

            DataPaths =
                new DeskVaultDataPaths(
                    rootDirectory);

            Factory =
                new EncryptedDeskVaultDbContextFactory(
                    DataPaths,
                    KeyService.Object);
        }

        public void AssertKeyBufferWasCleared()
        {
            KeyService.Verify(
                service => service.GetKeyAsync(
                    It.IsAny<CancellationToken>()),
                Times.Once);

            byte[] acquiredKeyBuffer =
                Assert.Single(
                    _acquiredKeyBuffers);

            Assert.All(
                acquiredKeyBuffer,
                value => Assert.Equal(
                    (byte)0,
                    value));

            // The factory must not clear independent service-owned state.
            Assert.Equal(
                ExpectedBackingKey,
                ServiceBackingKey);
        }

        public void Dispose()
        {
            foreach (byte[] acquiredKeyBuffer in _acquiredKeyBuffers)
            {
                CryptographicOperations.ZeroMemory(
                    acquiredKeyBuffer);
            }

            CryptographicOperations.ZeroMemory(
                ServiceBackingKey);

            CryptographicOperations.ZeroMemory(
                ExpectedBackingKey);

            DeleteTemporaryDirectory(
                _temporaryDirectory);
        }

        private static Mock<IDatabaseEncryptionKeyService> CreateKeyService(
            byte[] serviceBackingKey,
            ICollection<byte[]> acquiredKeyBuffers)
        {
            var keyService =
                new Mock<IDatabaseEncryptionKeyService>(
                    MockBehavior.Strict);

            Guid keyId = Guid.NewGuid();

            keyService
                .Setup(service => service.GetKeyAsync(
                    It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    // Each retrieval returns a separate caller-owned buffer.
                    byte[] acquiredKeyBuffer =
                        serviceBackingKey.ToArray();

                    acquiredKeyBuffers.Add(
                        acquiredKeyBuffer);

                    return Task.FromResult(
                        new EncryptionKeyMaterial(
                            acquiredKeyBuffer,
                            keyId,
                            keyVersion: 1));
                });

            return keyService;
        }
    }
}
