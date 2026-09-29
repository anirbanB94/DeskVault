using System.Security.Cryptography;
using System.Text;
using DeskVault.Application.Documents.Processing;
using DeskVault.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;

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
