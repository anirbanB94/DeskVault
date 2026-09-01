using System.Security.Cryptography;
using DeskVault.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeskVault.Infrastructure.Tests;

public sealed class WindowsDatabaseEncryptionKeyServiceTests
{
    [Fact]
    public async Task GetOrCreateKeyAsync_WhenKeyDoesNotExist_CreatesAndPersistsProtectedKey()
    {
        string rootDirectory =
            CreateTemporaryDirectory();

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(
                    rootDirectory);

            var service =
                CreateService(dataPaths);

            byte[] key =
                await service.GetOrCreateKeyAsync();

            Assert.Equal(
                32,
                key.Length);

            string keyFilePath =
                GetDatabaseKeyFilePath(dataPaths);

            Assert.True(
                File.Exists(keyFilePath));

            byte[] persistedProtectedKey =
                await File.ReadAllBytesAsync(
                    keyFilePath);

            Assert.NotEmpty(
                persistedProtectedKey);

            Assert.NotEqual(
                key,
                persistedProtectedKey);
        }
        finally
        {
            DeleteTemporaryDirectory(
                rootDirectory);
        }
    }

    [Fact]
    public async Task GetOrCreateKeyAsync_WhenKeyExists_ReturnsSameKey()
    {
        string rootDirectory =
            CreateTemporaryDirectory();

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(
                    rootDirectory);

            var service =
                CreateService(dataPaths);

            byte[] firstKey =
                await service.GetOrCreateKeyAsync();

            byte[] secondKey =
                await service.GetOrCreateKeyAsync();

            Assert.Equal(
                firstKey,
                secondKey);
        }
        finally
        {
            DeleteTemporaryDirectory(
                rootDirectory);
        }
    }

    [Fact]
    public async Task GetOrCreateKeyAsync_CreatesSecurityDirectory()
    {
        string rootDirectory =
            CreateTemporaryDirectory();

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(
                    rootDirectory);

            var service =
                CreateService(dataPaths);

            Assert.False(
                Directory.Exists(
                    dataPaths.SecurityDirectory));

            await service.GetOrCreateKeyAsync();

            Assert.True(
                Directory.Exists(
                    dataPaths.SecurityDirectory));
        }
        finally
        {
            DeleteTemporaryDirectory(
                rootDirectory);
        }
    }

    [Fact]
    public async Task GetOrCreateKeyAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        string rootDirectory =
            CreateTemporaryDirectory();

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(
                    rootDirectory);

            var service =
                CreateService(dataPaths);

            using var cancellationTokenSource =
                new CancellationTokenSource();

            cancellationTokenSource.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () =>
                    service.GetOrCreateKeyAsync(
                        cancellationTokenSource.Token));
        }
        finally
        {
            DeleteTemporaryDirectory(
                rootDirectory);
        }
    }

    [Fact]
    public async Task GetOrCreateKeyAsync_WhenProtectedKeyIsMissing_CreatesNewKey()
    {
        string rootDirectory =
            CreateTemporaryDirectory();

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(
                    rootDirectory);

            var service =
                CreateService(dataPaths);

            byte[] firstKey =
                await service.GetOrCreateKeyAsync();

            string keyFilePath =
                GetDatabaseKeyFilePath(dataPaths);

            File.Delete(
                keyFilePath);

            byte[] secondKey =
                await service.GetOrCreateKeyAsync();

            Assert.Equal(
                32,
                secondKey.Length);

            Assert.NotEqual(
                firstKey,
                secondKey);

            Assert.True(
                File.Exists(
                    keyFilePath));
        }
        finally
        {
            DeleteTemporaryDirectory(
                rootDirectory);
        }
    }

    [Fact]
    public async Task GetOrCreateKeyAsync_WhenProtectedKeyIsTampered_ThrowsCryptographicException()
    {
        string rootDirectory =
            CreateTemporaryDirectory();

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(
                    rootDirectory);

            var service =
                CreateService(dataPaths);

            await service.GetOrCreateKeyAsync();

            string keyFilePath =
                GetDatabaseKeyFilePath(dataPaths);

            byte[] protectedKey =
                await File.ReadAllBytesAsync(
                    keyFilePath);

            protectedKey[^1] ^= 0xFF;

            await File.WriteAllBytesAsync(
                keyFilePath,
                protectedKey);

            await Assert.ThrowsAsync<CryptographicException>(
                () =>
                    service.GetOrCreateKeyAsync());
        }
        finally
        {
            DeleteTemporaryDirectory(
                rootDirectory);
        }
    }

    [Fact]
    public async Task GetOrCreateKeyAsync_UsesDatabaseSpecificKeyFile()
    {
        string rootDirectory =
            CreateTemporaryDirectory();

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(
                    rootDirectory);

            var service =
                CreateService(dataPaths);

            await service.GetOrCreateKeyAsync();

            string databaseKeyFilePath =
                GetDatabaseKeyFilePath(dataPaths);

            string documentKeyFilePath =
                GetDocumentKeyFilePath(dataPaths);

            Assert.True(
                File.Exists(
                    databaseKeyFilePath));

            Assert.False(
                File.Exists(
                    documentKeyFilePath));
        }
        finally
        {
            DeleteTemporaryDirectory(
                rootDirectory);
        }
    }

    [Fact]
    public async Task GetOrCreateKeyAsync_GeneratesKeyDifferentFromDocumentEncryptionKey()
    {
        string rootDirectory =
            CreateTemporaryDirectory();

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(
                    rootDirectory);

            var databaseKeyService =
                CreateService(dataPaths);

            var documentKeyService =
                new WindowsEncryptionKeyService(
                    dataPaths,
                    NullLogger<WindowsEncryptionKeyService>.Instance);

            byte[] databaseKey =
                await databaseKeyService.GetOrCreateKeyAsync();

            byte[] documentKey =
                await documentKeyService.GetOrCreateKeyAsync();

            Assert.Equal(
                32,
                databaseKey.Length);

            Assert.Equal(
                32,
                documentKey.Length);

            Assert.NotEqual(
                databaseKey,
                documentKey);
        }
        finally
        {
            DeleteTemporaryDirectory(
                rootDirectory);
        }
    }

    [Fact]
    public async Task GetOrCreateKeyAsync_DoesNotPersistPlaintextDatabaseKey()
    {
        string rootDirectory =
            CreateTemporaryDirectory();

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(
                    rootDirectory);

            var service =
                CreateService(dataPaths);

            byte[] key =
                await service.GetOrCreateKeyAsync();

            string keyFilePath =
                GetDatabaseKeyFilePath(dataPaths);

            byte[] persistedContents =
                await File.ReadAllBytesAsync(
                    keyFilePath);

            Assert.NotEqual(
                key,
                persistedContents);
        }
        finally
        {
            DeleteTemporaryDirectory(
                rootDirectory);
        }
    }

    private static WindowsDatabaseEncryptionKeyService CreateService(
        DeskVaultDataPaths dataPaths)
    {
        return new WindowsDatabaseEncryptionKeyService(
            dataPaths,
            NullLogger<WindowsDatabaseEncryptionKeyService>.Instance);
    }

    private static string GetDatabaseKeyFilePath(
        DeskVaultDataPaths dataPaths)
    {
        return Path.Combine(
            dataPaths.SecurityDirectory,
            "database.key");
    }

    private static string GetDocumentKeyFilePath(
        DeskVaultDataPaths dataPaths)
    {
        return Path.Combine(
            dataPaths.SecurityDirectory,
            "master.key");
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
