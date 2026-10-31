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
                await service.GetOrCreateKeyAsync(TestContext.Current.CancellationToken);

            string keyFilePath =
                GetDatabaseKeyFilePath(dataPaths);

            persistedProtectedKey =
                await File.ReadAllBytesAsync(keyFilePath, TestContext.Current.CancellationToken);

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
                await service.GetOrCreateKeyAsync(TestContext.Current.CancellationToken);

            secondKey =
                await service.GetOrCreateKeyAsync(TestContext.Current.CancellationToken);

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
    public async Task GetOrCreateKeyAsync_WhenExistingKeyReadEncountersSharingViolation_RetriesAndReturnsAuthoritativeKey()
    {
        // Arrange
        string rootDirectory =
            CreateTemporaryDirectory();

        EncryptionKeyMaterial? originalKey = null;
        EncryptionKeyMaterial? retrievedKey = null;
        byte[]? persistedArtifactBeforeRead = null;
        byte[]? persistedArtifactAfterRead = null;

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(rootDirectory);

            var provisioningService =
                CreateService(dataPaths);

            originalKey =
                await provisioningService.GetOrCreateKeyAsync(
                    TestContext.Current.CancellationToken);

            string keyFilePath =
                GetDatabaseKeyFilePath(dataPaths);

            persistedArtifactBeforeRead =
                await File.ReadAllBytesAsync(
                    keyFilePath,
                    TestContext.Current.CancellationToken);

            int readAttempts = 0;

            var service =
                new WindowsDatabaseEncryptionKeyService(
                    dataPaths,
                    NullLogger<WindowsDatabaseEncryptionKeyService>.Instance,
                    beforeKeyPublicationAsync: null,
                    readProtectedKeyFileAsync: async (path, cancellationToken) =>
                    {
                        if (Interlocked.Increment(ref readAttempts) == 1)
                        {
                            throw new IOException(
                                "Simulated transient sharing violation.",
                                unchecked((int)0x80070020));
                        }

                        return await File.ReadAllBytesAsync(
                            path,
                            cancellationToken);
                    });

            // Act
            retrievedKey =
                await service.GetOrCreateKeyAsync(
                    TestContext.Current.CancellationToken);

            persistedArtifactAfterRead =
                await File.ReadAllBytesAsync(
                    keyFilePath,
                    TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(2, readAttempts);

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
                persistedArtifactBeforeRead,
                persistedArtifactAfterRead);

            Assert.Equal(
                new[] { keyFilePath },
                Directory.GetFiles(
                    dataPaths.SecurityDirectory));

            Assert.Empty(
                Directory.GetFiles(
                    dataPaths.SecurityDirectory,
                    "database.key.*.tmp"));
        }
        finally
        {
            if (originalKey is not null)
            {
                CryptographicOperations.ZeroMemory(
                    originalKey.KeyBytes);
            }

            if (retrievedKey is not null)
            {
                CryptographicOperations.ZeroMemory(
                    retrievedKey.KeyBytes);
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

            DeleteTemporaryDirectory(rootDirectory);
        }
    }

    [Fact]
    public async Task GetOrCreateKeyAsync_WhenPublicationLoserReadEncountersSharingViolation_RetriesAndReturnsAuthoritativeKey()
    {
        // Arrange
        string rootDirectory =
            CreateTemporaryDirectory();

        EncryptionKeyMaterial? authoritativeKey = null;
        EncryptionKeyMaterial? retrievedKey = null;
        byte[]? persistedArtifactBeforeRead = null;
        byte[]? persistedArtifactAfterRead = null;

        int publicationHookCalls = 0;
        int readAttempts = 0;

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(rootDirectory);

            string keyFilePath =
                GetDatabaseKeyFilePath(dataPaths);

            var service =
                new WindowsDatabaseEncryptionKeyService(
                    dataPaths,
                    NullLogger<WindowsDatabaseEncryptionKeyService>.Instance,
                    beforeKeyPublicationAsync: async cancellationToken =>
                    {
                        Interlocked.Increment(
                            ref publicationHookCalls);

                        // Simulate another initializer winning publication
                        // after this service has already created its candidate.
                        authoritativeKey =
                            await CreateService(dataPaths)
                                .GetOrCreateKeyAsync(cancellationToken);

                        persistedArtifactBeforeRead =
                            await File.ReadAllBytesAsync(
                                keyFilePath,
                                cancellationToken);
                    },
                    readProtectedKeyFileAsync: async (path, cancellationToken) =>
                    {
                        if (Interlocked.Increment(ref readAttempts) == 1)
                        {
                            throw new IOException(
                                "Simulated transient sharing violation.",
                                unchecked((int)0x80070020));
                        }

                        return await File.ReadAllBytesAsync(
                            path,
                            cancellationToken);
                    });

            // Act
            retrievedKey =
                await service.GetOrCreateKeyAsync(
                    TestContext.Current.CancellationToken);

            persistedArtifactAfterRead =
                await File.ReadAllBytesAsync(
                    keyFilePath,
                    TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(1, publicationHookCalls);
            Assert.Equal(2, readAttempts);

            Assert.NotNull(authoritativeKey);

            Assert.Equal(
                authoritativeKey.KeyBytes,
                retrievedKey.KeyBytes);

            Assert.Equal(
                authoritativeKey.KeyId,
                retrievedKey.KeyId);

            Assert.Equal(
                authoritativeKey.KeyVersion,
                retrievedKey.KeyVersion);

            Assert.Equal(
                persistedArtifactBeforeRead,
                persistedArtifactAfterRead);

            Assert.Equal(
                new[] { keyFilePath },
                Directory.GetFiles(
                    dataPaths.SecurityDirectory));

            Assert.Empty(
                Directory.GetFiles(
                    dataPaths.SecurityDirectory,
                    "database.key.*.tmp"));
        }
        finally
        {
            if (authoritativeKey is not null)
            {
                CryptographicOperations.ZeroMemory(
                    authoritativeKey.KeyBytes);
            }

            if (retrievedKey is not null)
            {
                CryptographicOperations.ZeroMemory(
                    retrievedKey.KeyBytes);
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

            DeleteTemporaryDirectory(rootDirectory);
        }
    }

    [Fact]
    public async Task GetOrCreateKeyAsync_WhenCancelledDuringSharingViolationRetry_StopsRetryAndPreservesKey()
    {
        // Arrange
        string rootDirectory =
            CreateTemporaryDirectory();

        EncryptionKeyMaterial? originalKey = null;
        EncryptionKeyMaterial? verifiedKey = null;

        byte[]? persistedArtifactBeforeRead = null;
        byte[]? persistedArtifactAfterRead = null;

        int readAttempts = 0;

        using var cancellationTokenSource =
            new CancellationTokenSource();

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(rootDirectory);

            var provisioningService =
                CreateService(dataPaths);

            originalKey =
                await provisioningService.GetOrCreateKeyAsync(
                    TestContext.Current.CancellationToken);

            string keyFilePath =
                GetDatabaseKeyFilePath(dataPaths);

            persistedArtifactBeforeRead =
                await File.ReadAllBytesAsync(
                    keyFilePath,
                    TestContext.Current.CancellationToken);

            var service =
                new WindowsDatabaseEncryptionKeyService(
                    dataPaths,
                    NullLogger<WindowsDatabaseEncryptionKeyService>.Instance,
                    beforeKeyPublicationAsync: null,
                    readProtectedKeyFileAsync: (_, _) =>
                    {
                        Interlocked.Increment(ref readAttempts);

                        // Cancel while the production retry delay is pending.
                        cancellationTokenSource.CancelAfter(
                            TimeSpan.FromMilliseconds(50));

                        return Task.FromException<byte[]>(
                            new IOException(
                                "Simulated transient sharing violation.",
                                unchecked((int)0x80070020)));
                    });

            // Act
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () =>
                    service.GetOrCreateKeyAsync(
                        cancellationTokenSource.Token));

            persistedArtifactAfterRead =
                await File.ReadAllBytesAsync(
                    keyFilePath,
                    TestContext.Current.CancellationToken);

            // Verify that strict retrieval still returns the original key.
            verifiedKey =
                await CreateService(dataPaths).GetKeyAsync(
                    TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(1, readAttempts);

            Assert.Equal(
                originalKey.KeyBytes,
                verifiedKey.KeyBytes);

            Assert.Equal(
                originalKey.KeyId,
                verifiedKey.KeyId);

            Assert.Equal(
                originalKey.KeyVersion,
                verifiedKey.KeyVersion);

            Assert.Equal(
                persistedArtifactBeforeRead,
                persistedArtifactAfterRead);

            Assert.Equal(
                new[] { keyFilePath },
                Directory.GetFiles(
                    dataPaths.SecurityDirectory));

            Assert.Empty(
                Directory.GetFiles(
                    dataPaths.SecurityDirectory,
                    "database.key.*.tmp"));
        }
        finally
        {
            if (originalKey is not null)
            {
                CryptographicOperations.ZeroMemory(
                    originalKey.KeyBytes);
            }

            if (verifiedKey is not null)
            {
                CryptographicOperations.ZeroMemory(
                    verifiedKey.KeyBytes);
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

            DeleteTemporaryDirectory(rootDirectory);
        }
    }

    [Fact]
    public async Task GetOrCreateKeyAsync_WhenExistingKeyReadEncountersNonSharingIOException_FailsWithoutRetryingOrModifyingKey()
    {
        // Arrange
        string rootDirectory =
            CreateTemporaryDirectory();

        EncryptionKeyMaterial? originalKey = null;
        EncryptionKeyMaterial? verifiedKey = null;

        byte[]? persistedArtifactBeforeRead = null;
        byte[]? persistedArtifactAfterRead = null;

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(rootDirectory);

            var provisioningService =
                CreateService(dataPaths);

            originalKey =
                await provisioningService.GetOrCreateKeyAsync(
                    TestContext.Current.CancellationToken);

            string keyFilePath =
                GetDatabaseKeyFilePath(dataPaths);

            persistedArtifactBeforeRead =
                await File.ReadAllBytesAsync(
                    keyFilePath,
                    TestContext.Current.CancellationToken);

            int readAttempts = 0;

            var expectedException =
                new IOException(
                    "Simulated non-sharing I/O failure.",
                    unchecked((int)0x80070005));

            var service =
                new WindowsDatabaseEncryptionKeyService(
                    dataPaths,
                    NullLogger<WindowsDatabaseEncryptionKeyService>.Instance,
                    beforeKeyPublicationAsync: null,
                    readProtectedKeyFileAsync: (_, _) =>
                    {
                        Interlocked.Increment(ref readAttempts);

                        return Task.FromException<byte[]>(
                            expectedException);
                    });

            // Act
            IOException exception =
                await Assert.ThrowsAsync<IOException>(
                    () =>
                        service.GetOrCreateKeyAsync(
                            TestContext.Current.CancellationToken));

            persistedArtifactAfterRead =
                await File.ReadAllBytesAsync(
                    keyFilePath,
                    TestContext.Current.CancellationToken);

            verifiedKey =
                await CreateService(dataPaths).GetKeyAsync(
                    TestContext.Current.CancellationToken);

            // Assert
            Assert.Same(
                expectedException,
                exception);

            Assert.Equal(1, readAttempts);

            Assert.Equal(
                originalKey.KeyBytes,
                verifiedKey.KeyBytes);

            Assert.Equal(
                originalKey.KeyId,
                verifiedKey.KeyId);

            Assert.Equal(
                originalKey.KeyVersion,
                verifiedKey.KeyVersion);

            Assert.Equal(
                persistedArtifactBeforeRead,
                persistedArtifactAfterRead);

            Assert.Equal(
                new[] { keyFilePath },
                Directory.GetFiles(
                    dataPaths.SecurityDirectory));

            Assert.Empty(
                Directory.GetFiles(
                    dataPaths.SecurityDirectory,
                    "database.key.*.tmp"));
        }
        finally
        {
            if (originalKey is not null)
            {
                CryptographicOperations.ZeroMemory(
                    originalKey.KeyBytes);
            }

            if (verifiedKey is not null)
            {
                CryptographicOperations.ZeroMemory(
                    verifiedKey.KeyBytes);
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

            await service.GetOrCreateKeyAsync(TestContext.Current.CancellationToken);

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
                await service.GetOrCreateKeyAsync(TestContext.Current.CancellationToken);

            string keyFilePath =
                GetDatabaseKeyFilePath(dataPaths);

            File.Delete(keyFilePath);

            // Act
            secondKey =
                await service.GetOrCreateKeyAsync(TestContext.Current.CancellationToken);

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
                await service.GetOrCreateKeyAsync(TestContext.Current.CancellationToken);

            // Act
            retrievedKey =
                await service.GetKeyAsync(TestContext.Current.CancellationToken);

            secondRetrievedKey =
                await service.GetKeyAsync(TestContext.Current.CancellationToken);

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
                await documentService.GetOrCreateKeyAsync(TestContext.Current.CancellationToken);

            string documentKeyPath =
                GetDocumentKeyFilePath(dataPaths);

            string databaseKeyPath =
                GetDatabaseKeyFilePath(dataPaths);

            documentArtifact =
                await File.ReadAllBytesAsync(documentKeyPath, TestContext.Current.CancellationToken);

            // Copy the protected document-key artifact to the database-key path.
            await File.WriteAllBytesAsync(databaseKeyPath, documentArtifact, TestContext.Current.CancellationToken);

            databaseArtifactBeforeRead =
                await File.ReadAllBytesAsync(databaseKeyPath, TestContext.Current.CancellationToken);

            // A database-key service must reject a document-purpose payload.
            await Assert.ThrowsAsync<CryptographicException>(
                () => databaseService.GetKeyAsync(TestContext.Current.CancellationToken));

            // Failed retrieval must not rewrite either artifact.
            documentArtifactAfterRead =
                await File.ReadAllBytesAsync(documentKeyPath, TestContext.Current.CancellationToken);

            databaseArtifactAfterRead =
                await File.ReadAllBytesAsync(databaseKeyPath, TestContext.Current.CancellationToken);

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

            await service.GetOrCreateKeyAsync(TestContext.Current.CancellationToken);

            string keyFilePath =
                GetDatabaseKeyFilePath(dataPaths);

            File.Delete(
                keyFilePath);

            await Assert.ThrowsAsync<CryptographicException>(
                () =>
                    service.GetKeyAsync(TestContext.Current.CancellationToken));

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

            await File.WriteAllBytesAsync(keyFilePath, invalidProtectedKey, TestContext.Current.CancellationToken);

            persistedArtifactBeforeRead =
                await File.ReadAllBytesAsync(keyFilePath, TestContext.Current.CancellationToken);

            var service =
                CreateService(dataPaths);

            // Act
            await Assert.ThrowsAsync<CryptographicException>(
                () => service.GetKeyAsync(TestContext.Current.CancellationToken));

            persistedArtifactAfterRead =
                await File.ReadAllBytesAsync(keyFilePath, TestContext.Current.CancellationToken);

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

            await File.WriteAllBytesAsync(keyFilePath, protectedInvalidKey, TestContext.Current.CancellationToken);

            persistedArtifactBeforeRead =
                await File.ReadAllBytesAsync(keyFilePath, TestContext.Current.CancellationToken);

            var service =
                CreateService(dataPaths);

            // Act
            await Assert.ThrowsAsync<CryptographicException>(
                () => service.GetKeyAsync(TestContext.Current.CancellationToken));

            persistedArtifactAfterRead =
                await File.ReadAllBytesAsync(keyFilePath, TestContext.Current.CancellationToken);

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
                await service.GetOrCreateKeyAsync(TestContext.Current.CancellationToken);

            string keyFilePath =
                GetDatabaseKeyFilePath(dataPaths);

            persistedArtifactBeforeRead =
                await File.ReadAllBytesAsync(keyFilePath, TestContext.Current.CancellationToken);

            // Act
            await using (
                var fileLock = new FileStream(
                    keyFilePath,
                    FileMode.Open,
                    FileAccess.ReadWrite,
                    FileShare.None))
            {
                await Assert.ThrowsAsync<IOException>(
                    () => service.GetKeyAsync(TestContext.Current.CancellationToken));
            }

            persistedArtifactAfterRead =
                await File.ReadAllBytesAsync(keyFilePath, TestContext.Current.CancellationToken);

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
                await service.GetOrCreateKeyAsync(TestContext.Current.CancellationToken);

            string keyFilePath =
                GetDatabaseKeyFilePath(dataPaths);

            artifactBeforeCancellation =
                await File.ReadAllBytesAsync(keyFilePath, TestContext.Current.CancellationToken);

            using var cancellationTokenSource =
                new CancellationTokenSource();

            cancellationTokenSource.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () =>
                    service.GetKeyAsync(
                        cancellationTokenSource.Token));

            artifactAfterCancellation =
                await File.ReadAllBytesAsync(keyFilePath, TestContext.Current.CancellationToken);

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
    public async Task GetOrCreateKeyAsync_WhenProtectedKeyIsTampered_ThrowsWithoutModifyingKey()
    {
        string rootDirectory =
            CreateTemporaryDirectory();

        byte[]? protectedKey = null;
        byte[]? storedBytesBeforeRead = null;
        byte[]? storedBytesAfterRead = null;

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(
                    rootDirectory);

            var service =
                CreateService(dataPaths);

            EncryptionKeyMaterial? provisionedKey = null;

            try
            {
                provisionedKey =
                    await service.GetOrCreateKeyAsync(TestContext.Current.CancellationToken);
            }
            finally
            {
                if (provisionedKey is not null)
                {
                    CryptographicOperations.ZeroMemory(
                        provisionedKey.KeyBytes);
                }
            }

            string keyFilePath =
                GetDatabaseKeyFilePath(dataPaths);

            protectedKey =
                await File.ReadAllBytesAsync(keyFilePath, TestContext.Current.CancellationToken);

            protectedKey[^1] ^= 0xFF;

            await File.WriteAllBytesAsync(keyFilePath, protectedKey, TestContext.Current.CancellationToken);

            storedBytesBeforeRead =
                await File.ReadAllBytesAsync(keyFilePath, TestContext.Current.CancellationToken);

            await Assert.ThrowsAsync<CryptographicException>(
                () =>
                    service.GetOrCreateKeyAsync(TestContext.Current.CancellationToken));

            storedBytesAfterRead =
                await File.ReadAllBytesAsync(keyFilePath, TestContext.Current.CancellationToken);

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
            if (protectedKey is not null)
            {
                CryptographicOperations.ZeroMemory(
                    protectedKey);
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

            await service.GetOrCreateKeyAsync(TestContext.Current.CancellationToken);

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
                await databaseKeyService.GetOrCreateKeyAsync(TestContext.Current.CancellationToken);

            documentKey =
                await documentKeyService.GetOrCreateKeyAsync(TestContext.Current.CancellationToken);

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
                await service.GetOrCreateKeyAsync(TestContext.Current.CancellationToken);

            string keyFilePath =
                GetDatabaseKeyFilePath(dataPaths);

            persistedContents =
                await File.ReadAllBytesAsync(keyFilePath, TestContext.Current.CancellationToken);

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
    public async Task GetOrCreateKeyAsync_WhenCompetingInitializersReachPublicationTogether_UsesPersistedAuthoritativeKey()
    {
        // Arrange
        string rootDirectory = CreateTemporaryDirectory();

        const int participantCount = 2;

        var publicationBarrier =
            new KeyPublicationBarrier(participantCount);

        Task<EncryptionKeyMaterial>[] initializationTasks =
            Array.Empty<Task<EncryptionKeyMaterial>>();

        byte[]? persistedProtectedKey = null;
        byte[]? unprotectedPayload = null;
        EncryptionKeyMaterial? persistedMaterial = null;

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(rootDirectory);

            WindowsDatabaseEncryptionKeyService[] services =
                Enumerable
                    .Range(0, participantCount)
                    .Select(
                        _ =>
                            new WindowsDatabaseEncryptionKeyService(
                                dataPaths,
                                NullLogger<WindowsDatabaseEncryptionKeyService>.Instance,
                                publicationBarrier.WaitAtPublicationAsync))
                    .ToArray();

            initializationTasks =
                services
                    .Select(service => service.GetOrCreateKeyAsync())
                    .ToArray();

            // Wait until both candidates are ready to publish.
            await publicationBarrier.WaitForAllArrivalsAsync(
                TimeSpan.FromSeconds(15));

            Assert.Equal(
                participantCount,
                publicationBarrier.ArrivalCount);

            string keyFilePath =
                GetDatabaseKeyFilePath(dataPaths);

            Assert.False(File.Exists(keyFilePath));

            string[] candidateFiles =
                Directory.GetFiles(
                    dataPaths.SecurityDirectory,
                    "database.key.*.tmp");

            Assert.Equal(
                participantCount,
                candidateFiles.Length);

            // Act: release both candidates to compete for publication.
            publicationBarrier.Release();

            EncryptionKeyMaterial[] keyMaterials =
                await Task.WhenAll(initializationTasks);

            // Assert: both callers receive the persisted authoritative key.
            Assert.Equal(
                participantCount,
                keyMaterials.Length);

            EncryptionKeyMaterial expectedMaterial =
                keyMaterials[0];

            Assert.All(
                keyMaterials,
                keyMaterial =>
                {
                    Assert.True(
                        CryptographicOperations.FixedTimeEquals(
                            expectedMaterial.KeyBytes,
                            keyMaterial.KeyBytes),
                        "Concurrent initializers returned different key bytes.");

                    Assert.Equal(
                        expectedMaterial.KeyId,
                        keyMaterial.KeyId);

                    Assert.Equal(
                        expectedMaterial.KeyVersion,
                        keyMaterial.KeyVersion);
                });

            Assert.True(File.Exists(keyFilePath));

            string[] persistedFiles =
                Directory.GetFiles(dataPaths.SecurityDirectory);

            Assert.Single(persistedFiles);

            Assert.Equal(
                Path.GetFullPath(keyFilePath),
                Path.GetFullPath(persistedFiles[0]));

            persistedProtectedKey =
                await File.ReadAllBytesAsync(keyFilePath, TestContext.Current.CancellationToken);

            unprotectedPayload =
                ProtectedData.Unprotect(
                    persistedProtectedKey,
                    null,
                    DataProtectionScope.CurrentUser);

            persistedMaterial =
                ProtectedEncryptionKeyPayload.Decode(
                    unprotectedPayload,
                    persistedProtectedKey,
                    EncryptionKeyPurpose.Database);

            Assert.True(
                CryptographicOperations.FixedTimeEquals(
                    expectedMaterial.KeyBytes,
                    persistedMaterial.KeyBytes),
                "Persisted canonical key does not match the authoritative key bytes.");

            Assert.Equal(
                expectedMaterial.KeyId,
                persistedMaterial.KeyId);

            Assert.Equal(
                expectedMaterial.KeyVersion,
                persistedMaterial.KeyVersion);

            Assert.Equal(1, persistedMaterial.KeyVersion);

            Assert.Empty(
                Directory.GetFiles(
                    dataPaths.SecurityDirectory,
                    "database.key.*.tmp"));
        }
        finally
        {
            // Unblock candidates before observing tasks or cleaning up.
            publicationBarrier.Release();

            foreach (Task<EncryptionKeyMaterial> task in initializationTasks)
            {
                try
                {
                    EncryptionKeyMaterial keyMaterial = await task;

                    CryptographicOperations.ZeroMemory(
                        keyMaterial.KeyBytes);
                }
                catch
                {
                    // Preserve the original test failure.
                }
            }

            if (persistedMaterial is not null)
            {
                CryptographicOperations.ZeroMemory(
                    persistedMaterial.KeyBytes);
            }

            if (unprotectedPayload is not null)
            {
                CryptographicOperations.ZeroMemory(
                    unprotectedPayload);
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
                await File.ReadAllBytesAsync(keyFilePath, TestContext.Current.CancellationToken);

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
    public async Task GetOrCreateKeyAsync_WhenCleanupFailsAfterWriteFailure_PreservesPrimaryException()
    {
        // Arrange
        string rootDirectory = CreateTemporaryDirectory();

        try
        {
            var dataPaths = new DeskVaultDataPaths(rootDirectory);

            var operations = new FaultInjectingKeyCandidateFileOperations
            {
                FailAfterPartialWrite = true,
                FailDelete = true
            };

            var service = new WindowsDatabaseEncryptionKeyService(
                dataPaths,
                NullLogger<WindowsDatabaseEncryptionKeyService>.Instance,
                beforeKeyPublicationAsync: null,
                candidateFileOperations: operations);

            // Act
            IOException exception = await Assert.ThrowsAsync<IOException>(
                () => service.GetOrCreateKeyAsync(TestContext.Current.CancellationToken));

            // Assert
            Assert.Equal(
                "Injected partial candidate write failure.",
                exception.Message);

            Assert.NotNull(operations.LastCandidateFilePath);

            Assert.True(
                File.Exists(operations.LastCandidateFilePath));

            Assert.False(
                File.Exists(GetDatabaseKeyFilePath(dataPaths)));

            Assert.Equal(1, operations.DeleteCallCount);
            Assert.Equal(0, operations.PublishCallCount);
        }
        finally
        {
            DeleteTemporaryDirectory(rootDirectory);
        }
    }

    [Fact]
    public async Task GetOrCreateKeyAsync_WhenCleanupFailsAfterCancellation_PreservesCancellation()
    {
        // Arrange
        string rootDirectory = CreateTemporaryDirectory();

        using var cancellationTokenSource =
            new CancellationTokenSource();

        try
        {
            var dataPaths = new DeskVaultDataPaths(rootDirectory);

            var operations = new FaultInjectingKeyCandidateFileOperations
            {
                FailDelete = true
            };

            Func<CancellationToken, Task> cancelBeforePublication = _ =>
            {
                cancellationTokenSource.Cancel();

                return Task.FromCanceled(
                    cancellationTokenSource.Token);
            };

            var service = new WindowsDatabaseEncryptionKeyService(
                dataPaths,
                NullLogger<WindowsDatabaseEncryptionKeyService>.Instance,
                beforeKeyPublicationAsync: cancelBeforePublication,
                candidateFileOperations: operations);

            // Act
            OperationCanceledException exception =
                await Assert.ThrowsAnyAsync<OperationCanceledException>(
                    () => service.GetOrCreateKeyAsync(
                        cancellationTokenSource.Token));

            // Assert
            Assert.Equal(
                cancellationTokenSource.Token,
                exception.CancellationToken);

            Assert.NotNull(operations.LastCandidateFilePath);

            Assert.True(
                File.Exists(operations.LastCandidateFilePath));

            Assert.False(
                File.Exists(GetDatabaseKeyFilePath(dataPaths)));

            Assert.Equal(1, operations.DeleteCallCount);
            Assert.Equal(0, operations.PublishCallCount);
        }
        finally
        {
            DeleteTemporaryDirectory(rootDirectory);
        }
    }

    [Fact]
    public async Task GetOrCreateKeyAsync_WhenAbandonedCandidateExists_DoesNotPromoteOrDeleteIt()
    {
        // Arrange
        string rootDirectory = CreateTemporaryDirectory();

        EncryptionKeyMaterial? createdKeyMaterial = null;
        EncryptionKeyMaterial? loadedKeyMaterial = null;
        byte[]? persistedKeyFileContents = null;

        byte[] abandonedCandidateContents =
        {
        0x44, 0x56, 0x41, 0x42, 0x41, 0x4E, 0x44, 0x4F, 0x4E, 0x45
        };

        try
        {
            var dataPaths = new DeskVaultDataPaths(rootDirectory);

            Directory.CreateDirectory(
                dataPaths.SecurityDirectory);

            string abandonedCandidatePath = Path.Combine(
                dataPaths.SecurityDirectory,
                "database.key.0123456789abcdef0123456789abcdef.tmp");

            string canonicalKeyFilePath =
                GetDatabaseKeyFilePath(dataPaths);

            await File.WriteAllBytesAsync(abandonedCandidatePath, abandonedCandidateContents, TestContext.Current.CancellationToken);

            // Act
            createdKeyMaterial = await CreateService(dataPaths)
                .GetOrCreateKeyAsync(TestContext.Current.CancellationToken);

            // Assert: create a valid canonical key and preserve the abandoned file.
            Assert.True(File.Exists(canonicalKeyFilePath));
            Assert.True(File.Exists(abandonedCandidatePath));

            byte[] preservedCandidateContents =
                await File.ReadAllBytesAsync(abandonedCandidatePath, TestContext.Current.CancellationToken);

            Assert.True(
                abandonedCandidateContents.SequenceEqual(
                    preservedCandidateContents));

            persistedKeyFileContents =
                await File.ReadAllBytesAsync(canonicalKeyFilePath, TestContext.Current.CancellationToken);

            Assert.False(
                abandonedCandidateContents.SequenceEqual(
                    persistedKeyFileContents));

            Assert.Equal(
                2,
                Directory.GetFiles(dataPaths.SecurityDirectory).Length);

            // Verify that subsequent retrieval uses the canonical key.
            loadedKeyMaterial = await CreateService(dataPaths)
                .GetOrCreateKeyAsync(TestContext.Current.CancellationToken);

            Assert.True(
                createdKeyMaterial.KeyBytes.SequenceEqual(
                    loadedKeyMaterial.KeyBytes));
        }
        finally
        {
            if (createdKeyMaterial is not null)
            {
                CryptographicOperations.ZeroMemory(
                    createdKeyMaterial.KeyBytes);
            }

            if (loadedKeyMaterial is not null)
            {
                CryptographicOperations.ZeroMemory(
                    loadedKeyMaterial.KeyBytes);
            }

            if (persistedKeyFileContents is not null)
            {
                CryptographicOperations.ZeroMemory(
                    persistedKeyFileContents);
            }

            DeleteTemporaryDirectory(rootDirectory);
        }
    }

    [Fact]
    public async Task GetOrCreateKeyAsync_WhenCandidateCreationCollides_DoesNotDeletePreExistingFile()
    {
        // Arrange
        string rootDirectory = CreateTemporaryDirectory();

        byte[] preExistingContents = [0x10, 0x20, 0x30, 0x40];
        byte[]? actualContents = null;

        try
        {
            var dataPaths = new DeskVaultDataPaths(rootDirectory);

            Directory.CreateDirectory(dataPaths.SecurityDirectory);

            string candidatePath = Path.Combine(
                dataPaths.SecurityDirectory,
                "database.key.preexisting.tmp");

            await File.WriteAllBytesAsync(candidatePath, preExistingContents, TestContext.Current.CancellationToken);

            var operations = new FaultInjectingKeyCandidateFileOperations
            {
                CandidateFilePathOverride = candidatePath
            };

            var service = new WindowsDatabaseEncryptionKeyService(
                dataPaths,
                NullLogger<WindowsDatabaseEncryptionKeyService>.Instance,
                beforeKeyPublicationAsync: null,
                candidateFileOperations: operations);

            // Act
            await Assert.ThrowsAsync<IOException>(
                () => service.GetOrCreateKeyAsync(TestContext.Current.CancellationToken));

            actualContents = await File.ReadAllBytesAsync(candidatePath, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(preExistingContents, actualContents);
            Assert.False(File.Exists(GetDatabaseKeyFilePath(dataPaths)));
            Assert.Equal(0, operations.DeleteCallCount);
            Assert.Equal(0, operations.PublishCallCount);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(preExistingContents);

            if (actualContents is not null)
            {
                CryptographicOperations.ZeroMemory(actualContents);
            }

            DeleteTemporaryDirectory(rootDirectory);
        }
    }

    [Fact]
    public async Task GetOrCreateKeyAsync_WhenCandidateWriteFailsAfterPartialWrite_CleansOwnedCandidate()
    {
        // Arrange
        string rootDirectory = CreateTemporaryDirectory();

        try
        {
            var dataPaths = new DeskVaultDataPaths(rootDirectory);

            var operations = new FaultInjectingKeyCandidateFileOperations
            {
                FailAfterPartialWrite = true
            };

            var service = new WindowsDatabaseEncryptionKeyService(
                dataPaths,
                NullLogger<WindowsDatabaseEncryptionKeyService>.Instance,
                beforeKeyPublicationAsync: null,
                candidateFileOperations: operations);

            // Act
            await Assert.ThrowsAsync<IOException>(
                () => service.GetOrCreateKeyAsync(TestContext.Current.CancellationToken));

            // Assert
            Assert.NotNull(operations.LastCandidateFilePath);

            Assert.False(
                File.Exists(operations.LastCandidateFilePath));

            Assert.False(
                File.Exists(GetDatabaseKeyFilePath(dataPaths)));

            Assert.Equal(1, operations.DeleteCallCount);
            Assert.Equal(0, operations.PublishCallCount);

            Assert.Empty(
                Directory.GetFiles(dataPaths.SecurityDirectory));
        }
        finally
        {
            DeleteTemporaryDirectory(rootDirectory);
        }
    }

    [Fact]
    public async Task GetOrCreateKeyAsync_WhenDurableCandidateFlushFails_CleansOwnedCandidate()
    {
        // Arrange
        string rootDirectory = CreateTemporaryDirectory();

        try
        {
            var dataPaths = new DeskVaultDataPaths(rootDirectory);

            var operations = new FaultInjectingKeyCandidateFileOperations
            {
                FailDurableFlush = true
            };

            var service = new WindowsDatabaseEncryptionKeyService(
                dataPaths,
                NullLogger<WindowsDatabaseEncryptionKeyService>.Instance,
                beforeKeyPublicationAsync: null,
                candidateFileOperations: operations);

            // Act
            await Assert.ThrowsAsync<IOException>(
                () => service.GetOrCreateKeyAsync(TestContext.Current.CancellationToken));

            // Assert
            Assert.NotNull(operations.LastCandidateFilePath);

            Assert.False(
                File.Exists(operations.LastCandidateFilePath));

            Assert.False(
                File.Exists(GetDatabaseKeyFilePath(dataPaths)));

            Assert.Equal(1, operations.DeleteCallCount);
            Assert.Equal(0, operations.PublishCallCount);

            Assert.Empty(
                Directory.GetFiles(dataPaths.SecurityDirectory));
        }
        finally
        {
            DeleteTemporaryDirectory(rootDirectory);
        }
    }

    [Fact]
    public async Task GetOrCreateKeyAsync_WhenCancelledAfterCandidateCreation_CleansOwnedCandidate()
    {
        // Arrange
        string rootDirectory = CreateTemporaryDirectory();

        using var cancellationTokenSource =
            new CancellationTokenSource();

        try
        {
            var dataPaths = new DeskVaultDataPaths(rootDirectory);

            var operations = new FaultInjectingKeyCandidateFileOperations();

            Func<CancellationToken, Task> cancelBeforePublication = _ =>
            {
                cancellationTokenSource.Cancel();

                return Task.FromCanceled(
                    cancellationTokenSource.Token);
            };

            var service = new WindowsDatabaseEncryptionKeyService(
                dataPaths,
                NullLogger<WindowsDatabaseEncryptionKeyService>.Instance,
                beforeKeyPublicationAsync: cancelBeforePublication,
                candidateFileOperations: operations);

            // Act
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => service.GetOrCreateKeyAsync(
                    cancellationTokenSource.Token));

            // Assert
            Assert.NotNull(operations.LastCandidateFilePath);

            Assert.False(
                File.Exists(operations.LastCandidateFilePath));

            Assert.False(
                File.Exists(GetDatabaseKeyFilePath(dataPaths)));

            Assert.Equal(1, operations.DeleteCallCount);
            Assert.Equal(0, operations.PublishCallCount);

            Assert.Empty(
                Directory.GetFiles(dataPaths.SecurityDirectory));
        }
        finally
        {
            DeleteTemporaryDirectory(rootDirectory);
        }
    }

    [Fact]
    public async Task GetOrCreateKeyAsync_WhenLosingCandidateCleanupFails_ReturnsPersistedAuthoritativeKey()
    {
        // Arrange
        string rootDirectory = CreateTemporaryDirectory();

        const int participantCount = 2;

        var publicationBarrier =
            new KeyPublicationBarrier(participantCount);

        Task<EncryptionKeyMaterial>[]? initializationTasks = null;
        EncryptionKeyMaterial[]? keyMaterials = null;
        EncryptionKeyMaterial? persistedMaterial = null;

        byte[]? persistedProtectedKey = null;
        byte[]? unprotectedPayload = null;

        try
        {
            var dataPaths = new DeskVaultDataPaths(rootDirectory);

            var operations = Enumerable
                .Range(0, participantCount)
                .Select(
                    _ => new FaultInjectingKeyCandidateFileOperations
                    {
                        FailDelete = true
                    })
                .ToArray();

            WindowsDatabaseEncryptionKeyService[] services = Enumerable
                .Range(0, participantCount)
                .Select(
                    index => new WindowsDatabaseEncryptionKeyService(
                        dataPaths,
                        NullLogger<WindowsDatabaseEncryptionKeyService>.Instance,
                        publicationBarrier.WaitAtPublicationAsync,
                        operations[index]))
                .ToArray();

            initializationTasks = services
                .Select(service => service.GetOrCreateKeyAsync())
                .ToArray();

            // Wait until both candidates exist before releasing publication.
            await publicationBarrier.WaitForAllArrivalsAsync(
                TimeSpan.FromSeconds(15));

            Assert.Equal(
                participantCount,
                publicationBarrier.ArrivalCount);

            string keyFilePath = GetDatabaseKeyFilePath(dataPaths);

            Assert.False(File.Exists(keyFilePath));

            Assert.Equal(
                participantCount,
                Directory.GetFiles(
                    dataPaths.SecurityDirectory,
                    "database.key.*.tmp").Length);

            // Act
            publicationBarrier.Release();

            keyMaterials = await Task.WhenAll(initializationTasks);

            // Assert: both initializers return the authoritative key.
            Assert.Equal(participantCount, keyMaterials.Length);

            EncryptionKeyMaterial expectedMaterial = keyMaterials[0];

            Assert.All(
                keyMaterials,
                keyMaterial =>
                {
                    Assert.True(
                        CryptographicOperations.FixedTimeEquals(
                            expectedMaterial.KeyBytes,
                            keyMaterial.KeyBytes),
                        "Concurrent initializers returned different key bytes.");

                    Assert.Equal(expectedMaterial.KeyId, keyMaterial.KeyId);
                    Assert.Equal(expectedMaterial.KeyVersion, keyMaterial.KeyVersion);
                });

            Assert.True(File.Exists(keyFilePath));

            // The winning candidate was published. The losing candidate remains
            // because cleanup of that candidate was deliberately made to fail.
            Assert.Single(
                Directory.GetFiles(
                    dataPaths.SecurityDirectory,
                    "database.key.*.tmp"));

            Assert.Equal(
                1,
                operations.Sum(operation => operation.DeleteCallCount));

            persistedProtectedKey = await File.ReadAllBytesAsync(keyFilePath, TestContext.Current.CancellationToken);

            unprotectedPayload = ProtectedData.Unprotect(
                persistedProtectedKey,
                null,
                DataProtectionScope.CurrentUser);

            persistedMaterial = ProtectedEncryptionKeyPayload.Decode(
                unprotectedPayload,
                persistedProtectedKey,
                EncryptionKeyPurpose.Database);

            Assert.True(
                CryptographicOperations.FixedTimeEquals(
                    expectedMaterial.KeyBytes,
                    persistedMaterial.KeyBytes),
                "The canonical key differs from the authoritative returned key.");

            Assert.Equal(expectedMaterial.KeyId, persistedMaterial.KeyId);
            Assert.Equal(expectedMaterial.KeyVersion, persistedMaterial.KeyVersion);
        }
        finally
        {
            // Release any participant left waiting if an assertion fails.
            publicationBarrier.Release();

            if (keyMaterials is null &&
                initializationTasks is not null)
            {
                try
                {
                    keyMaterials = await Task.WhenAll(initializationTasks);
                }
                catch
                {
                    // Preserve the original test failure.
                }
            }

            if (keyMaterials is not null)
            {
                foreach (EncryptionKeyMaterial keyMaterial in keyMaterials)
                {
                    CryptographicOperations.ZeroMemory(keyMaterial.KeyBytes);
                }
            }
            else if (initializationTasks is not null)
            {
                foreach (Task<EncryptionKeyMaterial> task in initializationTasks)
                {
                    try
                    {
                        EncryptionKeyMaterial keyMaterial = await task;

                        CryptographicOperations.ZeroMemory(keyMaterial.KeyBytes);
                    }
                    catch
                    {
                        // Preserve the original test failure.
                    }
                }
            }

            if (persistedMaterial is not null)
            {
                CryptographicOperations.ZeroMemory(persistedMaterial.KeyBytes);
            }

            if (unprotectedPayload is not null)
            {
                CryptographicOperations.ZeroMemory(unprotectedPayload);
            }

            if (persistedProtectedKey is not null)
            {
                CryptographicOperations.ZeroMemory(persistedProtectedKey);
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
                        service.GetOrCreateKeyAsync(TestContext.Current.CancellationToken));

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

            await File.WriteAllBytesAsync(keyFilePath, protectedLegacyArtifact, TestContext.Current.CancellationToken);

            var service =
                CreateService(dataPaths);

            // Act
            retrievedKey =
                await service.GetKeyAsync(TestContext.Current.CancellationToken);

            persistedArtifactAfterRead =
                await File.ReadAllBytesAsync(keyFilePath, TestContext.Current.CancellationToken);

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
