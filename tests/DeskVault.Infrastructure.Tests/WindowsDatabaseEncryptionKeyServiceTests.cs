using System.Security.Cryptography;
using DeskVault.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeskVault.Infrastructure.Tests;

public sealed class WindowsDatabaseEncryptionKeyServiceTests
{
    [Fact]
    public async Task GetOrCreateKeyAsync_WhenKeyDoesNotExist_CreatesAndPersistsProtectedKey()
    {
        // Arrange
        string rootDirectory =
            CreateTemporaryDirectory();

        EncryptionKeyMaterial? keyMaterial = null;
        byte[]? persistedProtectedKey = null;

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(rootDirectory);

            var service =
                CreateService(dataPaths);

            // Act
            keyMaterial =
                await service.GetOrCreateKeyAsync();

            string keyFilePath =
                GetDatabaseKeyFilePath(dataPaths);

            persistedProtectedKey =
                await File.ReadAllBytesAsync(keyFilePath);

            // Assert
            Assert.Equal(32, keyMaterial.KeyBytes.Length);
            Assert.NotEqual(Guid.Empty, keyMaterial.KeyId);
            Assert.Equal(1, keyMaterial.KeyVersion);
            Assert.True(File.Exists(keyFilePath));
            Assert.NotEmpty(persistedProtectedKey);

            Assert.False(
                keyMaterial.KeyBytes.SequenceEqual(
                    persistedProtectedKey));
        }
        finally
        {
            if (keyMaterial is not null)
            {
                CryptographicOperations.ZeroMemory(
                    keyMaterial.KeyBytes);
            }

            if (persistedProtectedKey is not null)
            {
                CryptographicOperations.ZeroMemory(
                    persistedProtectedKey);
            }

            DeleteTemporaryDirectory(rootDirectory);
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
                new DeskVaultDataPaths(rootDirectory);

            var service =
                CreateService(dataPaths);

            // Act
            firstKey =
                await service.GetOrCreateKeyAsync();

            secondKey =
                await service.GetOrCreateKeyAsync();

            // Assert
            Assert.Equal(firstKey.KeyBytes, secondKey.KeyBytes);
            Assert.Equal(firstKey.KeyId, secondKey.KeyId);
            Assert.Equal(firstKey.KeyVersion, secondKey.KeyVersion);
        }
        finally
        {
            if (firstKey is not null)
            {
                CryptographicOperations.ZeroMemory(firstKey.KeyBytes);
            }

            if (secondKey is not null)
            {
                CryptographicOperations.ZeroMemory(secondKey.KeyBytes);
            }

            DeleteTemporaryDirectory(rootDirectory);
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
                new DeskVaultDataPaths(rootDirectory);

            var service =
                CreateService(dataPaths);

            firstKey =
                await service.GetOrCreateKeyAsync();

            string keyFilePath =
                GetDatabaseKeyFilePath(dataPaths);

            File.Delete(keyFilePath);

            // Act
            secondKey =
                await service.GetOrCreateKeyAsync();

            // Assert
            Assert.Equal(32, secondKey.KeyBytes.Length);
            Assert.False(firstKey.KeyBytes.SequenceEqual(secondKey.KeyBytes));
            Assert.NotEqual(firstKey.KeyId, secondKey.KeyId);
            Assert.Equal(1, secondKey.KeyVersion);
            Assert.True(File.Exists(keyFilePath));
        }
        finally
        {
            if (firstKey is not null)
            {
                CryptographicOperations.ZeroMemory(firstKey.KeyBytes);
            }

            if (secondKey is not null)
            {
                CryptographicOperations.ZeroMemory(secondKey.KeyBytes);
            }

            DeleteTemporaryDirectory(rootDirectory);
        }
    }

    [Fact]
    public async Task GetKeyAsync_WhenKeyExists_ReturnsSameKey()
    {
        // Arrange
        string rootDirectory =
            CreateTemporaryDirectory();

        EncryptionKeyMaterial? originalKey = null;
        EncryptionKeyMaterial? retrievedKey = null;
        EncryptionKeyMaterial? secondRetrievedKey = null;

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(rootDirectory);

            var service =
                CreateService(dataPaths);

            originalKey =
                await service.GetOrCreateKeyAsync();

            // Act
            retrievedKey =
                await service.GetKeyAsync();

            secondRetrievedKey =
                await service.GetKeyAsync();

            // Assert
            Assert.Equal(
                originalKey.KeyBytes,
                retrievedKey.KeyBytes);

            Assert.Equal(
                originalKey.KeyId,
                retrievedKey.KeyId);

            Assert.Equal(
                originalKey.KeyVersion,
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
            if (originalKey is not null)
            {
                CryptographicOperations.ZeroMemory(originalKey.KeyBytes);
            }

            if (retrievedKey is not null)
            {
                CryptographicOperations.ZeroMemory(retrievedKey.KeyBytes);
            }

            if (secondRetrievedKey is not null)
            {
                CryptographicOperations.ZeroMemory(
                    secondRetrievedKey.KeyBytes);
            }

            DeleteTemporaryDirectory(rootDirectory);
        }
    }

    [Fact]
    public async Task GetKeyAsync_WhenDocumentKeyArtifactIsCopiedToDatabaseKeyPath_RejectsMismatchedPurposeWithoutModifyingArtifacts()
    {
        string rootDirectory =
            CreateTemporaryDirectory();

        EncryptionKeyMaterial? documentKey = null;
        byte[]? documentArtifact = null;
        byte[]? databaseArtifactBeforeRead = null;
        byte[]? documentArtifactAfterRead = null;
        byte[]? databaseArtifactAfterRead = null;

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(rootDirectory);

            var documentService =
                new WindowsEncryptionKeyService(
                    dataPaths,
                    NullLogger<WindowsEncryptionKeyService>.Instance);

            var databaseService =
                CreateService(dataPaths);

            documentKey =
                await documentService.GetOrCreateKeyAsync();

            string documentKeyPath =
                GetDocumentKeyFilePath(dataPaths);

            string databaseKeyPath =
                GetDatabaseKeyFilePath(dataPaths);

            documentArtifact =
                await File.ReadAllBytesAsync(
                    documentKeyPath);

            // Copy the protected document-key artifact to the database-key path.
            await File.WriteAllBytesAsync(
                databaseKeyPath,
                documentArtifact);

            databaseArtifactBeforeRead =
                await File.ReadAllBytesAsync(
                    databaseKeyPath);

            // A database-key service must reject a document-purpose payload.
            await Assert.ThrowsAsync<CryptographicException>(
                () => databaseService.GetKeyAsync());

            // Failed retrieval must not rewrite either artifact.
            documentArtifactAfterRead =
                await File.ReadAllBytesAsync(
                    documentKeyPath);

            databaseArtifactAfterRead =
                await File.ReadAllBytesAsync(
                    databaseKeyPath);

            Assert.Equal(
                documentArtifact,
                documentArtifactAfterRead);

            Assert.Equal(
                databaseArtifactBeforeRead,
                databaseArtifactAfterRead);
        }
        finally
        {
            if (documentKey is not null)
            {
                CryptographicOperations.ZeroMemory(
                    documentKey.KeyBytes);
            }

            if (documentArtifact is not null)
            {
                CryptographicOperations.ZeroMemory(
                    documentArtifact);
            }

            if (databaseArtifactBeforeRead is not null)
            {
                CryptographicOperations.ZeroMemory(
                    databaseArtifactBeforeRead);
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
    public async Task GetKeyAsync_WhenProtectedKeyIsMissing_ThrowsWithoutCreatingReplacementKey()
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

            File.Delete(
                keyFilePath);

            await Assert.ThrowsAsync<CryptographicException>(
                () =>
                    service.GetKeyAsync());

            Assert.False(
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
    public async Task GetKeyAsync_WhenProtectedKeyCannotBeUnprotected_ThrowsWithoutModifyingKey()
    {
        // Arrange
        string rootDirectory =
            CreateTemporaryDirectory();

        byte[] invalidProtectedKey =
            [1, 2, 3, 4, 5, 6, 7, 8];

        byte[]? persistedArtifactBeforeRead = null;
        byte[]? persistedArtifactAfterRead = null;

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(rootDirectory);

            Directory.CreateDirectory(
                dataPaths.SecurityDirectory);

            string keyFilePath =
                GetDatabaseKeyFilePath(dataPaths);

            await File.WriteAllBytesAsync(
                keyFilePath,
                invalidProtectedKey);

            persistedArtifactBeforeRead =
                await File.ReadAllBytesAsync(
                    keyFilePath);

            var service =
                CreateService(dataPaths);

            // Act
            await Assert.ThrowsAsync<CryptographicException>(
                () => service.GetKeyAsync());

            persistedArtifactAfterRead =
                await File.ReadAllBytesAsync(
                    keyFilePath);

            // Assert
            Assert.True(
                File.Exists(keyFilePath));

            Assert.Equal(
                persistedArtifactBeforeRead,
                persistedArtifactAfterRead);

            Assert.Empty(
                Directory.GetFiles(
                    dataPaths.SecurityDirectory,
                    "database.key.*.tmp"));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                invalidProtectedKey);

            if (persistedArtifactBeforeRead is not null)
            {
                CryptographicOperations.ZeroMemory(
                    persistedArtifactBeforeRead);
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

    [Fact]
    public async Task GetKeyAsync_WhenUnprotectedKeyHasInvalidLength_ThrowsWithoutModifyingKey()
    {
        // Arrange
        string rootDirectory =
            CreateTemporaryDirectory();

        byte[] invalidKey =
            RandomNumberGenerator.GetBytes(16);

        byte[] protectedInvalidKey =
            ProtectedData.Protect(
                invalidKey,
                null,
                DataProtectionScope.CurrentUser);

        byte[]? persistedArtifactBeforeRead = null;
        byte[]? persistedArtifactAfterRead = null;

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(rootDirectory);

            Directory.CreateDirectory(
                dataPaths.SecurityDirectory);

            string keyFilePath =
                GetDatabaseKeyFilePath(dataPaths);

            await File.WriteAllBytesAsync(
                keyFilePath,
                protectedInvalidKey);

            persistedArtifactBeforeRead =
                await File.ReadAllBytesAsync(
                    keyFilePath);

            var service =
                CreateService(dataPaths);

            // Act
            await Assert.ThrowsAsync<CryptographicException>(
                () => service.GetKeyAsync());

            persistedArtifactAfterRead =
                await File.ReadAllBytesAsync(
                    keyFilePath);

            // Assert
            Assert.True(
                File.Exists(keyFilePath));

            Assert.Equal(
                persistedArtifactBeforeRead,
                persistedArtifactAfterRead);

            Assert.Empty(
                Directory.GetFiles(
                    dataPaths.SecurityDirectory,
                    "database.key.*.tmp"));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                invalidKey);

            CryptographicOperations.ZeroMemory(
                protectedInvalidKey);

            if (persistedArtifactBeforeRead is not null)
            {
                CryptographicOperations.ZeroMemory(
                    persistedArtifactBeforeRead);
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

    [Fact]
    public async Task GetKeyAsync_WhenKeyFileIsUnreadable_ThrowsWithoutModifyingKey()
    {
        // Arrange
        string rootDirectory =
            CreateTemporaryDirectory();

        EncryptionKeyMaterial? provisionedKey = null;
        byte[]? persistedArtifactBeforeRead = null;
        byte[]? persistedArtifactAfterRead = null;

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(rootDirectory);

            var service =
                CreateService(dataPaths);

            provisionedKey =
                await service.GetOrCreateKeyAsync();

            string keyFilePath =
                GetDatabaseKeyFilePath(dataPaths);

            persistedArtifactBeforeRead =
                await File.ReadAllBytesAsync(
                    keyFilePath);

            // Act
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

            persistedArtifactAfterRead =
                await File.ReadAllBytesAsync(
                    keyFilePath);

            // Assert
            Assert.True(
                File.Exists(keyFilePath));

            Assert.Equal(
                persistedArtifactBeforeRead,
                persistedArtifactAfterRead);

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

            if (persistedArtifactBeforeRead is not null)
            {
                CryptographicOperations.ZeroMemory(
                    persistedArtifactBeforeRead);
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

    [Fact]
    public async Task GetKeyAsync_WhenCancelled_ThrowsOperationCanceledException()
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

            using var cancellationTokenSource =
                new CancellationTokenSource();

            cancellationTokenSource.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () =>
                    service.GetKeyAsync(
                        cancellationTokenSource.Token));
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
        // Arrange
        string rootDirectory =
            CreateTemporaryDirectory();

        EncryptionKeyMaterial? databaseKey = null;
        EncryptionKeyMaterial? documentKey = null;

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(rootDirectory);

            var databaseKeyService =
                CreateService(dataPaths);

            var documentKeyService =
                new WindowsEncryptionKeyService(
                    dataPaths,
                    NullLogger<WindowsEncryptionKeyService>.Instance);

            // Act
            databaseKey =
                await databaseKeyService.GetOrCreateKeyAsync();

            documentKey =
                await documentKeyService.GetOrCreateKeyAsync();

            // Assert
            Assert.Equal(32, databaseKey.KeyBytes.Length);
            Assert.Equal(32, documentKey.KeyBytes.Length);

            Assert.False(
                databaseKey.KeyBytes.SequenceEqual(
                    documentKey.KeyBytes));

            Assert.NotEqual(
                databaseKey.KeyId,
                documentKey.KeyId);

            Assert.Equal(1, databaseKey.KeyVersion);
            Assert.Equal(1, documentKey.KeyVersion);
        }
        finally
        {
            if (databaseKey is not null)
            {
                CryptographicOperations.ZeroMemory(databaseKey.KeyBytes);
            }

            if (documentKey is not null)
            {
                CryptographicOperations.ZeroMemory(documentKey.KeyBytes);
            }

            DeleteTemporaryDirectory(rootDirectory);
        }
    }

    [Fact]
    public async Task GetOrCreateKeyAsync_DoesNotPersistPlaintextDatabaseKey()
    {
        // Arrange
        string rootDirectory =
            CreateTemporaryDirectory();

        EncryptionKeyMaterial? keyMaterial = null;
        byte[]? persistedContents = null;

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(rootDirectory);

            var service =
                CreateService(dataPaths);

            // Act
            keyMaterial =
                await service.GetOrCreateKeyAsync();

            string keyFilePath =
                GetDatabaseKeyFilePath(dataPaths);

            persistedContents =
                await File.ReadAllBytesAsync(keyFilePath);

            // Assert
            Assert.False(
                keyMaterial.KeyBytes.SequenceEqual(
                    persistedContents));
        }
        finally
        {
            if (keyMaterial is not null)
            {
                CryptographicOperations.ZeroMemory(keyMaterial.KeyBytes);
            }

            if (persistedContents is not null)
            {
                CryptographicOperations.ZeroMemory(persistedContents);
            }

            DeleteTemporaryDirectory(rootDirectory);
        }
    }

    [Fact]
    public async Task GetOrCreateKeyAsync_WhenInitializedConcurrently_UsesSingleAuthoritativeKeyAndLeavesNoTemporaryFiles()
    {
        // Arrange
        string rootDirectory =
            CreateTemporaryDirectory();

        EncryptionKeyMaterial[]? initializedKeyMaterials = null;
        byte[]? persistedProtectedKey = null;
        byte[]? unprotectedPayload = null;
        EncryptionKeyMaterial? authoritativeKeyMaterial = null;

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(rootDirectory);

            const int concurrentCallCount = 16;

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

                                    return await CreateService(dataPaths)
                                        .GetOrCreateKeyAsync();
                                }))
                    .ToArray();

            // Act
            startGate.SetResult(true);

            initializedKeyMaterials =
                await Task.WhenAll(initializationTasks);

            // Assert
            string keyFilePath =
                GetDatabaseKeyFilePath(dataPaths);

            Assert.True(File.Exists(keyFilePath));

            string[] keyFiles =
                Directory.GetFiles(dataPaths.SecurityDirectory);

            Assert.Single(keyFiles);
            Assert.Equal(keyFilePath, keyFiles[0]);

            persistedProtectedKey =
                await File.ReadAllBytesAsync(keyFilePath);

            unprotectedPayload =
                ProtectedData.Unprotect(
                    persistedProtectedKey,
                    null,
                    DataProtectionScope.CurrentUser);

            authoritativeKeyMaterial =
                ProtectedEncryptionKeyPayload.Decode(
                    unprotectedPayload,
                    persistedProtectedKey,
                    EncryptionKeyPurpose.Database);

            Assert.Equal(32, authoritativeKeyMaterial.KeyBytes.Length);
            Assert.NotEqual(Guid.Empty, authoritativeKeyMaterial.KeyId);
            Assert.Equal(1, authoritativeKeyMaterial.KeyVersion);

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

            if (persistedProtectedKey is not null)
            {
                CryptographicOperations.ZeroMemory(
                    persistedProtectedKey);
            }

            DeleteTemporaryDirectory(rootDirectory);
        }
    }

    [Fact]
    public async Task GetOrCreateKeyAsync_WhenKeyPublicationFails_CleansTemporaryProtectedKeyFile()
    {
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
                GetDatabaseKeyFilePath(
                    dataPaths);

            Directory.CreateDirectory(
                keyFilePath);

            var service =
                CreateService(
                    dataPaths);

            Exception? exception =
                await Record.ExceptionAsync(
                    () =>
                        service.GetOrCreateKeyAsync());

            Assert.NotNull(exception);

            Assert.True(
                exception is IOException or UnauthorizedAccessException);

            Assert.True(
                Directory.Exists(keyFilePath));

            Assert.False(
                File.Exists(keyFilePath));

            Assert.Empty(
                Directory.GetFiles(
                    dataPaths.SecurityDirectory,
                    "database.key.*.tmp"));
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
                new DeskVaultDataPaths(rootDirectory);

            Directory.CreateDirectory(dataPaths.SecurityDirectory);

            string keyFilePath =
                GetDatabaseKeyFilePath(dataPaths);

            await File.WriteAllBytesAsync(
                keyFilePath,
                protectedLegacyArtifact);

            var service =
                CreateService(dataPaths);

            // Act
            retrievedKey =
                await service.GetKeyAsync();

            persistedArtifactAfterRead =
                await File.ReadAllBytesAsync(keyFilePath);

            // Assert
            Assert.Equal(legacyKey, retrievedKey.KeyBytes);
            Assert.NotEqual(Guid.Empty, retrievedKey.KeyId);
            Assert.Equal(0, retrievedKey.KeyVersion);

            Assert.Equal(
                protectedLegacyArtifact,
                persistedArtifactAfterRead);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(legacyKey);
            CryptographicOperations.ZeroMemory(protectedLegacyArtifact);

            if (retrievedKey is not null)
            {
                CryptographicOperations.ZeroMemory(retrievedKey.KeyBytes);
            }

            if (persistedArtifactAfterRead is not null)
            {
                CryptographicOperations.ZeroMemory(
                    persistedArtifactAfterRead);
            }

            DeleteTemporaryDirectory(rootDirectory);
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
