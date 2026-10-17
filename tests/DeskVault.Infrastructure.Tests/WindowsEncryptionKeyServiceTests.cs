using System.Security.Cryptography;
using DeskVault.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeskVault.Infrastructure.Tests;

public sealed class WindowsEncryptionKeyServiceTests
{
    [Fact]
    public async Task GetOrCreateKeyAsync_WhenKeyDoesNotExist_CreatesAndPersistsKey()
    {
        // Arrange
        string rootDirectory =
            CreateTemporaryDirectory();

        EncryptionKeyMaterial? keyMaterial = null;
        byte[]? persistedKey = null;

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(
                    rootDirectory);

            var service =
                CreateService(dataPaths);

            // Act
            keyMaterial =
                await service.GetOrCreateKeyAsync();

            string keyFilePath =
                GetKeyFilePath(dataPaths);

            persistedKey =
                await File.ReadAllBytesAsync(
                    keyFilePath);

            // Assert
            Assert.Equal(
                32,
                keyMaterial.KeyBytes.Length);

            Assert.NotEqual(
                Guid.Empty,
                keyMaterial.KeyId);

            Assert.Equal(
                1,
                keyMaterial.KeyVersion);

            Assert.True(
                File.Exists(keyFilePath));

            Assert.NotEmpty(
                persistedKey);

            Assert.NotEqual(
                keyMaterial.KeyBytes,
                persistedKey);
        }
        finally
        {
            if (keyMaterial is not null)
            {
                CryptographicOperations.ZeroMemory(
                    keyMaterial.KeyBytes);
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
    public async Task GetOrCreateKeyAsync_WhenKeyExists_ReturnsSameKey()
    {
        // Arrange
        string rootDirectory =
            CreateTemporaryDirectory();

        EncryptionKeyMaterial? firstKey = null;
        EncryptionKeyMaterial? secondKey = null;

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(
                    rootDirectory);

            var service =
                CreateService(dataPaths);

            // Act
            firstKey =
                await service.GetOrCreateKeyAsync();

            secondKey =
                await service.GetOrCreateKeyAsync();

            // Assert
            Assert.Equal(
                firstKey.KeyBytes,
                secondKey.KeyBytes);

            Assert.Equal(
                firstKey.KeyId,
                secondKey.KeyId);

            Assert.Equal(
                firstKey.KeyVersion,
                secondKey.KeyVersion);
        }
        finally
        {
            if (firstKey is not null)
            {
                CryptographicOperations.ZeroMemory(
                    firstKey.KeyBytes);
            }

            if (secondKey is not null)
            {
                CryptographicOperations.ZeroMemory(
                    secondKey.KeyBytes);
            }

            DeleteTemporaryDirectory(
                rootDirectory);
        }
    }

    [Fact]
    public async Task GetKeyAsync_WhenKeyExists_ReturnsSameKey()
    {
        // Arrange
        string rootDirectory =
            CreateTemporaryDirectory();

        EncryptionKeyMaterial? provisionedKey = null;
        EncryptionKeyMaterial? retrievedKey = null;
        EncryptionKeyMaterial? secondRetrievedKey = null;

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(
                    rootDirectory);

            var service =
                CreateService(dataPaths);

            // Act
            provisionedKey =
                await service.GetOrCreateKeyAsync();

            retrievedKey =
                await service.GetKeyAsync();

            secondRetrievedKey =
                await service.GetKeyAsync();

            // Assert
            Assert.Equal(
                provisionedKey.KeyBytes,
                retrievedKey.KeyBytes);

            Assert.Equal(
                provisionedKey.KeyId,
                retrievedKey.KeyId);

            Assert.Equal(
                provisionedKey.KeyVersion,
                retrievedKey.KeyVersion);

            Assert.Equal(
                retrievedKey.KeyBytes,
                secondRetrievedKey.KeyBytes);

            Assert.Equal(
                retrievedKey.KeyId,
                secondRetrievedKey.KeyId);

            Assert.Equal(
                retrievedKey.KeyVersion,
                secondRetrievedKey.KeyVersion);
        }
        finally
        {
            if (provisionedKey is not null)
            {
                CryptographicOperations.ZeroMemory(
                    provisionedKey.KeyBytes);
            }

            if (retrievedKey is not null)
            {
                CryptographicOperations.ZeroMemory(
                    retrievedKey.KeyBytes);
            }

            if (secondRetrievedKey is not null)
            {
                CryptographicOperations.ZeroMemory(
                    secondRetrievedKey.KeyBytes);
            }

            DeleteTemporaryDirectory(
                rootDirectory);
        }
    }

    [Fact]
    public async Task GetKeyAsync_WhenDatabaseKeyArtifactIsCopiedToDocumentKeyPath_RejectsMismatchedPurposeWithoutModifyingArtifacts()
    {
        string rootDirectory =
            CreateTemporaryDirectory();

        EncryptionKeyMaterial? databaseKey = null;
        byte[]? databaseArtifact = null;
        byte[]? documentArtifactBeforeRead = null;
        byte[]? documentArtifactAfterRead = null;
        byte[]? databaseArtifactAfterRead = null;

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(rootDirectory);

            var databaseService =
                new WindowsDatabaseEncryptionKeyService(
                    dataPaths,
                    NullLogger<WindowsDatabaseEncryptionKeyService>.Instance);

            var documentService =
                CreateService(dataPaths);

            databaseKey =
                await databaseService.GetOrCreateKeyAsync();

            string databaseKeyPath =
                Path.Combine(
                    dataPaths.SecurityDirectory,
                    "database.key");

            string documentKeyPath =
                GetKeyFilePath(dataPaths);

            databaseArtifact =
                await File.ReadAllBytesAsync(
                    databaseKeyPath);

            // Copy the protected database-key artifact to the document-key path.
            await File.WriteAllBytesAsync(
                documentKeyPath,
                databaseArtifact);

            documentArtifactBeforeRead =
                await File.ReadAllBytesAsync(
                    documentKeyPath);

            // A document-key service must reject a database-purpose payload.
            await Assert.ThrowsAsync<CryptographicException>(
                () => documentService.GetKeyAsync());

            // Failed retrieval must not rewrite either artifact.
            documentArtifactAfterRead =
                await File.ReadAllBytesAsync(
                    documentKeyPath);

            databaseArtifactAfterRead =
                await File.ReadAllBytesAsync(
                    databaseKeyPath);

            Assert.Equal(
                documentArtifactBeforeRead,
                documentArtifactAfterRead);

            Assert.Equal(
                databaseArtifact,
                databaseArtifactAfterRead);
        }
        finally
        {
            if (databaseKey is not null)
            {
                CryptographicOperations.ZeroMemory(
                    databaseKey.KeyBytes);
            }

            if (databaseArtifact is not null)
            {
                CryptographicOperations.ZeroMemory(
                    databaseArtifact);
            }

            if (documentArtifactBeforeRead is not null)
            {
                CryptographicOperations.ZeroMemory(
                    documentArtifactBeforeRead);
            }

            if (documentArtifactAfterRead is not null)
            {
                CryptographicOperations.ZeroMemory(
                    documentArtifactAfterRead);
            }

            if (databaseArtifactAfterRead is not null)
            {
                CryptographicOperations.ZeroMemory(
                    databaseArtifactAfterRead);
            }

            DeleteTemporaryDirectory(
                rootDirectory);
        }
    }

    [Fact]
    public async Task GetKeyAsync_WhenKeyIsMissing_ThrowsWithoutCreatingKey()
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

            string keyFilePath =
                GetKeyFilePath(dataPaths);

            Assert.False(
                Directory.Exists(
                    dataPaths.SecurityDirectory));

            await Assert.ThrowsAsync<CryptographicException>(
                () =>
                    service.GetKeyAsync());

            Assert.False(
                Directory.Exists(
                    dataPaths.SecurityDirectory));

            Assert.False(
                File.Exists(keyFilePath));
        }
        finally
        {
            DeleteTemporaryDirectory(
                rootDirectory);
        }
    }

    [Fact]
    public async Task GetOrCreateKeyAsync_WhenProtectedKeyCannotBeUnprotected_ThrowsWithoutModifyingKey()
    {
        string rootDirectory =
            CreateTemporaryDirectory();

        byte[] invalidProtectedKey =
            [1, 2, 3, 4, 5, 6, 7, 8];

        byte[]? storedBytesBeforeRead = null;
        byte[]? storedBytesAfterRead = null;

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(
                    rootDirectory);

            Directory.CreateDirectory(
                dataPaths.SecurityDirectory);

            string keyFilePath =
                GetKeyFilePath(dataPaths);

            await File.WriteAllBytesAsync(
                keyFilePath,
                invalidProtectedKey);

            storedBytesBeforeRead =
                await File.ReadAllBytesAsync(
                    keyFilePath);

            var service =
                CreateService(dataPaths);

            await Assert.ThrowsAsync<CryptographicException>(
                () =>
                    service.GetOrCreateKeyAsync());

            storedBytesAfterRead =
                await File.ReadAllBytesAsync(
                    keyFilePath);

            Assert.True(
                File.Exists(keyFilePath));

            Assert.Equal(
                storedBytesBeforeRead,
                storedBytesAfterRead);

            Assert.Equal(
                new[] { keyFilePath },
                Directory.GetFiles(
                    dataPaths.SecurityDirectory));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                invalidProtectedKey);

            if (storedBytesBeforeRead is not null)
            {
                CryptographicOperations.ZeroMemory(
                    storedBytesBeforeRead);
            }

            if (storedBytesAfterRead is not null)
            {
                CryptographicOperations.ZeroMemory(
                    storedBytesAfterRead);
            }

            DeleteTemporaryDirectory(
                rootDirectory);
        }
    }

    [Fact]
    public async Task GetKeyAsync_WhenProtectedKeyCannotBeUnprotected_ThrowsWithoutModifyingKey()
    {
        string rootDirectory =
            CreateTemporaryDirectory();

        byte[] invalidProtectedKey =
            [1, 2, 3, 4, 5, 6, 7, 8];

        byte[]? storedBytes = null;

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(
                    rootDirectory);

            Directory.CreateDirectory(
                dataPaths.SecurityDirectory);

            string keyFilePath =
                GetKeyFilePath(dataPaths);

            await File.WriteAllBytesAsync(
                keyFilePath,
                invalidProtectedKey);

            storedBytes =
                await File.ReadAllBytesAsync(
                    keyFilePath);

            var service =
                CreateService(dataPaths);

            await Assert.ThrowsAnyAsync<CryptographicException>(
                () =>
                    service.GetKeyAsync());

            Assert.Equal(
                storedBytes,
                await File.ReadAllBytesAsync(
                    keyFilePath));

            Assert.Single(
                Directory.GetFiles(
                    dataPaths.SecurityDirectory));
        }
        finally
        {
            if (storedBytes is not null)
            {
                CryptographicOperations.ZeroMemory(
                    storedBytes);
            }

            CryptographicOperations.ZeroMemory(
                invalidProtectedKey);

            DeleteTemporaryDirectory(
                rootDirectory);
        }
    }

    [Fact]
    public async Task GetKeyAsync_WhenUnprotectedKeyHasInvalidLength_ThrowsWithoutModifyingKey()
    {
        string rootDirectory =
            CreateTemporaryDirectory();

        byte[] invalidKey =
            RandomNumberGenerator.GetBytes(16);

        byte[] protectedKey =
            ProtectedData.Protect(
                invalidKey,
                null,
                DataProtectionScope.CurrentUser);

        byte[]? storedBytes = null;

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(
                    rootDirectory);

            Directory.CreateDirectory(
                dataPaths.SecurityDirectory);

            string keyFilePath =
                GetKeyFilePath(dataPaths);

            await File.WriteAllBytesAsync(
                keyFilePath,
                protectedKey);

            storedBytes =
                await File.ReadAllBytesAsync(
                    keyFilePath);

            var service =
                CreateService(dataPaths);

            await Assert.ThrowsAsync<CryptographicException>(
                () =>
                    service.GetKeyAsync());

            Assert.Equal(
                storedBytes,
                await File.ReadAllBytesAsync(
                    keyFilePath));

            Assert.Single(
                Directory.GetFiles(
                    dataPaths.SecurityDirectory));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                invalidKey);

            CryptographicOperations.ZeroMemory(
                protectedKey);

            if (storedBytes is not null)
            {
                CryptographicOperations.ZeroMemory(
                    storedBytes);
            }

            DeleteTemporaryDirectory(
                rootDirectory);
        }
    }

    [Fact]
    public async Task GetKeyAsync_WhenKeyFileIsUnreadable_ThrowsWithoutModifyingKey()
    {
        // Arrange
        string rootDirectory =
            CreateTemporaryDirectory();

        EncryptionKeyMaterial? provisionedKey = null;
        byte[]? storedBytesBeforeRead = null;
        byte[]? storedBytesAfterRead = null;

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(rootDirectory);

            var service =
                CreateService(dataPaths);

            provisionedKey =
                await service.GetOrCreateKeyAsync();

            string keyFilePath =
                GetKeyFilePath(dataPaths);

            storedBytesBeforeRead =
                await File.ReadAllBytesAsync(
                    keyFilePath);

            // Act: hold an exclusive lock so retrieval cannot read the file.
            await using (
                var fileLock = new FileStream(
                    keyFilePath,
                    FileMode.Open,
                    FileAccess.ReadWrite,
                    FileShare.None))
            {
                await Assert.ThrowsAsync<IOException>(
                    () => service.GetKeyAsync());
            }

            storedBytesAfterRead =
                await File.ReadAllBytesAsync(
                    keyFilePath);

            // Assert: failed strict retrieval leaves canonical state unchanged.
            Assert.True(
                File.Exists(keyFilePath));

            Assert.Equal(
                storedBytesBeforeRead,
                storedBytesAfterRead);

            Assert.Empty(
                Directory.GetFiles(
                    dataPaths.SecurityDirectory,
                    "master.key.*.tmp"));
        }
        finally
        {
            if (provisionedKey is not null)
            {
                CryptographicOperations.ZeroMemory(
                    provisionedKey.KeyBytes);
            }

            if (storedBytesBeforeRead is not null)
            {
                CryptographicOperations.ZeroMemory(
                    storedBytesBeforeRead);
            }

            if (storedBytesAfterRead is not null)
            {
                CryptographicOperations.ZeroMemory(
                    storedBytesAfterRead);
            }

            DeleteTemporaryDirectory(
                rootDirectory);
        }
    }

    [Fact]
    public async Task GetKeyAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        string rootDirectory =
            CreateTemporaryDirectory();

        EncryptionKeyMaterial? provisionedKey = null;
        byte[]? artifactBeforeCancellation = null;
        byte[]? artifactAfterCancellation = null;

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(
                    rootDirectory);

            var service =
                CreateService(dataPaths);

            provisionedKey =
                await service.GetOrCreateKeyAsync();

            string keyFilePath =
                GetKeyFilePath(dataPaths);

            artifactBeforeCancellation =
                await File.ReadAllBytesAsync(
                    keyFilePath);

            using var cancellationTokenSource =
                new CancellationTokenSource();

            cancellationTokenSource.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () =>
                    service.GetKeyAsync(
                        cancellationTokenSource.Token));

            artifactAfterCancellation =
                await File.ReadAllBytesAsync(
                    keyFilePath);

            Assert.Equal(
                artifactBeforeCancellation,
                artifactAfterCancellation);

            Assert.Equal(
                new[] { keyFilePath },
                Directory.GetFiles(
                    dataPaths.SecurityDirectory));
        }
        finally
        {
            if (provisionedKey is not null)
            {
                CryptographicOperations.ZeroMemory(
                    provisionedKey.KeyBytes);
            }

            if (artifactBeforeCancellation is not null)
            {
                CryptographicOperations.ZeroMemory(
                    artifactBeforeCancellation);
            }

            if (artifactAfterCancellation is not null)
            {
                CryptographicOperations.ZeroMemory(
                    artifactAfterCancellation);
            }

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

        EncryptionKeyMaterial[]? keyMaterials = null;
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

            Task<EncryptionKeyMaterial>[] initializationTasks =
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

            keyMaterials =
                await Task.WhenAll(
                    initializationTasks);

            // Assert
            Assert.Equal(
                ConcurrentCallCount,
                keyMaterials.Length);

            EncryptionKeyMaterial expectedMaterial =
                keyMaterials[0];

            foreach (EncryptionKeyMaterial keyMaterial in keyMaterials)
            {
                Assert.Equal(
                    expectedMaterial.KeyBytes,
                    keyMaterial.KeyBytes);

                Assert.Equal(
                    expectedMaterial.KeyId,
                    keyMaterial.KeyId);

                Assert.Equal(
                    expectedMaterial.KeyVersion,
                    keyMaterial.KeyVersion);
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

            byte[]? unprotectedPayload = null;

            try
            {
                unprotectedPayload =
                    ProtectedData.Unprotect(
                        protectedKeyFromFile,
                        null,
                        DataProtectionScope.CurrentUser);

                EncryptionKeyMaterial persistedMaterial =
                    ProtectedEncryptionKeyPayload.Decode(
                        unprotectedPayload,
                        protectedKeyFromFile,
                        EncryptionKeyPurpose.Document);

                persistedKey =
                    persistedMaterial.KeyBytes;

                Assert.Equal(
                    expectedMaterial.KeyBytes,
                    persistedMaterial.KeyBytes);

                Assert.Equal(
                    expectedMaterial.KeyId,
                    persistedMaterial.KeyId);

                Assert.Equal(
                    expectedMaterial.KeyVersion,
                    persistedMaterial.KeyVersion);
            }
            finally
            {
                if (unprotectedPayload is not null)
                {
                    CryptographicOperations.ZeroMemory(
                        unprotectedPayload);
                }

                CryptographicOperations.ZeroMemory(
                    protectedKeyFromFile);
            }
        }
        finally
        {
            if (keyMaterials is not null)
            {
                foreach (EncryptionKeyMaterial keyMaterial in keyMaterials)
                {
                    CryptographicOperations.ZeroMemory(
                        keyMaterial.KeyBytes);
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
        // Arrange
        string rootDirectory =
            CreateTemporaryDirectory();

        EncryptionKeyMaterial? firstKey = null;
        EncryptionKeyMaterial? secondKey = null;

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(
                    rootDirectory);

            var service =
                CreateService(dataPaths);

            firstKey =
                await service.GetOrCreateKeyAsync();

            string keyFilePath =
                GetKeyFilePath(dataPaths);

            File.Delete(keyFilePath);

            // Act
            secondKey =
                await service.GetOrCreateKeyAsync();

            // Assert
            Assert.Equal(
                32,
                secondKey.KeyBytes.Length);

            Assert.False(
                firstKey.KeyBytes.SequenceEqual(
                    secondKey.KeyBytes));

            Assert.NotEqual(
                firstKey.KeyId,
                secondKey.KeyId);

            Assert.Equal(
                1,
                secondKey.KeyVersion);

            Assert.True(
                File.Exists(keyFilePath));
        }
        finally
        {
            if (firstKey is not null)
            {
                CryptographicOperations.ZeroMemory(
                    firstKey.KeyBytes);
            }

            if (secondKey is not null)
            {
                CryptographicOperations.ZeroMemory(
                    secondKey.KeyBytes);
            }

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

            Task<EncryptionKeyMaterial>[] initializationTasks =
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

    [Fact]
    public async Task GetKeyAsync_WhenLegacyKeyExists_ReturnsKeyWithoutRewritingArtifact()
    {
        // Arrange
        string rootDirectory =
            CreateTemporaryDirectory();

        byte[] legacyKey =
            RandomNumberGenerator.GetBytes(32);

        byte[] protectedLegacyArtifact =
            ProtectedData.Protect(
                legacyKey,
                null,
                DataProtectionScope.CurrentUser);

        EncryptionKeyMaterial? retrievedKey = null;
        byte[]? persistedArtifactAfterRead = null;

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(
                    rootDirectory);

            Directory.CreateDirectory(
                dataPaths.SecurityDirectory);

            string keyFilePath =
                GetKeyFilePath(dataPaths);

            await File.WriteAllBytesAsync(
                keyFilePath,
                protectedLegacyArtifact);

            var service = CreateService(dataPaths);

            // Act
            retrievedKey =
                await service.GetKeyAsync();

            persistedArtifactAfterRead =
                await File.ReadAllBytesAsync(
                    keyFilePath);

            // Assert
            Assert.Equal(
                legacyKey,
                retrievedKey.KeyBytes);

            Assert.NotEqual(
                Guid.Empty,
                retrievedKey.KeyId);

            Assert.Equal(
                0,
                retrievedKey.KeyVersion);

            Assert.Equal(
                protectedLegacyArtifact,
                persistedArtifactAfterRead);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                legacyKey);

            CryptographicOperations.ZeroMemory(
                protectedLegacyArtifact);

            if (retrievedKey is not null)
            {
                CryptographicOperations.ZeroMemory(
                    retrievedKey.KeyBytes);
            }

            if (persistedArtifactAfterRead is not null)
            {
                CryptographicOperations.ZeroMemory(
                    persistedArtifactAfterRead);
            }

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
