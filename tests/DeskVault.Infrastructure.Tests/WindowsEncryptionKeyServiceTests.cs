using System.Security.Cryptography;
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
    public async Task GetOrCreateKeyAsync_WhenCalledConcurrently_PersistsOneAuthoritativeKey()
    {
        // Arrange
        string rootDirectory =
            CreateTemporaryDirectory();

        byte[][]? keys = null;
        byte[]? persistedKey = null;

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(
                    rootDirectory);

            const int ConcurrentCallCount = 16;

            WindowsEncryptionKeyService[] services =
                Enumerable.Range(
                    0,
                    ConcurrentCallCount)
                .Select(
                    _ => CreateService(dataPaths))
                .ToArray();

            using var startGate =
                new ManualResetEventSlim(false);

            Task<byte[]>[] initializationTasks =
                services
                    .Select(
                        service =>
                            Task.Run(
                                async () =>
                                {
                                    startGate.Wait();

                                    return await service.GetOrCreateKeyAsync();
                                }))
                    .ToArray();

            // Act
            startGate.Set();

            // Assert
            keys =
                await Task.WhenAll(
                    initializationTasks);

            Assert.Equal(
                ConcurrentCallCount,
                keys.Length);

            byte[] expectedKey =
                keys[0];

            foreach (byte[] key in keys)
            {
                Assert.Equal(
                    expectedKey,
                    key);
            }

            string keyFilePath =
                GetKeyFilePath(dataPaths);

            Assert.True(
                File.Exists(keyFilePath));

            string[] persistedFiles =
                Directory.GetFiles(
                    dataPaths.SecurityDirectory);

            Assert.Single(
                persistedFiles);

            Assert.Equal(
                Path.GetFullPath(keyFilePath),
                Path.GetFullPath(persistedFiles[0]));

            byte[] protectedKeyFromFile =
                await File.ReadAllBytesAsync(
                    keyFilePath);

            try
            {
                persistedKey =
                    ProtectedData.Unprotect(
                        protectedKeyFromFile,
                        null,
                        DataProtectionScope.CurrentUser);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(
                    protectedKeyFromFile);
            }

            Assert.Equal(
                expectedKey,
                persistedKey);
        }
        finally
        {
            if (keys is not null)
            {
                foreach (byte[] key in keys)
                {
                    CryptographicOperations.ZeroMemory(
                        key);
                }
            }

            if (persistedKey is not null)
            {
                CryptographicOperations.ZeroMemory(
                    persistedKey);
            }

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
                GetKeyFilePath(dataPaths);

            File.Delete(keyFilePath);

            byte[] secondKey =
                await service.GetOrCreateKeyAsync();

            Assert.Equal(
                32,
                secondKey.Length);

            Assert.NotEqual(
                firstKey,
                secondKey);

            Assert.True(
                File.Exists(keyFilePath));
        }
        finally
        {
            DeleteTemporaryDirectory(
                rootDirectory);
        }
    }

    [Fact]
    public async Task GetOrCreateKeyAsync_WhenConcurrentPublicationFails_DoesNotLeaveTemporaryKeyFiles()
    {
        // Arrange
        string rootDirectory =
            CreateTemporaryDirectory();

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(
                    rootDirectory);

            Directory.CreateDirectory(
                dataPaths.SecurityDirectory);

            string keyFilePath =
                GetKeyFilePath(dataPaths);

            Directory.CreateDirectory(
                keyFilePath);

            const int ConcurrentCallCount = 2;

            WindowsEncryptionKeyService[] services =
                Enumerable.Range(
                    0,
                    ConcurrentCallCount)
                .Select(
                    _ => CreateService(dataPaths))
                .ToArray();

            using var startGate =
                new ManualResetEventSlim(false);

            Task<byte[]>[] initializationTasks =
                services
                    .Select(
                        service =>
                            Task.Run(
                                async () =>
                                {
                                    startGate.Wait();

                                    return await service.GetOrCreateKeyAsync();
                                }))
                    .ToArray();

            // Act
            startGate.Set();

            Exception?[] failures =
                await Task.WhenAll(
                    initializationTasks.Select(
                        async task =>
                            await Record.ExceptionAsync(
                                async () =>
                                {
                                    await task;
                                })));

            // Assert
            Assert.Equal(
                ConcurrentCallCount,
                failures.Length);

            Assert.All(
                failures,
                failure =>
                {
                    Assert.NotNull(failure);

                    Assert.True(
                        failure is IOException or UnauthorizedAccessException,
                        $"Unexpected failure type: {failure?.GetType().FullName ?? "null"}");
                });

            Assert.True(
                Directory.Exists(keyFilePath));

            Assert.False(
                File.Exists(keyFilePath));

            Assert.Empty(
                Directory.GetFiles(
                    dataPaths.SecurityDirectory));
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
