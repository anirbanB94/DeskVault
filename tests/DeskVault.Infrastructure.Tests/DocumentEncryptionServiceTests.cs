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

        await service.EncryptLegacyVersion2Async(
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

            await encryptionService.EncryptLegacyVersion2Async(
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

            await encryptionService.EncryptLegacyVersion2Async(
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

            await encryptionService.EncryptLegacyVersion2Async(
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

        await service.EncryptLegacyVersion2Async(
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

        await service.EncryptLegacyVersion2Async(
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

        await service.EncryptLegacyVersion2Async(
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

        await service.EncryptLegacyVersion2Async(
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

        await service.EncryptLegacyVersion2Async(
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

        await service.EncryptLegacyVersion2Async(
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

        await service.EncryptLegacyVersion2Async(
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

        await service.EncryptLegacyVersion2Async(
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
                service.EncryptLegacyVersion2Async(
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

        await service.EncryptLegacyVersion2Async(
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

    [Fact]
    public async Task EncryptAndDecryptAsync_ForDocumentVersionThree_RestoresContent()
    {
        byte[] originalContent =
            Encoding.UTF8.GetBytes(
                "DeskVault version-three record test.");

        Guid documentId = Guid.NewGuid();

        DocumentEncryptionService service = CreateService();

        await using var source =
            new MemoryStream(originalContent);

        await using var encrypted =
            new MemoryStream();

        await service.EncryptAsync(
            source,
            encrypted,
            documentId);

        byte[] encryptedBytes = encrypted.ToArray();

        Assert.Equal(
            3u,
            BinaryPrimitives.ReadUInt32LittleEndian(
                encryptedBytes.AsSpan(4, sizeof(uint))));

        encrypted.Position = 0;

        await using var decrypted =
            new MemoryStream();

        await service.DecryptAsync(
            encrypted,
            decrypted,
            documentId);

        Assert.Equal(
            originalContent,
            decrypted.ToArray());
    }

    [Fact]
    public async Task EncryptAndDecryptAsync_ForMultiChunkVersionThree_RestoresContent()
    {
        const int chunkSize = 1024 * 1024;

        byte[] originalContent = new byte[chunkSize + 37];
        Array.Fill(originalContent, (byte)'M');

        Guid documentId = Guid.NewGuid();
        DocumentEncryptionService service = CreateService();

        await using var source = new MemoryStream(originalContent);
        await using var encrypted = new MemoryStream();

        await service.EncryptAsync(
            source,
            encrypted,
            documentId);

        byte[] encryptedBytes = encrypted.ToArray();

        Assert.Equal(
            3u,
            BinaryPrimitives.ReadUInt32LittleEndian(
                encryptedBytes.AsSpan(4, sizeof(uint))));

        encrypted.Position = 0;

        await using var decrypted = new MemoryStream();

        await service.DecryptAsync(
            encrypted,
            decrypted,
            documentId);

        Assert.Equal(originalContent, decrypted.ToArray());
    }

    [Fact]
    public async Task DecryptAsync_WhenVersionThreeRecordContainsTrailingBytes_ThrowsCryptographicException()
    {
        Guid documentId = Guid.NewGuid();
        DocumentEncryptionService service = CreateService();

        await using var source =
            new MemoryStream(
                Encoding.UTF8.GetBytes("Trailing data validation test."));

        await using var encrypted = new MemoryStream();

        await service.EncryptAsync(
            source,
            encrypted,
            documentId);

        byte[] recordWithTrailingData = encrypted.ToArray();

        Array.Resize(
            ref recordWithTrailingData,
            recordWithTrailingData.Length + 1);

        recordWithTrailingData[^1] = 0xA5;

        await using var tamperedSource =
            new MemoryStream(recordWithTrailingData);

        await using var destination = new MemoryStream();

        await Assert.ThrowsAsync<CryptographicException>(
            () => service.DecryptAsync(
                tamperedSource,
                destination,
                documentId));
    }

    [Fact]
    public async Task DecryptAsync_WhenVersionThreeChunkIsSubstitutedFromAnotherDocument_ThrowsAuthenticationTagMismatchException()
    {
        byte[] firstContent =
            Encoding.UTF8.GetBytes("Document A payload.");

        byte[] secondContent =
            Encoding.UTF8.GetBytes("Document B payload.");

        Assert.Equal(firstContent.Length, secondContent.Length);

        Guid firstDocumentId = Guid.NewGuid();
        Guid secondDocumentId = Guid.NewGuid();

        DocumentEncryptionService service = CreateService();

        await using var firstSource = new MemoryStream(firstContent);
        await using var secondSource = new MemoryStream(secondContent);
        await using var firstEncrypted = new MemoryStream();
        await using var secondEncrypted = new MemoryStream();

        await service.EncryptAsync(
            firstSource,
            firstEncrypted,
            firstDocumentId);

        await service.EncryptAsync(
            secondSource,
            secondEncrypted,
            secondDocumentId);

        byte[] firstRecord = firstEncrypted.ToArray();
        byte[] secondRecord = secondEncrypted.ToArray();

        const int headerSize = 8;
        const int chunkMetadataSize = sizeof(int) + 12 + 16;

        int dataRecordLength =
            chunkMetadataSize + firstContent.Length;

        Assert.Equal(firstRecord.Length, secondRecord.Length);

        // Substitute document B's complete data record into document A's record.
        // Keep document A's header and authenticated final record unchanged.
        Buffer.BlockCopy(
            secondRecord,
            headerSize,
            firstRecord,
            headerSize,
            dataRecordLength);

        await using var substitutedSource = new MemoryStream(firstRecord);
        await using var destination = new MemoryStream();

        await Assert.ThrowsAsync<AuthenticationTagMismatchException>(
            () => service.DecryptAsync(
                substitutedSource,
                destination,
                firstDocumentId));
    }

    [Fact]
    public async Task DecryptAsync_WhenLegacyVersionTwoRecordIsTruncatedAtEachStructuralBoundary_RejectsEveryIncompletePrefix()
    {
        DocumentEncryptionService service = CreateService();

        await using var source =
            new MemoryStream(
                Encoding.UTF8.GetBytes("Legacy record boundary validation."));

        await using var encrypted = new MemoryStream();

        // The legacy overload intentionally creates a format-2 test fixture.
        await service.EncryptLegacyVersion2Async(
            source,
            encrypted);

        byte[] encryptedBytes = encrypted.ToArray();

        for (int prefixLength = 0;
             prefixLength < encryptedBytes.Length;
             prefixLength++)
        {
            // A header-only version-2 artifact is the valid legacy representation
            // of an empty document, so it must not be treated as a truncation.
            if (prefixLength == 8)
            {
                continue;
            }

            await using var truncatedSource =
                new MemoryStream(
                    encryptedBytes[..prefixLength]);

            await using var destination = new MemoryStream();

            await Assert.ThrowsAsync<CryptographicException>(
                () => service.DecryptAsync(
                    truncatedSource,
                    destination));
        }
    }

    [Fact]
    public async Task EncryptAndDecryptAsync_ForEmptyVersionThreeDocument_PreservesEmptyContent()
    {
        Guid documentId = Guid.NewGuid();

        DocumentEncryptionService service = CreateService();

        await using var source =
            new MemoryStream([]);

        await using var encrypted =
            new MemoryStream();

        await service.EncryptAsync(
            source,
            encrypted,
            documentId);

        byte[] encryptedBytes = encrypted.ToArray();

        // Header plus authenticated terminal record:
        // 8-byte header + 4-byte length + 12-byte nonce + 16-byte tag.
        Assert.Equal(
            8 + sizeof(int) + 12 + 16,
            encryptedBytes.Length);

        Assert.Equal(
            0,
            BinaryPrimitives.ReadInt32LittleEndian(
                encryptedBytes.AsSpan(8, sizeof(int))));

        encrypted.Position = 0;

        await using var decrypted =
            new MemoryStream();

        await service.DecryptAsync(
            encrypted,
            decrypted,
            documentId);

        Assert.Empty(decrypted.ToArray());
    }

    [Fact]
    public async Task DecryptAsync_WhenVersionThreeDocumentIdentityDiffers_ThrowsAuthenticationTagMismatchException()
    {
        Guid encryptionDocumentId = Guid.NewGuid();
        Guid decryptionDocumentId = Guid.NewGuid();

        DocumentEncryptionService service = CreateService();

        await using var source =
            new MemoryStream(
                Encoding.UTF8.GetBytes(
                    "Document identity binding test."));

        await using var encrypted =
            new MemoryStream();

        await service.EncryptAsync(
            source,
            encrypted,
            encryptionDocumentId);

        encrypted.Position = 0;

        await using var destination =
            new MemoryStream();

        await Assert.ThrowsAsync<AuthenticationTagMismatchException>(
            () => service.DecryptAsync(
                encrypted,
                destination,
                decryptionDocumentId));
    }

    [Fact]
    public async Task DecryptAsync_WhenVersionThreeRecordIsTruncatedAtAnyByteBoundary_ThrowsCryptographicException()
    {
        Guid documentId = Guid.NewGuid();

        DocumentEncryptionService service = CreateService();

        await using var source =
            new MemoryStream(
                Encoding.UTF8.GetBytes(
                    "Every byte boundary must be validated."));

        await using var encrypted =
            new MemoryStream();

        await service.EncryptAsync(
            source,
            encrypted,
            documentId);

        byte[] encryptedBytes = encrypted.ToArray();

        // Every proper prefix truncates at least one byte from the
        // header, a chunk field, or the authenticated terminal record.
        for (int prefixLength = 0;
             prefixLength < encryptedBytes.Length;
             prefixLength++)
        {
            await using var truncatedSource =
                new MemoryStream(
                    encryptedBytes[..prefixLength]);

            await using var destination =
                new MemoryStream();

            await Assert.ThrowsAsync<CryptographicException>(
                () => service.DecryptAsync(
                    truncatedSource,
                    destination,
                    documentId));
        }
    }

    [Fact]
    public async Task DecryptAsync_WhenVersionThreeRecordEndsAtACompleteChunkBoundary_ThrowsCryptographicException()
    {
        const int chunkSize = 1024 * 1024;
        const int terminalRecordSize = sizeof(int) + 12 + 16;

        byte[] originalContent =
            new byte[chunkSize + 37];

        Array.Fill(originalContent, (byte)'A');

        Guid documentId = Guid.NewGuid();

        DocumentEncryptionService service = CreateService();

        await using var source =
            new MemoryStream(originalContent);

        await using var encrypted =
            new MemoryStream();

        await service.EncryptAsync(
            source,
            encrypted,
            documentId);

        byte[] encryptedBytes = encrypted.ToArray();

        // Remove the entire terminal record while leaving every data
        // chunk intact. EOF at that boundary must not indicate success.
        byte[] truncatedBytes =
            encryptedBytes[..^terminalRecordSize];

        await using var truncatedSource =
            new MemoryStream(truncatedBytes);

        await using var destination =
            new MemoryStream();

        await Assert.ThrowsAsync<CryptographicException>(
            () => service.DecryptAsync(
                truncatedSource,
                destination,
                documentId));
    }

    [Fact]
    public async Task DecryptAsync_WhenVersionThreeChunksAreReordered_ThrowsAuthenticationTagMismatchException()
    {
        const int chunkSize = 1024 * 1024;
        const int recordOverhead = sizeof(int) + 12 + 16;
        const int terminalRecordSize = recordOverhead;
        const int secondChunkSize = 7;

        byte[] originalContent =
            new byte[chunkSize + secondChunkSize];

        Array.Fill(
            originalContent,
            (byte)'A',
            0,
            chunkSize);

        Array.Fill(
            originalContent,
            (byte)'B',
            chunkSize,
            secondChunkSize);

        Guid documentId = Guid.NewGuid();

        DocumentEncryptionService service = CreateService();

        await using var source =
            new MemoryStream(originalContent);

        await using var encrypted =
            new MemoryStream();

        await service.EncryptAsync(
            source,
            encrypted,
            documentId);

        byte[] encryptedBytes = encrypted.ToArray();

        int firstRecordOffset = 8;
        int firstRecordLength = recordOverhead + chunkSize;

        int secondRecordOffset =
            firstRecordOffset + firstRecordLength;

        int secondRecordLength =
            recordOverhead + secondChunkSize;

        int finalRecordOffset =
            secondRecordOffset + secondRecordLength;

        Assert.Equal(
            encryptedBytes.Length - terminalRecordSize,
            finalRecordOffset);

        byte[] reorderedBytes =
            new byte[encryptedBytes.Length];

        // Preserve the header.
        encryptedBytes.AsSpan(0, 8)
            .CopyTo(reorderedBytes);

        // Reverse the two complete data records.
        encryptedBytes.AsSpan(
                secondRecordOffset,
                secondRecordLength)
            .CopyTo(
                reorderedBytes.AsSpan(firstRecordOffset));

        encryptedBytes.AsSpan(
                firstRecordOffset,
                firstRecordLength)
            .CopyTo(
                reorderedBytes.AsSpan(
                    firstRecordOffset + secondRecordLength));

        // The original final record is retained unchanged.
        encryptedBytes.AsSpan(
                finalRecordOffset,
                terminalRecordSize)
            .CopyTo(
                reorderedBytes.AsSpan(
                    finalRecordOffset));

        await using var reorderedSource =
            new MemoryStream(reorderedBytes);

        await using var destination =
            new MemoryStream();

        await Assert.ThrowsAsync<AuthenticationTagMismatchException>(
            () => service.DecryptAsync(
                reorderedSource,
                destination,
                documentId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1024 * 1024 + 1)]
    public async Task DecryptAsync_WhenLegacyVersionTwoChunkLengthIsInvalid_ThrowsCryptographicException(
        int chunkLength)
    {
        byte[] malformedRecord =
            new byte[8 + sizeof(int)];

        BinaryPrimitives.WriteUInt32LittleEndian(
            malformedRecord.AsSpan(0, sizeof(uint)),
            0x44564C54);

        BinaryPrimitives.WriteUInt32LittleEndian(
            malformedRecord.AsSpan(sizeof(uint), sizeof(uint)),
            2);

        BinaryPrimitives.WriteInt32LittleEndian(
            malformedRecord.AsSpan(8, sizeof(int)),
            chunkLength);

        DocumentEncryptionService service = CreateService();

        await using var source =
            new MemoryStream(malformedRecord);

        await using var destination =
            new MemoryStream();

        await Assert.ThrowsAsync<CryptographicException>(
            () => service.DecryptAsync(
                source,
                destination));
    }

    [Fact]
    public async Task DecryptAsync_WhenVersionThreeChunkLengthExceedsMaximum_ThrowsCryptographicException()
    {
        const int oversizedChunkLength = 1024 * 1024 + 1;

        byte[] malformedRecord =
            new byte[8 + sizeof(int)];

        BinaryPrimitives.WriteUInt32LittleEndian(
            malformedRecord.AsSpan(0, sizeof(uint)),
            0x44564C54);

        BinaryPrimitives.WriteUInt32LittleEndian(
            malformedRecord.AsSpan(sizeof(uint), sizeof(uint)),
            3);

        BinaryPrimitives.WriteInt32LittleEndian(
            malformedRecord.AsSpan(8, sizeof(int)),
            oversizedChunkLength);

        Guid documentId = Guid.NewGuid();

        DocumentEncryptionService service =
            CreateService();

        await using var source =
            new MemoryStream(malformedRecord);

        await using var destination =
            new MemoryStream();

        await Assert.ThrowsAsync<CryptographicException>(
            () => service.DecryptAsync(
                source,
                destination,
                documentId));
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
            _key = [.. key];
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
                [.. _key],
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
                [.. _key],
                _keyId,
                keyVersion: 1);
        }
    }
}
