using DeskVault.Application.Documents.Processing;
using DeskVault.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace DeskVault.Infrastructure.Tests;

public sealed class DocumentEncryptionServiceTests
{
    [Fact]
    public async Task EncryptAndDecryptAsync_WhenContentIsValid_RestoresOriginalContent()
    {
        byte[] originalContent =
            Encoding.UTF8.GetBytes(
                "DeskVault encryption test document.");

        DocumentEncryptionService service =
            CreateService();

        await using var source =
            new MemoryStream(originalContent);

        await using var encrypted =
            new MemoryStream();

        await service.EncryptAsync(
            source,
            encrypted);

        encrypted.Position = 0;

        await using var decrypted =
            new MemoryStream();

        await service.DecryptAsync(
            encrypted,
            decrypted);

        Assert.Equal(
            originalContent,
            decrypted.ToArray());
    }

    [Fact]
    public async Task DecryptAsync_UsesExistingKeyWithoutProvisioning()
    {
        byte[] originalContent =
            Encoding.UTF8.GetBytes(
                "DeskVault strict key retrieval test.");

        byte[] key =
            RandomNumberGenerator.GetBytes(32);

        Guid keyId = Guid.NewGuid();

        try
        {
            var encryptionService =
                new DocumentEncryptionService(
                    new TestEncryptionKeyService(key, keyId),
                    NullLogger<DocumentEncryptionService>.Instance);

            await using var source =
                new MemoryStream(originalContent);

            await using var encrypted =
                new MemoryStream();

            await encryptionService.EncryptAsync(
                source,
                encrypted);

            encrypted.Position = 0;

            var decryptionService =
                new DocumentEncryptionService(
                    new ExistingKeyOnlyTestEncryptionKeyService(key, keyId),
                    NullLogger<DocumentEncryptionService>.Instance);

            await using var decrypted =
                new MemoryStream();

            await decryptionService.DecryptAsync(
                encrypted,
                decrypted);

            Assert.Equal(
                originalContent,
                decrypted.ToArray());
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    [Fact]
    public async Task DecryptAsync_WhenReadingLegacyVersionTwoDocument_WithUnversionedKey_Succeeds()
    {
        byte[] originalContent =
            Encoding.UTF8.GetBytes(
                "Existing DeskVault version-two encrypted document.");

        byte[] key =
            RandomNumberGenerator.GetBytes(
                EncryptionKeyMaterial.KeySizeBytes);

        byte[] nonce =
            RandomNumberGenerator.GetBytes(12);

        byte[] tag = new byte[16];

        byte[] ciphertext =
            new byte[originalContent.Length];

        byte[] encryptedDocument =
            new byte[
                8 +
                sizeof(int) +
                nonce.Length +
                tag.Length +
                ciphertext.Length];

        Guid keyId = Guid.NewGuid();

        try
        {
            using (var aes = new AesGcm(key, tag.Length))
            {
                aes.Encrypt(
                    nonce,
                    originalContent,
                    ciphertext,
                    tag);
            }

            // Construct the existing version-two document header.
            BinaryPrimitives.WriteUInt32LittleEndian(
                encryptedDocument.AsSpan(0, sizeof(uint)),
                0x44564C54);

            BinaryPrimitives.WriteUInt32LittleEndian(
                encryptedDocument.AsSpan(sizeof(uint), sizeof(uint)),
                2);

            // Construct the original length/nonce/tag/ciphertext chunk.
            int lengthOffset = 8;
            int nonceOffset = lengthOffset + sizeof(int);
            int tagOffset = nonceOffset + nonce.Length;
            int ciphertextOffset = tagOffset + tag.Length;

            BinaryPrimitives.WriteInt32LittleEndian(
                encryptedDocument.AsSpan(lengthOffset, sizeof(int)),
                ciphertext.Length);

            nonce.CopyTo(
                encryptedDocument.AsSpan(nonceOffset, nonce.Length));

            tag.CopyTo(
                encryptedDocument.AsSpan(tagOffset, tag.Length));

            ciphertext.CopyTo(
                encryptedDocument.AsSpan(
                    ciphertextOffset,
                    ciphertext.Length));

            var service =
                new DocumentEncryptionService(
                    new ExistingKeyOnlyTestEncryptionKeyService(
                        key,
                        keyId,
                        keyVersion: 0),
                    NullLogger<DocumentEncryptionService>.Instance);

            await using var source =
                new MemoryStream(encryptedDocument);

            await using var destination =
                new MemoryStream();

            await service.DecryptAsync(
                source,
                destination);

            Assert.Equal(
                originalContent,
                destination.ToArray());
        }
        finally
        {
            CryptographicOperations.ZeroMemory(originalContent);
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(nonce);
            CryptographicOperations.ZeroMemory(tag);
            CryptographicOperations.ZeroMemory(ciphertext);
            CryptographicOperations.ZeroMemory(encryptedDocument);
        }
    }

    [Fact]
    public async Task DecryptAsync_WhenKeyIdentityDiffers_ThrowsAuthenticationTagMismatchException()
    {
        byte[] originalContent =
            Encoding.UTF8.GetBytes(
                "DeskVault key identity validation test.");

        byte[] key =
            RandomNumberGenerator.GetBytes(
                EncryptionKeyMaterial.KeySizeBytes);

        Guid encryptionKeyId =
            Guid.Parse("11111111-1111-1111-1111-111111111111");

        Guid decryptionKeyId =
            Guid.Parse("22222222-2222-2222-2222-222222222222");

        try
        {
            var encryptionService =
                new DocumentEncryptionService(
                    new TestEncryptionKeyService(
                        key,
                        encryptionKeyId),
                    NullLogger<DocumentEncryptionService>.Instance);

            await using var source =
                new MemoryStream(originalContent);

            await using var encrypted =
                new MemoryStream();

            await encryptionService.EncryptAsync(
                source,
                encrypted);

            encrypted.Position = 0;

            var decryptionService =
                new DocumentEncryptionService(
                    new ExistingKeyOnlyTestEncryptionKeyService(
                        key,
                        decryptionKeyId,
                        keyVersion: 1),
                    NullLogger<DocumentEncryptionService>.Instance);

            await using var destination =
                new MemoryStream();

            await Assert.ThrowsAsync<AuthenticationTagMismatchException>(
                () => decryptionService.DecryptAsync(
                    encrypted,
                    destination));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(originalContent);
            CryptographicOperations.ZeroMemory(key);
        }
    }

    [Fact]
    public async Task DecryptAsync_WhenKeyVersionDiffers_ThrowsAuthenticationTagMismatchException()
    {
        byte[] originalContent =
            Encoding.UTF8.GetBytes(
                "DeskVault key version validation test.");

        byte[] key =
            RandomNumberGenerator.GetBytes(
                EncryptionKeyMaterial.KeySizeBytes);

        Guid keyId =
            Guid.Parse("33333333-3333-3333-3333-333333333333");

        try
        {
            var encryptionService =
                new DocumentEncryptionService(
                    new TestEncryptionKeyService(
                        key,
                        keyId),
                    NullLogger<DocumentEncryptionService>.Instance);

            await using var source =
                new MemoryStream(originalContent);

            await using var encrypted =
                new MemoryStream();

            await encryptionService.EncryptAsync(
                source,
                encrypted);

            encrypted.Position = 0;

            var decryptionService =
                new DocumentEncryptionService(
                    new ExistingKeyOnlyTestEncryptionKeyService(
                        key,
                        keyId,
                        keyVersion: 2),
                    NullLogger<DocumentEncryptionService>.Instance);

            await using var destination =
                new MemoryStream();

            await Assert.ThrowsAsync<AuthenticationTagMismatchException>(
                () => decryptionService.DecryptAsync(
                    encrypted,
                    destination));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(originalContent);
            CryptographicOperations.ZeroMemory(key);
        }
    }

    [Fact]
    public async Task EncryptAndDecryptAsync_WhenContentIsEmpty_RestoresEmptyContent()
    {
        byte[] originalContent = [];

        DocumentEncryptionService service =
            CreateService();

        await using var source =
            new MemoryStream(originalContent);

        await using var encrypted =
            new MemoryStream();

        await service.EncryptAsync(
            source,
            encrypted);

        encrypted.Position = 0;

        await using var decrypted =
            new MemoryStream();

        await service.DecryptAsync(
            encrypted,
            decrypted);

        Assert.Empty(
            decrypted.ToArray());
    }

    [Fact]
    public async Task EncryptAsync_WritesDeskVaultFormatHeader()
    {
        DocumentEncryptionService service =
            CreateService();

        await using var source =
            new MemoryStream(
                Encoding.UTF8.GetBytes("DeskVault"));

        await using var encrypted =
            new MemoryStream();

        await service.EncryptAsync(
            source,
            encrypted);

        byte[] result =
            encrypted.ToArray();

        Assert.True(
            result.Length >= 8);

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
            BitConverter.ToUInt32(
                result,
                4));
    }

    [Fact]
    public async Task DecryptAsync_WhenMagicIsInvalid_ThrowsCryptographicException()
    {
        DocumentEncryptionService service =
            CreateService();

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
            () =>
                service.DecryptAsync(
                    source,
                    destination));
    }

    [Fact]
    public async Task DecryptAsync_WhenVersionIsUnsupported_ThrowsCryptographicException()
    {
        DocumentEncryptionService service =
            CreateService();

        byte[] unsupportedVersionHeader =
        [
            0x54, 0x4C, 0x56, 0x44,
            0xFF, 0x00, 0x00, 0x00
        ];

        await using var source =
            new MemoryStream(
                unsupportedVersionHeader);

        await using var destination =
            new MemoryStream();

        await Assert.ThrowsAsync<CryptographicException>(
            () =>
                service.DecryptAsync(
                    source,
                    destination));
    }

    [Fact]
    public async Task DecryptAsync_WhenCiphertextIsTampered_ThrowsAuthenticationTagMismatchException()
    {
        DocumentEncryptionService service =
            CreateService();

        byte[] originalContent =
            Encoding.UTF8.GetBytes(
                "Sensitive DeskVault document content.");

        await using var source =
            new MemoryStream(originalContent);

        await using var encrypted =
            new MemoryStream();

        await service.EncryptAsync(
            source,
            encrypted);

        byte[] encryptedBytes =
            encrypted.ToArray();

        Assert.True(
            encryptedBytes.Length > 24);

        encryptedBytes[^1] ^= 0xFF;

        await using var tamperedSource =
            new MemoryStream(encryptedBytes);

        await using var destination =
            new MemoryStream();

        await Assert.ThrowsAsync<AuthenticationTagMismatchException>(
            () =>
                service.DecryptAsync(
                    tamperedSource,
                    destination));
    }

    [Fact]
    public async Task DecryptAsync_WhenEncryptedDocumentIsTruncated_ThrowsCryptographicException()
    {
        DocumentEncryptionService service =
            CreateService();

        byte[] originalContent =
            Encoding.UTF8.GetBytes(
                "DeskVault truncated document test.");

        await using var source =
            new MemoryStream(originalContent);

        await using var encrypted =
            new MemoryStream();

        await service.EncryptAsync(
            source,
            encrypted);

        byte[] encryptedBytes =
            encrypted.ToArray();

        Assert.True(
            encryptedBytes.Length > 8);

        byte[] truncatedBytes =
            encryptedBytes[..^1];

        await using var truncatedSource =
            new MemoryStream(
                truncatedBytes);

        await using var destination =
            new MemoryStream();

        await Assert.ThrowsAsync<CryptographicException>(
            () =>
                service.DecryptAsync(
                    truncatedSource,
                    destination));
    }

    [Fact]
    public async Task DecryptAsync_WhenPlaintextExceedsMaximum_ThrowsResourceLimitExceededException()
    {
        DocumentEncryptionService service =
            CreateService();

        byte[] originalContent =
            Encoding.UTF8.GetBytes(
                "DeskVault resource limit test.");

        await using var source =
            new MemoryStream(originalContent);

        await using var encrypted =
            new MemoryStream();

        await service.EncryptAsync(
            source,
            encrypted);

        encrypted.Position = 0;

        await using var destination =
            new MemoryStream();

        long maximumPlaintextBytes =
            originalContent.Length - 1;

        ResourceLimitExceededException exception =
            await Assert.ThrowsAsync<ResourceLimitExceededException>(
                () =>
                    service.DecryptAsync(
                        encrypted,
                        destination,
                        maximumPlaintextBytes: maximumPlaintextBytes));

        Assert.Equal(
            maximumPlaintextBytes,
            exception.LimitBytes);

        Assert.Equal(
            originalContent.Length,
            exception.AttemptedBytes);

        Assert.Empty(
            destination.ToArray());
    }

    [Fact]
    public async Task DecryptAsync_WhenCumulativePlaintextExceedsMaximum_ThrowsResourceLimitExceededException()
    {
        DocumentEncryptionService service =
            CreateService();

        byte[] originalContent =
            new byte[(1024 * 1024) + 1];

        Array.Fill(
            originalContent,
            (byte)'A');

        await using var source =
            new MemoryStream(originalContent);

        await using var encrypted =
            new MemoryStream();

        await service.EncryptAsync(
            source,
            encrypted);

        encrypted.Position = 0;

        await using var destination =
            new MemoryStream();

        long maximumPlaintextBytes =
            1024 * 1024;

        ResourceLimitExceededException exception =
            await Assert.ThrowsAsync<ResourceLimitExceededException>(
                () =>
                    service.DecryptAsync(
                        encrypted,
                        destination,
                        maximumPlaintextBytes: maximumPlaintextBytes));

        Assert.Equal(
            maximumPlaintextBytes,
            exception.LimitBytes);

        Assert.Equal(
            originalContent.Length,
            exception.AttemptedBytes);

        Assert.Equal(
            maximumPlaintextBytes,
            destination.Length);
    }

    [Fact]
    public async Task DecryptAsync_WhenPlaintextExactlyMatchesMaximum_Succeeds()
    {
        DocumentEncryptionService service =
            CreateService();

        byte[] originalContent =
            new byte[1024 * 1024];

        Array.Fill(
            originalContent,
            (byte)'A');

        await using var source =
            new MemoryStream(originalContent);

        await using var encrypted =
            new MemoryStream();

        await service.EncryptAsync(
            source,
            encrypted);

        encrypted.Position = 0;

        await using var destination =
            new MemoryStream();

        await service.DecryptAsync(
            encrypted,
            destination,
            maximumPlaintextBytes: originalContent.Length);

        Assert.Equal(
            originalContent,
            destination.ToArray());

        Assert.Equal(
            originalContent.Length,
            destination.Length);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task DecryptAsync_WhenMaximumPlaintextBytesIsNotPositive_ThrowsArgumentOutOfRangeException(
    long maximumPlaintextBytes)
    {
        DocumentEncryptionService service =
            CreateService();

        await using var source =
            new MemoryStream();

        await using var destination =
            new MemoryStream();

        ArgumentOutOfRangeException exception =
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                () =>
                    service.DecryptAsync(
                        source,
                        destination,
                        maximumPlaintextBytes: maximumPlaintextBytes));

        Assert.Equal(
            "maximumPlaintextBytes",
            exception.ParamName);

        Assert.Contains(
            "Maximum plaintext bytes must be greater than zero.",
            exception.Message);
    }

    [Fact]
    public async Task DecryptAsync_WhenCancellationOccursDuringDecryption_ThrowsOperationCanceledException()
    {
        DocumentEncryptionService service =
            CreateService();

        byte[] originalContent =
            new byte[(1024 * 1024) * 2];

        Array.Fill(
            originalContent,
            (byte)'A');

        await using var source =
            new MemoryStream(originalContent);

        await using var encrypted =
            new MemoryStream();

        await service.EncryptAsync(
            source,
            encrypted);

        encrypted.Position = 0;

        using var cancellationTokenSource =
            new CancellationTokenSource();

        await using var destination =
            new MemoryStream();

        await using var cancellingSource =
            new CancellingReadStream(
                encrypted,
                cancellationTokenSource);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                service.DecryptAsync(
                    cancellingSource,
                    destination,
                    cancellationTokenSource.Token,
                    maximumPlaintextBytes: originalContent.Length));
    }

    [Fact]
    public async Task EncryptAsync_WhenCancellationIsRequested_ThrowsOperationCanceledException()
    {
        DocumentEncryptionService service =
            CreateService();

        await using var source =
            new MemoryStream(
                Encoding.UTF8.GetBytes(
                    "Cancellation test."));

        await using var destination =
            new MemoryStream();

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                service.EncryptAsync(
                    source,
                    destination,
                    cancellationTokenSource.Token));
    }

    [Fact]
    public async Task DecryptAsync_WhenCancellationIsRequested_ThrowsOperationCanceledException()
    {
        DocumentEncryptionService service =
            CreateService();

        await using var source =
            new MemoryStream(
                Encoding.UTF8.GetBytes(
                    "Cancellation test."));

        await using var encrypted =
            new MemoryStream();

        await service.EncryptAsync(
            source,
            encrypted);

        encrypted.Position = 0;

        await using var destination =
            new MemoryStream();

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                service.DecryptAsync(
                    encrypted,
                    destination,
                    cancellationTokenSource.Token));
    }

    private sealed class CancellingReadStream
        : Stream
    {
        private readonly Stream _inner;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private int _readCount;

        public CancellingReadStream(
            Stream inner,
            CancellationTokenSource cancellationTokenSource)
        {
            _inner = inner;
            _cancellationTokenSource =
                cancellationTokenSource;
        }

        public override bool CanRead =>
            _inner.CanRead;

        public override bool CanSeek =>
            _inner.CanSeek;

        public override bool CanWrite =>
            false;

        public override long Length =>
            _inner.Length;

        public override long Position
        {
            get => _inner.Position;
            set => _inner.Position = value;
        }

        public override void Flush()
        {
            _inner.Flush();
        }

        public override int Read(
            byte[] buffer,
            int offset,
            int count)
        {
            return _inner.Read(
                buffer,
                offset,
                count);
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            int bytesRead =
                await _inner.ReadAsync(
                    buffer,
                    cancellationToken);

            _readCount++;

            if (_readCount == 4)
            {
                _cancellationTokenSource.Cancel();
            }

            return bytesRead;
        }

        public override long Seek(
            long offset,
            SeekOrigin origin)
        {
            return _inner.Seek(
                offset,
                origin);
        }

        public override void SetLength(
            long value)
        {
            _inner.SetLength(
                value);
        }

        public override void Write(
            byte[] buffer,
            int offset,
            int count)
        {
            throw new NotSupportedException();
        }
    }

    private static DocumentEncryptionService CreateService()
    {
        byte[] key =
            RandomNumberGenerator.GetBytes(32);

        return new DocumentEncryptionService(
            new TestEncryptionKeyService(key),
            NullLogger<DocumentEncryptionService>.Instance);
    }

    private sealed class ExistingKeyOnlyTestEncryptionKeyService
        : IEncryptionKeyService
    {
        private readonly byte[] _key;
        private readonly Guid _keyId;
        private readonly int _keyVersion;

        public ExistingKeyOnlyTestEncryptionKeyService(
            byte[] key,
            Guid? keyId = null,
            int keyVersion = 1)
        {
            _key = key.ToArray();
            _keyId = keyId ?? Guid.NewGuid();
            _keyVersion = keyVersion;
        }

        public Task<EncryptionKeyMaterial> GetKeyAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                CreateMaterial());
        }

        public Task<EncryptionKeyMaterial> GetOrCreateKeyAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            throw new InvalidOperationException(
                "Document decryption must not provision an encryption key.");
        }

        private EncryptionKeyMaterial CreateMaterial()
        {
            return new EncryptionKeyMaterial(
                _key.ToArray(),
                _keyId,
                _keyVersion);
        }
    }

    private sealed class TestEncryptionKeyService
        : IEncryptionKeyService
    {
        private readonly byte[] _key;
        private readonly Guid _keyId;

        public TestEncryptionKeyService(
            byte[] key,
            Guid? keyId = null)
        {
            _key = key;
            _keyId = keyId ?? Guid.NewGuid();
        }

        public Task<EncryptionKeyMaterial> GetOrCreateKeyAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                CreateMaterial());
        }

        public Task<EncryptionKeyMaterial> GetKeyAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                CreateMaterial());
        }

        private EncryptionKeyMaterial CreateMaterial()
        {
            return new EncryptionKeyMaterial(
                _key.ToArray(),
                _keyId,
                keyVersion: 1);
        }
    }
}
