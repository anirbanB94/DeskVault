using System.Security.Cryptography;
using System.Text;
using DeskVault.Infrastructure.Services;

namespace DeskVault.Infrastructure.Tests;

public sealed class EncryptedDocumentReaderTests
{
    [Fact]
    public async Task OpenReadAsync_WhenStoredFileIsValid_ReturnsDecryptedContent()
    {
        byte[] key = RandomNumberGenerator.GetBytes(32);

        byte[] originalContent =
            Encoding.UTF8.GetBytes(
                "DeskVault encrypted document reader test.");

        string filePath = Path.Combine(
            Path.GetTempPath(),
            $"{Guid.NewGuid():N}.dvault");

        var keyService = new TestEncryptionKeyService(key);
        var encryptionService =
            new DocumentEncryptionService(keyService);

        var reader =
            new EncryptedDocumentReader(encryptionService);

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
                await reader.OpenReadAsync(filePath);

            Assert.Equal(
                0,
                result.Position);

            using var memory = new MemoryStream();

            await result.CopyToAsync(memory);

            Assert.Equal(
                originalContent,
                memory.ToArray());
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    private sealed class TestEncryptionKeyService
        : IEncryptionKeyService
    {
        private readonly byte[] _key;

        public TestEncryptionKeyService(byte[] key)
        {
            _key = key;
        }

        public Task<byte[]> GetOrCreateKeyAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_key);
        }
    }
}
