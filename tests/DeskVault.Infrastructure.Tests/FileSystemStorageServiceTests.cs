using DeskVault.Application.Documents.Commands.ImportDocument;
using DeskVault.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Cryptography;

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
    public async Task StoreAsync_WhenExpectedHashMatchesStoredContent_CompletesSuccessfully()
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
                "DeskVault hash verification test."u8.ToArray();

            await File.WriteAllBytesAsync(
                sourceFilePath,
                content);

            string expectedHash =
                Convert.ToHexString(
                    SHA256.HashData(content))
                .ToLowerInvariant();

            Guid documentId =
                Guid.NewGuid();

            string storedFilePath =
                await storageService.StoreAsync(
                    sourceFilePath,
                    documentId,
                    expectedSha256Hash: expectedHash);

            Assert.True(
                File.Exists(
                    storedFilePath));

            Assert.Equal(
                Path.Combine(
                    dataPaths.DocumentsDirectory,
                    $"{documentId}.dvault"),
                storedFilePath);
        }
        finally
        {
            DeleteTemporaryDirectory(
                rootDirectory);
        }
    }

    [Fact]
    public async Task StoreAsync_WhenExpectedHashDoesNotMatchStoredContent_ThrowsAndDeletesArtifact()
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
                "DeskVault hash mismatch test."u8.ToArray();

            await File.WriteAllBytesAsync(
                sourceFilePath,
                content);

            string expectedHash =
                Convert.ToHexString(
                    SHA256.HashData(
                        "Different content."u8.ToArray()))
                .ToLowerInvariant();

            Guid documentId =
                Guid.NewGuid();

            string expectedStoredFilePath =
                Path.Combine(
                    dataPaths.DocumentsDirectory,
                    $"{documentId}.dvault");

            await Assert.ThrowsAsync<DocumentImportContentMismatchException>(
                () =>
                    storageService.StoreAsync(
                        sourceFilePath,
                        documentId,
                        expectedSha256Hash: expectedHash));

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
    public async Task DeleteAsync_ExistingArtifact_DeletesArtifactOwnedByDocument()
    {
        // Arrange
        string rootDirectory =
            CreateTemporaryDirectory();

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(
                    rootDirectory);

            var pathResolver =
                new DocumentArtifactPathResolver(
                    dataPaths);

            FileSystemStorageService storageService =
                CreateStorageService(
                    dataPaths);

            Guid documentId =
                Guid.NewGuid();

            string storedFilePath =
                pathResolver.GetPath(
                    documentId);

            Directory.CreateDirectory(
                dataPaths.DocumentsDirectory);

            await File.WriteAllTextAsync(
                storedFilePath,
                "encrypted-content");

            // Act
            await storageService.DeleteAsync(
                documentId);

            // Assert
            Assert.False(
                File.Exists(storedFilePath));
        }
        finally
        {
            DeleteTemporaryDirectory(
                rootDirectory);
        }
    }

    [Fact]
    public async Task DeleteAsync_EmptyDocumentId_ThrowsArgumentException()
    {
        // Arrange
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

            // Act
            ArgumentException exception =
                await Assert.ThrowsAsync<ArgumentException>(
                    () =>
                        storageService.DeleteAsync(
                            Guid.Empty));

            // Assert
            Assert.Equal(
                "documentId",
                exception.ParamName);
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
        // Arrange
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

            // Act & Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () =>
                    storageService.DeleteAsync(
                        Guid.NewGuid(),
                        cancellationTokenSource.Token));
        }
        finally
        {
            DeleteTemporaryDirectory(
                rootDirectory);
        }
    }

    [Fact]
    public async Task DeleteAsync_MissingArtifact_DoesNotThrow()
    {
        // Arrange
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

            Guid documentId =
                Guid.NewGuid();

            // Act
            await storageService.DeleteAsync(
                documentId);

            // Assert
            string storedFilePath =
                new DocumentArtifactPathResolver(
                    dataPaths)
                .GetPath(documentId);

            Assert.False(
                File.Exists(storedFilePath));
        }
        finally
        {
            DeleteTemporaryDirectory(
                rootDirectory);
        }
    }

    [Fact]
    public async Task DeleteAsync_DeletesOnlyArtifactOwnedByRequestedDocument()
    {
        // Arrange
        string rootDirectory =
            CreateTemporaryDirectory();

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(
                    rootDirectory);

            var pathResolver =
                new DocumentArtifactPathResolver(
                    dataPaths);

            FileSystemStorageService storageService =
                CreateStorageService(
                    dataPaths);

            Guid firstDocumentId =
                Guid.NewGuid();

            Guid secondDocumentId =
                Guid.NewGuid();

            string firstArtifactPath =
                pathResolver.GetPath(
                    firstDocumentId);

            string secondArtifactPath =
                pathResolver.GetPath(
                    secondDocumentId);

            Directory.CreateDirectory(
                dataPaths.DocumentsDirectory);

            await File.WriteAllTextAsync(
                firstArtifactPath,
                "first-artifact");

            await File.WriteAllTextAsync(
                secondArtifactPath,
                "second-artifact");

            // Act
            await storageService.DeleteAsync(
                firstDocumentId);

            // Assert
            Assert.False(
                File.Exists(firstArtifactPath));

            Assert.True(
                File.Exists(secondArtifactPath));

            Assert.Equal(
                "second-artifact",
                await File.ReadAllTextAsync(secondArtifactPath));
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
            new DocumentArtifactPathResolver(dataPaths),
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
