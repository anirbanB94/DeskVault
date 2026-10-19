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
    public async Task GetOrCreateKeyAsync_WhenCompetingInitializersReachPublicationTogether_UsesPersistedAuthoritativeKey()
    {
        // Arrange
        string rootDirectory =
            CreateTemporaryDirectory();

        KeyPublicationBarrier? publicationBarrier = null;
        Task<EncryptionKeyMaterial>[]? initializationTasks = null;
        EncryptionKeyMaterial[]? keyMaterials = null;
        EncryptionKeyMaterial? persistedMaterial = null;
        byte[]? persistedProtectedKey = null;
        byte[]? unprotectedPayload = null;

        try
        {
            // Set up two independent initializers and a shared publication barrier.

            var dataPaths =
                new DeskVaultDataPaths(
                    rootDirectory);

            const int participantCount = 2;

            publicationBarrier =
                new KeyPublicationBarrier(
                    participantCount);

            WindowsEncryptionKeyService[] services =
                Enumerable
                    .Range(0, participantCount)
                    .Select(
                        _ =>
                            new WindowsEncryptionKeyService(
                                dataPaths,
                                NullLogger<WindowsEncryptionKeyService>.Instance,
                                publicationBarrier.WaitAtPublicationAsync))
                    .ToArray();

            initializationTasks =
                services
                    .Select(
                        service =>
                            service.GetOrCreateKeyAsync())
                    .ToArray();

            // Hold both candidates until each initializer reaches publication.
            await publicationBarrier.WaitForAllArrivalsAsync(
                TimeSpan.FromSeconds(15));

            Assert.Equal(
                participantCount,
                publicationBarrier.ArrivalCount);

            string keyFilePath =
                GetKeyFilePath(dataPaths);

            Assert.False(
                File.Exists(keyFilePath));

            // Verify both candidates exist before the canonical key is published.
            string[] candidateFiles =
                Directory.GetFiles(
                    dataPaths.SecurityDirectory,
                    "master.key.*.tmp");

            Assert.Equal(
                participantCount,
                candidateFiles.Length);

            // Act
            publicationBarrier.Release();

            keyMaterials =
                await Task.WhenAll(
                    initializationTasks);

            // Assert: each initializer returns the same key material.
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

            persistedProtectedKey =
                await File.ReadAllBytesAsync(
                    keyFilePath);

            unprotectedPayload =
                ProtectedData.Unprotect(
                    persistedProtectedKey,
                    null,
                    DataProtectionScope.CurrentUser);

            persistedMaterial =
                ProtectedEncryptionKeyPayload.Decode(
                    unprotectedPayload,
                    persistedProtectedKey,
                    EncryptionKeyPurpose.Document);

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

            Assert.Equal(
                1,
                persistedMaterial.KeyVersion);

            Assert.Empty(
                Directory.GetFiles(
                    dataPaths.SecurityDirectory,
                    "master.key.*.tmp"));
        }
        finally
        {
            // Always unblock any initializer left waiting if an assertion
            // or the arrival timeout fails.
            publicationBarrier?.Release();

            // Observe pending tasks before deleting their temporary directory.
            if (keyMaterials is null &&
                initializationTasks is not null)
            {
                try
                {
                    keyMaterials =
                        await Task.WhenAll(
                            initializationTasks);
                }
                catch
                {
                    // Preserve the original test failure.
                }
            }

            // Clear returned key material, including partial task success.
            if (keyMaterials is not null)
            {
                foreach (EncryptionKeyMaterial keyMaterial in keyMaterials)
                {
                    CryptographicOperations.ZeroMemory(
                        keyMaterial.KeyBytes);
                }
            }
            else if (initializationTasks is not null)
            {
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

            var service = new WindowsEncryptionKeyService(
                dataPaths,
                NullLogger<WindowsEncryptionKeyService>.Instance,
                beforeKeyPublicationAsync: null,
                candidateFileOperations: operations);

            // Act
            IOException exception = await Assert.ThrowsAsync<IOException>(
                () => service.GetOrCreateKeyAsync());

            // Assert
            Assert.Equal(
                "Injected partial candidate write failure.",
                exception.Message);

            Assert.NotNull(operations.LastCandidateFilePath);

            Assert.True(
                File.Exists(operations.LastCandidateFilePath));

            Assert.False(
                File.Exists(GetKeyFilePath(dataPaths)));

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

            var service = new WindowsEncryptionKeyService(
                dataPaths,
                NullLogger<WindowsEncryptionKeyService>.Instance,
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
                File.Exists(GetKeyFilePath(dataPaths)));

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
                "master.key.0123456789abcdef0123456789abcdef.tmp");

            string canonicalKeyFilePath = Path.Combine(
                dataPaths.SecurityDirectory,
                "master.key");

            await File.WriteAllBytesAsync(
                abandonedCandidatePath,
                abandonedCandidateContents);

            // Act
            createdKeyMaterial = await CreateService(dataPaths)
                .GetOrCreateKeyAsync();

            // Assert: create a valid canonical key and preserve the abandoned file.
            Assert.True(File.Exists(canonicalKeyFilePath));
            Assert.True(File.Exists(abandonedCandidatePath));

            byte[] preservedCandidateContents =
                await File.ReadAllBytesAsync(abandonedCandidatePath);

            Assert.True(
                abandonedCandidateContents.SequenceEqual(
                    preservedCandidateContents));

            persistedKeyFileContents =
                await File.ReadAllBytesAsync(canonicalKeyFilePath);

            Assert.False(
                abandonedCandidateContents.SequenceEqual(
                    persistedKeyFileContents));

            Assert.Equal(
                2,
                Directory.GetFiles(dataPaths.SecurityDirectory).Length);

            // Verify that subsequent retrieval uses the canonical key.
            loadedKeyMaterial = await CreateService(dataPaths)
                .GetOrCreateKeyAsync();

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
                "master.key.preexisting.tmp");

            await File.WriteAllBytesAsync(
                candidatePath,
                preExistingContents);

            var operations = new FaultInjectingKeyCandidateFileOperations
            {
                CandidateFilePathOverride = candidatePath
            };

            var service = new WindowsEncryptionKeyService(
                dataPaths,
                NullLogger<WindowsEncryptionKeyService>.Instance,
                beforeKeyPublicationAsync: null,
                candidateFileOperations: operations);

            // Act
            await Assert.ThrowsAsync<IOException>(
                () => service.GetOrCreateKeyAsync());

            actualContents = await File.ReadAllBytesAsync(candidatePath);

            // Assert
            Assert.Equal(preExistingContents, actualContents);
            Assert.False(File.Exists(GetKeyFilePath(dataPaths)));
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

            var service = new WindowsEncryptionKeyService(
                dataPaths,
                NullLogger<WindowsEncryptionKeyService>.Instance,
                beforeKeyPublicationAsync: null,
                candidateFileOperations: operations);

            // Act
            await Assert.ThrowsAsync<IOException>(
                () => service.GetOrCreateKeyAsync());

            // Assert
            Assert.NotNull(operations.LastCandidateFilePath);

            Assert.False(
                File.Exists(operations.LastCandidateFilePath));

            Assert.False(
                File.Exists(GetKeyFilePath(dataPaths)));

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

            var service = new WindowsEncryptionKeyService(
                dataPaths,
                NullLogger<WindowsEncryptionKeyService>.Instance,
                beforeKeyPublicationAsync: null,
                candidateFileOperations: operations);

            // Act
            await Assert.ThrowsAsync<IOException>(
                () => service.GetOrCreateKeyAsync());

            // Assert
            Assert.NotNull(operations.LastCandidateFilePath);

            Assert.False(
                File.Exists(operations.LastCandidateFilePath));

            Assert.False(
                File.Exists(GetKeyFilePath(dataPaths)));

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

            var service = new WindowsEncryptionKeyService(
                dataPaths,
                NullLogger<WindowsEncryptionKeyService>.Instance,
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
                File.Exists(GetKeyFilePath(dataPaths)));

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

            WindowsEncryptionKeyService[] services = Enumerable
                .Range(0, participantCount)
                .Select(
                    index => new WindowsEncryptionKeyService(
                        dataPaths,
                        NullLogger<WindowsEncryptionKeyService>.Instance,
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

            string keyFilePath = GetKeyFilePath(dataPaths);

            Assert.False(File.Exists(keyFilePath));

            Assert.Equal(
                participantCount,
                Directory.GetFiles(
                    dataPaths.SecurityDirectory,
                    "master.key.*.tmp").Length);

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

            // The winning candidate was moved to the canonical path.
            // The losing candidate remains because its cleanup failed.
            Assert.Single(
                Directory.GetFiles(
                    dataPaths.SecurityDirectory,
                    "master.key.*.tmp"));

            Assert.Equal(
                1,
                operations.Sum(operation => operation.DeleteCallCount));

            persistedProtectedKey = await File.ReadAllBytesAsync(keyFilePath);

            unprotectedPayload = ProtectedData.Unprotect(
                persistedProtectedKey,
                null,
                DataProtectionScope.CurrentUser);

            persistedMaterial = ProtectedEncryptionKeyPayload.Decode(
                unprotectedPayload,
                persistedProtectedKey,
                EncryptionKeyPurpose.Document);

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
