using DeskVault.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeskVault.Infrastructure.Tests;

public sealed class WindowsEncryptionKeyServiceTests
{
    [Fact]
    public async Task GetOrCreateKeyAsync_WhenKeyDoesNotExist_CreatesAndPersistsKey()
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
                GetKeyFilePath(dataPaths);

            Assert.True(
                File.Exists(keyFilePath));

            byte[] persistedKey =
                await File.ReadAllBytesAsync(
                    keyFilePath);

            Assert.NotEmpty(
                persistedKey);

            Assert.NotEqual(
                key,
                persistedKey);
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

    private static WindowsEncryptionKeyService CreateService(
        DeskVaultDataPaths dataPaths)
    {
        return new WindowsEncryptionKeyService(
            dataPaths,
            NullLogger<WindowsEncryptionKeyService>.Instance);
    }

    private static string GetKeyFilePath(
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
