using System.Security.Cryptography;
using System.Text;
using DeskVault.Infrastructure.Services;

namespace DeskVault.Infrastructure.Tests;

public sealed class DocumentEncryptionServiceTests
{
    [Fact]
    public async Task EncryptAndDecryptAsync_WhenContentIsValid_RestoresOriginalContent()
    {
        byte[] key = RandomNumberGenerator.GetBytes(32);
        byte[] originalContent =
            Encoding.UTF8.GetBytes(
                "DeskVault encryption test document.");

        var keyService = new TestEncryptionKeyService(key);
        var service = new DocumentEncryptionService(keyService);

        await using var source =
            new MemoryStream(originalContent);

        await using var encrypted =
            new MemoryStream();

        await service.EncryptAsync(source, encrypted);

        encrypted.Position = 0;

        await using var decrypted =
            new MemoryStream();

        await service.DecryptAsync(encrypted, decrypted);

        Assert.Equal(
            originalContent,
            decrypted.ToArray());
    }

    [Fact]
    public async Task EncryptAsync_WritesDeskVaultFormatHeader()
    {
        byte[] key = RandomNumberGenerator.GetBytes(32);

        var keyService = new TestEncryptionKeyService(key);
        var service = new DocumentEncryptionService(keyService);

        await using var source =
            new MemoryStream(
                Encoding.UTF8.GetBytes("DeskVault"));

        await using var encrypted =
            new MemoryStream();

        await service.EncryptAsync(source, encrypted);

        byte[] result = encrypted.ToArray();

        Assert.True(result.Length >= 8);

        Assert.Equal(
            (byte)0x54,
            result[0]);

        Assert.Equal(
            (byte)0x4C,
            result[1]);

        Assert.Equal(
            (byte)0x56,
            result[2]);

        Assert.Equal(
            (byte)0x44,
            result[3]);

        Assert.Equal(
            2u,
            BitConverter.ToUInt32(result, 4));
    }

    [Fact]
    public async Task DecryptAsync_WhenMagicIsInvalid_ThrowsCryptographicException()
    {
        byte[] key = RandomNumberGenerator.GetBytes(32);

        var keyService = new TestEncryptionKeyService(key);
        var service = new DocumentEncryptionService(keyService);

        byte[] invalidHeader =
        [
            0x00, 0x00, 0x00, 0x00,
            0x02, 0x00, 0x00, 0x00
        ];

        await using var source =
            new MemoryStream(invalidHeader);

        await using var destination =
            new MemoryStream();

        await Assert.ThrowsAsync<CryptographicException>(
            () => service.DecryptAsync(
                source,
                destination));
    }

    [Fact]
    public async Task DecryptAsync_WhenVersionIsUnsupported_ThrowsCryptographicException()
    {
        byte[] key = RandomNumberGenerator.GetBytes(32);

        var keyService = new TestEncryptionKeyService(key);
        var service = new DocumentEncryptionService(keyService);

        byte[] unsupportedVersionHeader =
        [
            0x54, 0x4C, 0x56, 0x44,
            0xFF, 0x00, 0x00, 0x00
        ];

        await using var source =
            new MemoryStream(unsupportedVersionHeader);

        await using var destination =
            new MemoryStream();

        await Assert.ThrowsAsync<CryptographicException>(
            () => service.DecryptAsync(
                source,
                destination));
    }

    private sealed class TestEncryptionKeyService : IEncryptionKeyService
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
