using System.Security.Cryptography;
using DeskVault.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeskVault.Infrastructure.Tests;

public sealed class FileSystemStorageServiceTests
{
    [Fact]
    public async Task StoreAsync_CreatesEncryptedFileAndReturnsPath()
    {
        string rootDirectory =
            CreateTemporaryDirectory();

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(
                    rootDirectory);

            byte[] key =
                RandomNumberGenerator.GetBytes(32);

            var keyService =
                new TestEncryptionKeyService(key);

            var encryptionService =
                new DocumentEncryptionService(
                    keyService,
                    NullLogger<DocumentEncryptionService>.Instance);

            var storageService =
                new FileSystemStorageService(
                    encryptionService,
                    dataPaths,
                    NullLogger<FileSystemStorageService>.Instance);

            string sourceFilePath =
                Path.Combine(
                    rootDirectory,
                    "source.txt");

            byte[] content =
                "DeskVault integration test content."u8.ToArray();

            await File.WriteAllBytesAsync(
                sourceFilePath,
                content);

            Guid documentId =
                Guid.NewGuid();

            string storedFilePath =
                await storageService.StoreAsync(
                    sourceFilePath,
                    documentId);

            Assert.Equal(
                Path.Combine(
                    dataPaths.DocumentsDirectory,
                    $"{documentId}.dvault"),
                storedFilePath);

            Assert.True(
                File.Exists(storedFilePath));

            Assert.NotEqual(
                content,
                await File.ReadAllBytesAsync(
                    storedFilePath));
        }
        finally
        {
            DeleteTemporaryDirectory(
                rootDirectory);
        }
    }

    [Fact]
    public async Task StoreAsync_StoredFileCanBeDecryptedBackToOriginalContent()
    {
        string rootDirectory =
            CreateTemporaryDirectory();

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(
                    rootDirectory);

            byte[] key =
                RandomNumberGenerator.GetBytes(32);

            var keyService =
                new TestEncryptionKeyService(key);

            var encryptionService =
                new DocumentEncryptionService(
                    keyService,
                    NullLogger<DocumentEncryptionService>.Instance);

            var storageService =
                new FileSystemStorageService(
                    encryptionService,
                    dataPaths,
                    NullLogger<FileSystemStorageService>.Instance);

            string sourceFilePath =
                Path.Combine(
                    rootDirectory,
                    "source.txt");

            byte[] originalContent =
                "DeskVault encryption round-trip test."u8.ToArray();

            await File.WriteAllBytesAsync(
                sourceFilePath,
                originalContent);

            string storedFilePath =
                await storageService.StoreAsync(
                    sourceFilePath,
                    Guid.NewGuid());

            await using var encryptedSource =
                new FileStream(
                    storedFilePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read);

            using var decryptedDestination =
                new MemoryStream();

            await encryptionService.DecryptAsync(
                encryptedSource,
                decryptedDestination);

            byte[] decryptedContent =
                decryptedDestination.ToArray();

            Assert.Equal(
                originalContent,
                decryptedContent);
        }
        finally
        {
            DeleteTemporaryDirectory(
                rootDirectory);
        }
    }

    [Fact]
    public async Task StoreAsync_CreatesDocumentsDirectory()
    {
        string rootDirectory =
            CreateTemporaryDirectory();

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(
                    rootDirectory);

            var keyService =
                new TestEncryptionKeyService(
                    RandomNumberGenerator.GetBytes(32));

            var encryptionService =
                new DocumentEncryptionService(
                    keyService,
                    NullLogger<DocumentEncryptionService>.Instance);

            var storageService =
                new FileSystemStorageService(
                    encryptionService,
                    dataPaths,
                    NullLogger<FileSystemStorageService>.Instance);

            string sourceFilePath =
                Path.Combine(
                    rootDirectory,
                    "source.txt");

            await File.WriteAllTextAsync(
                sourceFilePath,
                "DeskVault");

            Assert.False(
                Directory.Exists(
                    dataPaths.DocumentsDirectory));

            await storageService.StoreAsync(
                sourceFilePath,
                Guid.NewGuid());

            Assert.True(
                Directory.Exists(
                    dataPaths.DocumentsDirectory));
        }
        finally
        {
            DeleteTemporaryDirectory(
                rootDirectory);
        }
    }

    [Fact]
    public async Task StoreAsync_MissingSourceFile_ThrowsFileNotFoundException()
    {
        string rootDirectory =
            CreateTemporaryDirectory();

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(
                    rootDirectory);

            var keyService =
                new TestEncryptionKeyService(
                    RandomNumberGenerator.GetBytes(32));

            var encryptionService =
                new DocumentEncryptionService(
                    keyService,
                    NullLogger<DocumentEncryptionService>.Instance);

            var storageService =
                new FileSystemStorageService(
                    encryptionService,
                    dataPaths,
                    NullLogger<FileSystemStorageService>.Instance);

            string missingSource =
                Path.Combine(
                    rootDirectory,
                    "missing.txt");

            await Assert.ThrowsAsync<FileNotFoundException>(
                () =>
                    storageService.StoreAsync(
                        missingSource,
                        Guid.NewGuid()));
        }
        finally
        {
            DeleteTemporaryDirectory(
                rootDirectory);
        }
    }

    [Fact]
    public async Task DeleteAsync_ExistingFile_DeletesFile()
    {
        string rootDirectory =
            CreateTemporaryDirectory();

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(
                    rootDirectory);

            string filePath =
                Path.Combine(
                    dataPaths.RootDirectory,
                    "document.dvault");

            Directory.CreateDirectory(
                dataPaths.RootDirectory);

            await File.WriteAllTextAsync(
                filePath,
                "encrypted-content");

            var keyService =
                new TestEncryptionKeyService(
                    RandomNumberGenerator.GetBytes(32));

            var encryptionService =
                new DocumentEncryptionService(
                    keyService,
                    NullLogger<DocumentEncryptionService>.Instance);

            var storageService =
                new FileSystemStorageService(
                    encryptionService,
                    dataPaths,
                    NullLogger<FileSystemStorageService>.Instance);

            await storageService.DeleteAsync(
                filePath);

            Assert.False(
                File.Exists(filePath));
        }
        finally
        {
            DeleteTemporaryDirectory(
                rootDirectory);
        }
    }

    [Fact]
    public async Task DeleteAsync_EmptyPath_ThrowsArgumentException()
    {
        string rootDirectory =
            CreateTemporaryDirectory();

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(
                    rootDirectory);

            var keyService =
                new TestEncryptionKeyService(
                    RandomNumberGenerator.GetBytes(32));

            var encryptionService =
                new DocumentEncryptionService(
                    keyService,
                    NullLogger<DocumentEncryptionService>.Instance);

            var storageService =
                new FileSystemStorageService(
                    encryptionService,
                    dataPaths,
                    NullLogger<FileSystemStorageService>.Instance);

            await Assert.ThrowsAsync<ArgumentException>(
                () =>
                    storageService.DeleteAsync(
                        string.Empty));
        }
        finally
        {
            DeleteTemporaryDirectory(
                rootDirectory);
        }
    }

    [Fact]
    public async Task DeleteAsync_Cancelled_ThrowsOperationCanceledException()
    {
        string rootDirectory =
            CreateTemporaryDirectory();

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(
                    rootDirectory);

            var keyService =
                new TestEncryptionKeyService(
                    RandomNumberGenerator.GetBytes(32));

            var encryptionService =
                new DocumentEncryptionService(
                    keyService,
                    NullLogger<DocumentEncryptionService>.Instance);

            var storageService =
                new FileSystemStorageService(
                    encryptionService,
                    dataPaths,
                    NullLogger<FileSystemStorageService>.Instance);

            using var cancellationTokenSource =
                new CancellationTokenSource();

            cancellationTokenSource.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(
                () =>
                    storageService.DeleteAsync(
                        Path.Combine(
                            rootDirectory,
                            "document.dvault"),
                        cancellationTokenSource.Token));
        }
        finally
        {
            DeleteTemporaryDirectory(
                rootDirectory);
        }
    }

    [Fact]
    public async Task DeleteAsync_MissingFile_DoesNotThrow()
    {
        string rootDirectory =
            CreateTemporaryDirectory();

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(
                    rootDirectory);

            var keyService =
                new TestEncryptionKeyService(
                    RandomNumberGenerator.GetBytes(32));

            var encryptionService =
                new DocumentEncryptionService(
                    keyService,
                    NullLogger<DocumentEncryptionService>.Instance);

            var storageService =
                new FileSystemStorageService(
                    encryptionService,
                    dataPaths,
                    NullLogger<FileSystemStorageService>.Instance);

            string missingFilePath =
                Path.Combine(
                    rootDirectory,
                    "missing.dvault");

            await storageService.DeleteAsync(
                missingFilePath);

            Assert.False(
                File.Exists(
                    missingFilePath));
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

    private sealed class TestEncryptionKeyService :
        IEncryptionKeyService
    {
        private readonly byte[] _key;

        public TestEncryptionKeyService(
            byte[] key)
        {
            _key = key;
        }

        public Task<byte[]> GetOrCreateKeyAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                _key.ToArray());
        }
    }
}
