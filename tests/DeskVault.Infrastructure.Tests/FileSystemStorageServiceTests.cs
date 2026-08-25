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

            FileSystemStorageService storageService =
                CreateStorageService(
                    dataPaths);

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

            DocumentEncryptionService encryptionService =
                CreateEncryptionService();

            FileSystemStorageService storageService =
                CreateStorageService(
                    dataPaths,
                    encryptionService);

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

            FileSystemStorageService storageService =
                CreateStorageService(
                    dataPaths);

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

            FileSystemStorageService storageService =
                CreateStorageService(
                    dataPaths);

            string missingSource =
                Path.Combine(
                    rootDirectory,
                    "missing.txt");

            Guid documentId =
                Guid.NewGuid();

            string expectedStoredFilePath =
                Path.Combine(
                    dataPaths.DocumentsDirectory,
                    $"{documentId}.dvault");

            await Assert.ThrowsAsync<FileNotFoundException>(
                () =>
                    storageService.StoreAsync(
                        missingSource,
                        documentId));

            Assert.False(
                File.Exists(
                    expectedStoredFilePath));
        }
        finally
        {
            DeleteTemporaryDirectory(
                rootDirectory);
        }
    }

    [Fact]
    public async Task StoreAsync_Cancelled_ThrowsOperationCanceledException()
    {
        string rootDirectory =
            CreateTemporaryDirectory();

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(
                    rootDirectory);

            FileSystemStorageService storageService =
                CreateStorageService(
                    dataPaths);

            string sourceFilePath =
                Path.Combine(
                    rootDirectory,
                    "source.txt");

            await File.WriteAllTextAsync(
                sourceFilePath,
                "DeskVault cancellation test.");

            Guid documentId =
                Guid.NewGuid();

            string expectedStoredFilePath =
                Path.Combine(
                    dataPaths.DocumentsDirectory,
                    $"{documentId}.dvault");

            using var cancellationTokenSource =
                new CancellationTokenSource();

            cancellationTokenSource.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () =>
                    storageService.StoreAsync(
                        sourceFilePath,
                        documentId,
                        cancellationTokenSource.Token));

            Assert.False(
                File.Exists(
                    expectedStoredFilePath));
        }
        finally
        {
            DeleteTemporaryDirectory(
                rootDirectory);
        }
    }

    [Fact]
    public async Task StoreAsync_WhenDestinationAlreadyExists_ThrowsIOExceptionAndPreservesExistingFile()
    {
        string rootDirectory =
            CreateTemporaryDirectory();

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(
                    rootDirectory);

            FileSystemStorageService storageService =
                CreateStorageService(
                    dataPaths);

            string sourceFilePath =
                Path.Combine(
                    rootDirectory,
                    "source.txt");

            await File.WriteAllTextAsync(
                sourceFilePath,
                "DeskVault source content.");

            Guid documentId =
                Guid.NewGuid();

            Directory.CreateDirectory(
                dataPaths.DocumentsDirectory);

            string storedFilePath =
                Path.Combine(
                    dataPaths.DocumentsDirectory,
                    $"{documentId}.dvault");

            byte[] existingContent =
                "Existing encrypted file."u8.ToArray();

            await File.WriteAllBytesAsync(
                storedFilePath,
                existingContent);

            await Assert.ThrowsAnyAsync<IOException>(
                () =>
                    storageService.StoreAsync(
                        sourceFilePath,
                        documentId));

            Assert.Equal(
                existingContent,
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

            FileSystemStorageService storageService =
                CreateStorageService(
                    dataPaths);

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

            FileSystemStorageService storageService =
                CreateStorageService(
                    dataPaths);

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

            FileSystemStorageService storageService =
                CreateStorageService(
                    dataPaths);

            using var cancellationTokenSource =
                new CancellationTokenSource();

            cancellationTokenSource.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
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

            FileSystemStorageService storageService =
                CreateStorageService(
                    dataPaths);

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

    private static FileSystemStorageService CreateStorageService(
        DeskVaultDataPaths dataPaths,
        DocumentEncryptionService? encryptionService = null)
    {
        encryptionService ??=
            CreateEncryptionService();

        return new FileSystemStorageService(
            encryptionService,
            dataPaths,
            NullLogger<FileSystemStorageService>.Instance);
    }

    private static DocumentEncryptionService CreateEncryptionService()
    {
        byte[] key =
            RandomNumberGenerator.GetBytes(32);

        return new DocumentEncryptionService(
            new TestEncryptionKeyService(key),
            NullLogger<DocumentEncryptionService>.Instance);
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

    private sealed class TestEncryptionKeyService
        : IEncryptionKeyService
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
