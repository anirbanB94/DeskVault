using System.Security.Cryptography;
using System.Text;
using DeskVault.Infrastructure.Services;

namespace DeskVault.Infrastructure.Tests;

public sealed class EncryptedDocumentReaderTests
{
    [Fact]
    public async Task OpenReadAsync_WhenStoredFileIsValid_ReturnsDecryptedContent()
    {
        byte[] key =
            RandomNumberGenerator.GetBytes(32);

        byte[] originalContent =
            Encoding.UTF8.GetBytes(
                "DeskVault encrypted document reader test.");

        string filePath =
            Path.Combine(
                Path.GetTempPath(),
                $"{Guid.NewGuid():N}.dvault");

        var keyService =
            new TestEncryptionKeyService(key);

        var encryptionService =
            new DocumentEncryptionService(
                keyService);

        var reader =
            new EncryptedDocumentReader(
                encryptionService);

        try
        {
            await using (var source =
                new MemoryStream(originalContent))
            await using (var destination =
                new FileStream(
                    filePath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 81920,
                    useAsync: true))
            {
                await encryptionService.EncryptAsync(
                    source,
                    destination);
            }

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
            File.Delete(
                filePath);
        }
    }

    [Fact]
    public async Task OpenReadAsync_WhenStoredFileIsTampered_ThrowsCryptographicException()
    {
        byte[] key =
            RandomNumberGenerator.GetBytes(32);

        byte[] originalContent =
            Encoding.UTF8.GetBytes(
                "DeskVault tampered document test.");

        string filePath =
            Path.Combine(
                Path.GetTempPath(),
                $"{Guid.NewGuid():N}.dvault");

        var keyService =
            new TestEncryptionKeyService(key);

        var encryptionService =
            new DocumentEncryptionService(
                keyService);

        var reader =
            new EncryptedDocumentReader(
                encryptionService);

        try
        {
            await using (var source =
                new MemoryStream(originalContent))
            await using (var destination =
                new FileStream(
                    filePath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 81920,
                    useAsync: true))
            {
                await encryptionService.EncryptAsync(
                    source,
                    destination);
            }

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
            File.Delete(
                filePath);
        }
    }

    [Fact]
    public async Task OpenReadAsync_WhenStoredFileDoesNotExist_ThrowsFileNotFoundException()
    {
        byte[] key =
            RandomNumberGenerator.GetBytes(32);

        var keyService =
            new TestEncryptionKeyService(key);

        var encryptionService =
            new DocumentEncryptionService(
                keyService);

        var reader =
            new EncryptedDocumentReader(
                encryptionService);

        string filePath =
            Path.Combine(
                Path.GetTempPath(),
                $"{Guid.NewGuid():N}.dvault");

        await Assert.ThrowsAsync<FileNotFoundException>(
            () =>
                reader.OpenReadAsync(
                    filePath));
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
            return Task.FromResult(
                _key);
        }
    }
}
