using System.Security.Cryptography;
using System.Text;
using DeskVault.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeskVault.Infrastructure.Tests;

public sealed class EncryptedDocumentReaderTests
{
    [Fact]
    public async Task OpenReadAsync_WhenStoredFileIsValid_ReturnsDecryptedContent()
    {
        byte[] originalContent =
            Encoding.UTF8.GetBytes(
                "DeskVault encrypted document reader test.");

        string filePath =
            CreateTempFilePath();

        DocumentEncryptionService encryptionService =
            CreateEncryptionService();

        EncryptedDocumentReader reader =
            CreateReader(
                encryptionService);

        try
        {
            await CreateEncryptedFileAsync(
                filePath,
                originalContent,
                encryptionService);

            await using Stream result =
                await reader.OpenReadAsync(
                    filePath);

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
        finally
        {
            DeleteIfExists(
                filePath);
        }
    }

    [Fact]
    public async Task OpenReadAsync_WhenStoredFileIsTampered_ThrowsAuthenticationTagMismatchException()
    {
        byte[] originalContent =
            Encoding.UTF8.GetBytes(
                "DeskVault tampered document test.");

        string filePath =
            CreateTempFilePath();

        DocumentEncryptionService encryptionService =
            CreateEncryptionService();

        EncryptedDocumentReader reader =
            CreateReader(
                encryptionService);

        try
        {
            await CreateEncryptedFileAsync(
                filePath,
                originalContent,
                encryptionService);

            byte[] encryptedContent =
                await File.ReadAllBytesAsync(
                    filePath);

            Assert.True(
                encryptedContent.Length > 24);

            encryptedContent[^1] ^= 0xFF;

            await File.WriteAllBytesAsync(
                filePath,
                encryptedContent);

            await Assert.ThrowsAsync<AuthenticationTagMismatchException>(
                () =>
                    reader.OpenReadAsync(
                        filePath));
        }
        finally
        {
            DeleteIfExists(
                filePath);
        }
    }

    [Fact]
    public async Task OpenReadAsync_WhenStoredFileDoesNotExist_ThrowsFileNotFoundException()
    {
        EncryptedDocumentReader reader =
            CreateReader();

        string filePath =
            CreateTempFilePath();

        await Assert.ThrowsAsync<FileNotFoundException>(
            () =>
                reader.OpenReadAsync(
                    filePath));
    }

    [Fact]
    public async Task OpenReadAsync_WhenCancellationIsRequested_ThrowsOperationCanceledException()
    {
        byte[] originalContent =
            Encoding.UTF8.GetBytes(
                "DeskVault cancellation test.");

        string filePath =
            CreateTempFilePath();

        DocumentEncryptionService encryptionService =
            CreateEncryptionService();

        EncryptedDocumentReader reader =
            CreateReader(
                encryptionService);

        try
        {
            await CreateEncryptedFileAsync(
                filePath,
                originalContent,
                encryptionService);

            using var cancellationTokenSource =
                new CancellationTokenSource();

            cancellationTokenSource.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () =>
                    reader.OpenReadAsync(
                        filePath,
                        cancellationTokenSource.Token));
        }
        finally
        {
            DeleteIfExists(
                filePath);
        }
    }

    private static EncryptedDocumentReader CreateReader(
        DocumentEncryptionService? encryptionService = null)
    {
        encryptionService ??=
            CreateEncryptionService();

        return new EncryptedDocumentReader(
            encryptionService,
            NullLogger<EncryptedDocumentReader>.Instance);
    }

    private static DocumentEncryptionService CreateEncryptionService()
    {
        byte[] key =
            RandomNumberGenerator.GetBytes(32);

        return new DocumentEncryptionService(
            new TestEncryptionKeyService(key),
            NullLogger<DocumentEncryptionService>.Instance);
    }

    private static async Task CreateEncryptedFileAsync(
        string filePath,
        byte[] content,
        DocumentEncryptionService encryptionService)
    {
        await using var source =
            new MemoryStream(content);

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

    private static string CreateTempFilePath()
    {
        return Path.Combine(
            Path.GetTempPath(),
            $"{Guid.NewGuid():N}.dvault");
    }

    private static void DeleteIfExists(
        string filePath)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
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
                _key);
        }
    }
}
