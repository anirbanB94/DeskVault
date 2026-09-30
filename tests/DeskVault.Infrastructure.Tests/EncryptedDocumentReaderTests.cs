using DeskVault.Application.Documents.Processing;
using DeskVault.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Cryptography;
using System.Text;

namespace DeskVault.Infrastructure.Tests;

public sealed class EncryptedDocumentReaderTests
{
    [Fact]
    public async Task OpenReadAsync_WhenStoredFileIsValid_ReturnsDecryptedContent()
    {
        // Arrange
        using var context =
            CreateTestContext();

        byte[] originalContent =
            Encoding.UTF8.GetBytes(
                "DeskVault encrypted document reader test.");

        Guid documentId =
            Guid.NewGuid();

        string artifactPath =
            context.PathResolver.GetPath(
                documentId);

        await CreateEncryptedFileAsync(
            artifactPath,
            originalContent,
            context.EncryptionService);

        // Act
        await using Stream result =
            await context.Reader.OpenReadAsync(
                documentId);

        // Assert
        Assert.Equal(
            0,
            result.Position);

        using var memory =
            new MemoryStream();

        await result.CopyToAsync(
            memory);

        Assert.Equal(
            originalContent,
            memory.ToArray());
    }

    [Fact]
    public async Task OpenReadAsync_WhenPlaintextExceedsMaximum_ThrowsResourceLimitExceededException()
    {
        // Arrange
        using var context =
            CreateTestContext();

        byte[] originalContent =
            Encoding.UTF8.GetBytes(
                "DeskVault encrypted document reader resource limit test.");

        Guid documentId =
            Guid.NewGuid();

        string artifactPath =
            context.PathResolver.GetPath(
                documentId);

        await CreateEncryptedFileAsync(
            artifactPath,
            originalContent,
            context.EncryptionService);

        long maximumPlaintextBytes =
            originalContent.Length - 1;

        // Act
        ResourceLimitExceededException exception =
            await Assert.ThrowsAsync<ResourceLimitExceededException>(
                () =>
                    context.Reader.OpenReadAsync(
                        documentId,
                        maximumPlaintextBytes:
                            maximumPlaintextBytes));

        // Assert
        Assert.Equal(
            maximumPlaintextBytes,
            exception.LimitBytes);

        Assert.Equal(
            originalContent.Length,
            exception.AttemptedBytes);
    }

    [Fact]
    public async Task OpenReadAsync_WhenStoredFileIsTampered_ThrowsAuthenticationTagMismatchException()
    {
        // Arrange
        using var context =
            CreateTestContext();

        byte[] originalContent =
            Encoding.UTF8.GetBytes(
                "DeskVault tampered document test.");

        Guid documentId =
            Guid.NewGuid();

        string artifactPath =
            context.PathResolver.GetPath(
                documentId);

        await CreateEncryptedFileAsync(
            artifactPath,
            originalContent,
            context.EncryptionService);

        byte[] encryptedContent =
            await File.ReadAllBytesAsync(
                artifactPath);

        Assert.True(
            encryptedContent.Length > 24);

        encryptedContent[^1] ^= 0xFF;

        await File.WriteAllBytesAsync(
            artifactPath,
            encryptedContent);

        // Act & Assert
        await Assert.ThrowsAsync<AuthenticationTagMismatchException>(
            () =>
                context.Reader.OpenReadAsync(
                    documentId));
    }

    [Fact]
    public async Task OpenReadAsync_WhenStoredArtifactDoesNotExist_ThrowsFileNotFoundException()
    {
        // Arrange
        using var context =
            CreateTestContext();

        Guid documentId =
            Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(
            () =>
                context.Reader.OpenReadAsync(
                    documentId));
    }

    [Fact]
    public async Task OpenReadAsync_WhenCancellationIsRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        using var context =
            CreateTestContext();

        byte[] originalContent =
            Encoding.UTF8.GetBytes(
                "DeskVault cancellation test.");

        Guid documentId =
            Guid.NewGuid();

        string artifactPath =
            context.PathResolver.GetPath(
                documentId);

        await CreateEncryptedFileAsync(
            artifactPath,
            originalContent,
            context.EncryptionService);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                context.Reader.OpenReadAsync(
                    documentId,
                    cancellationTokenSource.Token));
    }

    [Fact]
    public async Task OpenReadAsync_UsesCanonicalArtifactForDocumentIdentity()
    {
        // Arrange
        using var context =
            CreateTestContext();

        Guid documentId =
            Guid.NewGuid();

        Guid unrelatedDocumentId =
            Guid.NewGuid();

        byte[] canonicalContent =
            Encoding.UTF8.GetBytes(
                "canonical-document-content");

        byte[] unrelatedContent =
            Encoding.UTF8.GetBytes(
                "unrelated-document-content");

        string canonicalArtifactPath =
            context.PathResolver.GetPath(
                documentId);

        string unrelatedArtifactPath =
            context.PathResolver.GetPath(
                unrelatedDocumentId);

        await CreateEncryptedFileAsync(
            canonicalArtifactPath,
            canonicalContent,
            context.EncryptionService);

        await CreateEncryptedFileAsync(
            unrelatedArtifactPath,
            unrelatedContent,
            context.EncryptionService);

        // Act
        await using Stream result =
            await context.Reader.OpenReadAsync(
                documentId);

        using var memory =
            new MemoryStream();

        await result.CopyToAsync(
            memory);

        // Assert
        Assert.Equal(
            canonicalContent,
            memory.ToArray());

        Assert.NotEqual(
            unrelatedContent,
            memory.ToArray());
    }

    private static TestContext CreateTestContext()
    {
        return new TestContext();
    }

    private static async Task CreateEncryptedFileAsync(
        string filePath,
        byte[] content,
        DocumentEncryptionService encryptionService)
    {
        string directory =
            Path.GetDirectoryName(
                filePath)!;

        Directory.CreateDirectory(
            directory);

        await using var source =
            new MemoryStream(
                content);

        await using var destination =
            new FileStream(
                filePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true);

        await encryptionService.EncryptAsync(
            source,
            destination);
    }

    private sealed class TestContext : IDisposable
    {
        public string RootDirectory { get; }

        public DeskVaultDataPaths DataPaths { get; }

        public DocumentArtifactPathResolver PathResolver { get; }

        public DocumentEncryptionService EncryptionService { get; }

        public EncryptedDocumentReader Reader { get; }

        public TestContext()
        {
            RootDirectory =
                Path.Combine(
                    Path.GetTempPath(),
                    "DeskVaultTests",
                    Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(
                RootDirectory);

            DataPaths =
                new DeskVaultDataPaths(
                    RootDirectory);

            Directory.CreateDirectory(
                DataPaths.DocumentsDirectory);

            PathResolver =
                new DocumentArtifactPathResolver(
                    DataPaths);

            byte[] key =
                RandomNumberGenerator.GetBytes(32);

            EncryptionService =
                new DocumentEncryptionService(
                    new TestEncryptionKeyService(
                        key),
                    NullLogger<DocumentEncryptionService>.Instance);

            Reader =
                new EncryptedDocumentReader(
                    EncryptionService,
                    PathResolver,
                    NullLogger<EncryptedDocumentReader>.Instance);
        }

        public void Dispose()
        {
            if (Directory.Exists(
                    RootDirectory))
            {
                Directory.Delete(
                    RootDirectory,
                    recursive: true);
            }
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
