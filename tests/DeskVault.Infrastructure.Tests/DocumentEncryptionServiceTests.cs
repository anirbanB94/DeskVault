using DeskVault.Application.Documents.Processing;
using DeskVault.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
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

        await service.EncryptLegacyVersion2Async(source, encrypted, TestContext.Current.CancellationToken);

        encrypted.Position = 0;

        await using var decrypted =
            new MemoryStream();

        await service.DecryptAsync(encrypted, decrypted, TestContext.Current.CancellationToken);

        Assert.Equal(
            originalContent,
            decrypted.ToArray());
    }

    [Fact]
    public async Task EncryptLegacyVersion2Async_ClearsPlaintextInputBufferAfterSuccessfulEncryption()
    {
        await AssertEncryptionInputBufferIsClearedAsync(
            PlaintextReadTermination.EndOfStream);
    }

    [Fact]
    public async Task EncryptLegacyVersion2Async_ClearsPlaintextInputBufferWhenSourceReadFails()
    {
        await AssertEncryptionInputBufferIsClearedAsync(
            PlaintextReadTermination.ThrowAfterContent);
    }

    [Fact]
    public async Task EncryptLegacyVersion2Async_ClearsPlaintextInputBufferWhenDestinationWriteFails()
    {
        await AssertEncryptionInputBufferIsClearedAsync(
            PlaintextReadTermination.EndOfStream,
            failDestinationWrite: true);
    }

    [Fact]
    public async Task EncryptLegacyVersion2Async_ClearsPlaintextInputBufferWhenCancellationOccursAfterRead()
    {
        await AssertEncryptionInputBufferIsClearedAsync(
            PlaintextReadTermination.CancelAfterContent);
    }

    [Fact]
    public async Task EncryptLegacyVersion2Async_ClearsReusablePlaintextInputBufferAcrossMultipleChunks()
    {
        await AssertEncryptionInputBufferIsClearedAsync(
            PlaintextReadTermination.EndOfStream,
            plaintextLength: (2 * 1024 * 1024) + 37);
    }

    [Fact]
    public async Task DecryptAsync_ClearsPlaintextChunkBufferAfterSuccessfulWrite()
    {
        var testData = await CreateEncryptedTestPayloadAsync();
        var observedBuffers = new List<byte[]>();

        try
        {
            var service = CreateDecryptionService(
                testData.Key,
                testData.KeyId,
                observedBuffers);

            await using var source =
                new MemoryStream(testData.EncryptedBytes);

            await using var destination = new MemoryStream();

            await service.DecryptAsync(source, destination, TestContext.Current.CancellationToken);

            Assert.Equal(
                testData.Plaintext,
                destination.ToArray());

            AssertSingleClearedPlaintextBuffer(observedBuffers);
        }
        finally
        {
            ClearDecryptionTestPayload(testData, observedBuffers);
        }
    }

    [Fact]
    public async Task DecryptAsync_ClearsPlaintextChunkBufferWhenDestinationWriteFails()
    {
        var testData = await CreateEncryptedTestPayloadAsync();
        var observedBuffers = new List<byte[]>();

        try
        {
            var service = CreateDecryptionService(
                testData.Key,
                testData.KeyId,
                observedBuffers);

            await using var source =
                new MemoryStream(testData.EncryptedBytes);

            await using var destination =
                new FailingDocumentWriteStream(
                    successfulWritesBeforeFailure: 0);

            await Assert.ThrowsAsync<IOException>(
                () => service.DecryptAsync(source, destination, TestContext.Current.CancellationToken));

            AssertSingleClearedPlaintextBuffer(observedBuffers);
        }
        finally
        {
            ClearDecryptionTestPayload(testData, observedBuffers);
        }
    }

    [Fact]
    public async Task DecryptAsync_ClearsPlaintextChunkBufferWhenAuthenticationFails()
    {
        var testData = await CreateEncryptedTestPayloadAsync();
        var observedBuffers = new List<byte[]>();

        try
        {
            // Corrupt the encrypted payload to force AES-GCM authentication failure.
            testData.EncryptedBytes[^1] ^= 0xFF;

            var service = CreateDecryptionService(
                testData.Key,
                testData.KeyId,
                observedBuffers);

            await using var source =
                new MemoryStream(testData.EncryptedBytes);

            await using var destination = new MemoryStream();

            await Assert.ThrowsAsync<AuthenticationTagMismatchException>(
                () => service.DecryptAsync(source, destination, TestContext.Current.CancellationToken));

            Assert.Equal(0, destination.Length);
            AssertSingleClearedPlaintextBuffer(observedBuffers);
        }
        finally
        {
            ClearDecryptionTestPayload(testData, observedBuffers);
        }
    }

    [Fact]
    public async Task DecryptAsync_ClearsPlaintextChunkBufferWhenEncryptedRecordIsTruncated()
    {
        var testData = await CreateEncryptedTestPayloadAsync();
        var observedBuffers = new List<byte[]>();

        try
        {
            var service = CreateDecryptionService(
                testData.Key,
                testData.KeyId,
                observedBuffers);

            // Truncate the final ciphertext byte, after the chunk's
            // plaintext buffer has been allocated but before authentication.
            await using var source = new MemoryStream(
                testData.EncryptedBytes[..^1]);

            await using var destination = new MemoryStream();

            await Assert.ThrowsAsync<CryptographicException>(
                () => service.DecryptAsync(source, destination, TestContext.Current.CancellationToken));

            Assert.Equal(0, destination.Length);
            AssertSingleClearedPlaintextBuffer(observedBuffers);
        }
        finally
        {
            ClearDecryptionTestPayload(testData, observedBuffers);
        }
    }

    [Fact]
    public async Task DecryptAsync_ClearsPlaintextChunkBufferWhenSourceReadFails()
    {
        var testData = await CreateEncryptedTestPayloadAsync();
        var observedBuffers = new List<byte[]>();

        try
        {
            var service = CreateDecryptionService(
                testData.Key,
                testData.KeyId,
                observedBuffers);

            // Legacy v2 has an 8-byte header followed by a 4-byte length,
            // 12-byte nonce, and 16-byte authentication tag.
            // Fail halfway through reading the ciphertext, after allocation.
            int bytesBeforeFailure =
                sizeof(uint) * 2
                + sizeof(int)
                + 12
                + 16
                + testData.Plaintext.Length / 2;

            await using var source =
                new FailingAfterByteLimitReadStream(
                    testData.EncryptedBytes,
                    bytesBeforeFailure);

            await using var destination = new MemoryStream();

            await Assert.ThrowsAsync<IOException>(
                () => service.DecryptAsync(source, destination, TestContext.Current.CancellationToken));

            Assert.Equal(0, destination.Length);
            AssertSingleClearedPlaintextBuffer(observedBuffers);
        }
        finally
        {
            ClearDecryptionTestPayload(testData, observedBuffers);
        }
    }

    [Fact]
    public async Task DecryptAsync_ClearsPlaintextChunkBufferWhenDestinationWriteIsCancelled()
    {
        var testData = await CreateEncryptedTestPayloadAsync();
        var observedBuffers = new List<byte[]>();

        using var cancellationTokenSource =
            new CancellationTokenSource();

        try
        {
            var service = CreateDecryptionService(
                testData.Key,
                testData.KeyId,
                observedBuffers);

            await using var source =
                new MemoryStream(testData.EncryptedBytes);

            await using var destination =
                new CancellingDocumentWriteStream(
                    cancellationTokenSource);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => service.DecryptAsync(
                    source,
                    destination,
                    cancellationTokenSource.Token));

            AssertSingleClearedPlaintextBuffer(observedBuffers);
        }
        finally
        {
            ClearDecryptionTestPayload(testData, observedBuffers);
        }
    }

    [Fact]
    public async Task DecryptAsync_ClearsEachPlaintextChunkBufferWhenSecondDestinationWriteFails()
    {
        var testData = await CreateEncryptedTestPayloadAsync(
            plaintextLength: (2 * 1024 * 1024) + 37);

        var observedBuffers = new List<byte[]>();

        try
        {
            var service = CreateDecryptionService(
                testData.Key,
                testData.KeyId,
                observedBuffers);

            await using var source =
                new MemoryStream(testData.EncryptedBytes);

            await using var destination =
                new FailingDocumentWriteStream(
                    successfulWritesBeforeFailure: 1);

            await Assert.ThrowsAsync<IOException>(
                () => service.DecryptAsync(source, destination, TestContext.Current.CancellationToken));

            // The first plaintext chunk was consumed successfully.
            // The second chunk's destination write fails.
            // Both operation-owned plaintext buffers must be cleared.
            AssertClearedPlaintextBuffers(
                observedBuffers,
                expectedCount: 2);
        }
        finally
        {
            ClearDecryptionTestPayload(
                testData,
                observedBuffers);
        }
    }

    [Fact]
    public async Task DecryptAsync_ClearsEachPlaintextChunkBufferAfterSuccessfulMultiChunkWrite()
    {
        var testData = await CreateEncryptedTestPayloadAsync(
            plaintextLength: (2 * 1024 * 1024) + 37);

        var observedBuffers = new List<byte[]>();

        try
        {
            var service = CreateDecryptionService(
                testData.Key,
                testData.KeyId,
                observedBuffers);

            await using var source =
                new MemoryStream(testData.EncryptedBytes);

            await using var destination = new MemoryStream();

            await service.DecryptAsync(source, destination, TestContext.Current.CancellationToken);

            Assert.Equal(
                testData.Plaintext,
                destination.ToArray());

            Assert.True(
                source.CanRead,
                "Decryption must not dispose the caller-owned source stream.");

            Assert.True(
                destination.CanWrite,
                "Decryption must not dispose the caller-owned destination stream.");

            AssertClearedPlaintextBuffers(
                observedBuffers,
                expectedCount: 3);
        }
        finally
        {
            ClearDecryptionTestPayload(
                testData,
                observedBuffers);
        }
    }

    [Fact]
    public async Task EncryptAsync_ClearsReturnedKeyBufferAfterSuccessfulEncryption()
    {
        byte[] key = RandomNumberGenerator.GetBytes(
            EncryptionKeyMaterial.KeySizeBytes);

        Guid keyId = Guid.NewGuid();

        var keyService = new TrackingEncryptionKeyService(
            key,
            keyId);

        byte[] expectedBackingKey = [.. keyService.BackingKey];

        try
        {
            var service = new DocumentEncryptionService(
                keyService,
                NullLogger<DocumentEncryptionService>.Instance);

            byte[] plaintext = Encoding.UTF8.GetBytes(
                "Verify operation-owned encryption key cleanup.");

            await using var source = new MemoryStream(plaintext);
            await using var encrypted = new MemoryStream();

            await service.EncryptLegacyVersion2Async(source, encrypted, TestContext.Current.CancellationToken);

            byte[] returnedKeyBuffer =
                Assert.Single(keyService.ReturnedKeyBuffers);

            Assert.True(
                returnedKeyBuffer.All(value => value == 0),
                "The returned key buffer was not cleared.");

            Assert.True(
                expectedBackingKey.SequenceEqual(keyService.BackingKey),
                "The key-service backing buffer was modified.");
        }
        finally
        {
            foreach (byte[] buffer in keyService.ReturnedKeyBuffers)
            {
                CryptographicOperations.ZeroMemory(buffer);
            }

            CryptographicOperations.ZeroMemory(keyService.BackingKey);
            CryptographicOperations.ZeroMemory(expectedBackingKey);
            CryptographicOperations.ZeroMemory(key);
        }
    }

    [Fact]
    public async Task DecryptAsync_ClearsReturnedKeyBufferWhenDestinationWriteFails()
    {
        byte[] key = RandomNumberGenerator.GetBytes(
            EncryptionKeyMaterial.KeySizeBytes);

        Guid keyId = Guid.NewGuid();

        var decryptionKeyService = new TrackingEncryptionKeyService(
            key,
            keyId);

        byte[] expectedBackingKey =
            [.. decryptionKeyService.BackingKey];

        byte[] plaintext = Encoding.UTF8.GetBytes(
            "Verify cleanup when the decryption destination fails.");

        byte[]? encryptedBytes = null;

        try
        {
            var encryptionService = new DocumentEncryptionService(
                new TestEncryptionKeyService(key, keyId),
                NullLogger<DocumentEncryptionService>.Instance);

            await using (var source = new MemoryStream(plaintext))
            await using (var encrypted = new MemoryStream())
            {
                await encryptionService.EncryptLegacyVersion2Async(source, encrypted, TestContext.Current.CancellationToken);

                encryptedBytes = encrypted.ToArray();
            }

            var decryptionService = new DocumentEncryptionService(
                decryptionKeyService,
                NullLogger<DocumentEncryptionService>.Instance);

            await using var sourceForDecryption =
                new MemoryStream(encryptedBytes);

            await using var destination =
                new FailingDocumentWriteStream(
                    successfulWritesBeforeFailure: 0);

            await Assert.ThrowsAsync<IOException>(
                () => decryptionService.DecryptAsync(sourceForDecryption, destination, TestContext.Current.CancellationToken));

            byte[] returnedKeyBuffer =
                Assert.Single(decryptionKeyService.ReturnedKeyBuffers);

            Assert.True(
                returnedKeyBuffer.All(value => value == 0),
                "The returned key buffer was not cleared after a destination-write failure.");

            Assert.True(
                expectedBackingKey.SequenceEqual(
                    decryptionKeyService.BackingKey),
                "The key-service backing buffer was modified.");
        }
        finally
        {
            foreach (byte[] buffer in decryptionKeyService.ReturnedKeyBuffers)
            {
                CryptographicOperations.ZeroMemory(buffer);
            }

            CryptographicOperations.ZeroMemory(decryptionKeyService.BackingKey);
            CryptographicOperations.ZeroMemory(expectedBackingKey);
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(plaintext);

            if (encryptedBytes is not null)
            {
                CryptographicOperations.ZeroMemory(encryptedBytes);
            }
        }
    }

    [Fact]
    public async Task DecryptAsync_ClearsReturnedKeyBufferWhenEncryptedHeaderIsInvalid()
    {
        byte[] key = RandomNumberGenerator.GetBytes(
            EncryptionKeyMaterial.KeySizeBytes);

        var keyService = new TrackingEncryptionKeyService(
            key,
            Guid.NewGuid());

        byte[] expectedBackingKey = [.. keyService.BackingKey];

        byte[] malformedHeader = new byte[8];

        BinaryPrimitives.WriteUInt32LittleEndian(
            malformedHeader.AsSpan(0, sizeof(uint)),
            0x44564C54);

        BinaryPrimitives.WriteUInt32LittleEndian(
            malformedHeader.AsSpan(sizeof(uint), sizeof(uint)),
            99);

        try
        {
            var service = new DocumentEncryptionService(
                keyService,
                NullLogger<DocumentEncryptionService>.Instance);

            await using var source = new MemoryStream(malformedHeader);
            await using var destination = new MemoryStream();

            await Assert.ThrowsAnyAsync<CryptographicException>(
                () => service.DecryptAsync(source, destination, TestContext.Current.CancellationToken));

            byte[] returnedKeyBuffer =
                Assert.Single(keyService.ReturnedKeyBuffers);

            Assert.True(
                returnedKeyBuffer.All(value => value == 0),
                "The returned key buffer was not cleared after header validation failed.");

            Assert.True(
                expectedBackingKey.SequenceEqual(keyService.BackingKey),
                "The key-service backing buffer was modified.");
        }
        finally
        {
            foreach (byte[] buffer in keyService.ReturnedKeyBuffers)
            {
                CryptographicOperations.ZeroMemory(buffer);
            }

            CryptographicOperations.ZeroMemory(keyService.BackingKey);
            CryptographicOperations.ZeroMemory(expectedBackingKey);
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(malformedHeader);
        }
    }

    [Fact]
    public async Task DecryptAsync_ClearsReturnedKeyBufferAfterSuccessfulDecryption()
    {
        byte[] key = RandomNumberGenerator.GetBytes(
            EncryptionKeyMaterial.KeySizeBytes);

        Guid keyId = Guid.NewGuid();

        var encryptionKeyService = new TrackingEncryptionKeyService(
            key,
            keyId);

        var decryptionKeyService = new TrackingEncryptionKeyService(
            key,
            keyId);

        byte[] expectedBackingKey =
            [.. decryptionKeyService.BackingKey];

        try
        {
            var encryptionService = new DocumentEncryptionService(
                encryptionKeyService,
                NullLogger<DocumentEncryptionService>.Instance);

            byte[] plaintext = Encoding.UTF8.GetBytes(
                "Verify operation-owned decryption key cleanup.");

            await using var source = new MemoryStream(plaintext);
            await using var encrypted = new MemoryStream();

            await encryptionService.EncryptLegacyVersion2Async(source, encrypted, TestContext.Current.CancellationToken);

            // The encryption setup is not the subject of this test.
            // Clear its returned buffer before testing decryption.
            foreach (byte[] buffer in encryptionKeyService.ReturnedKeyBuffers)
            {
                CryptographicOperations.ZeroMemory(buffer);
            }

            encrypted.Position = 0;

            var decryptionService = new DocumentEncryptionService(
                decryptionKeyService,
                NullLogger<DocumentEncryptionService>.Instance);

            await using var decrypted = new MemoryStream();

            await decryptionService.DecryptAsync(encrypted, decrypted, TestContext.Current.CancellationToken);

            Assert.Equal(plaintext, decrypted.ToArray());

            byte[] returnedKeyBuffer =
                Assert.Single(decryptionKeyService.ReturnedKeyBuffers);

            Assert.True(
                returnedKeyBuffer.All(value => value == 0),
                "The returned key buffer was not cleared.");

            Assert.True(
                expectedBackingKey.SequenceEqual(
                    decryptionKeyService.BackingKey),
                "The key-service backing buffer was modified.");
        }
        finally
        {
            foreach (byte[] buffer in encryptionKeyService.ReturnedKeyBuffers)
            {
                CryptographicOperations.ZeroMemory(buffer);
            }

            foreach (byte[] buffer in decryptionKeyService.ReturnedKeyBuffers)
            {
                CryptographicOperations.ZeroMemory(buffer);
            }

            CryptographicOperations.ZeroMemory(encryptionKeyService.BackingKey);
            CryptographicOperations.ZeroMemory(decryptionKeyService.BackingKey);
            CryptographicOperations.ZeroMemory(expectedBackingKey);
            CryptographicOperations.ZeroMemory(key);
        }
    }

    [Fact]
    public async Task DecryptAsync_ClearsReturnedKeyBufferWhenSourceReadFails()
    {
        byte[] key = RandomNumberGenerator.GetBytes(
            EncryptionKeyMaterial.KeySizeBytes);

        var keyService = new TrackingEncryptionKeyService(
            key,
            Guid.NewGuid());

        byte[] expectedBackingKey = [.. keyService.BackingKey];

        try
        {
            var service = new DocumentEncryptionService(
                keyService,
                NullLogger<DocumentEncryptionService>.Instance);

            await using var source = new FailingDocumentReadStream();
            await using var destination = new MemoryStream();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.DecryptAsync(source, destination, TestContext.Current.CancellationToken));

            byte[] returnedKeyBuffer =
                Assert.Single(keyService.ReturnedKeyBuffers);

            Assert.True(
                returnedKeyBuffer.All(value => value == 0),
                "The returned key buffer was not cleared after a read failure.");

            Assert.True(
                expectedBackingKey.SequenceEqual(keyService.BackingKey),
                "The key-service backing buffer was modified.");
        }
        finally
        {
            foreach (byte[] buffer in keyService.ReturnedKeyBuffers)
            {
                CryptographicOperations.ZeroMemory(buffer);
            }

            CryptographicOperations.ZeroMemory(keyService.BackingKey);
            CryptographicOperations.ZeroMemory(expectedBackingKey);
            CryptographicOperations.ZeroMemory(key);
        }
    }

    [Fact]
    public async Task DecryptAsync_ClearsReturnedKeyBufferWhenAuthenticationFails()
    {
        byte[] key = RandomNumberGenerator.GetBytes(
            EncryptionKeyMaterial.KeySizeBytes);

        Guid keyId = Guid.NewGuid();

        var encryptionKeyService = new TrackingEncryptionKeyService(
            key,
            keyId);

        var decryptionKeyService = new TrackingEncryptionKeyService(
            key,
            keyId);

        byte[] expectedBackingKey =
            [.. decryptionKeyService.BackingKey];

        try
        {
            var encryptionService = new DocumentEncryptionService(
                encryptionKeyService,
                NullLogger<DocumentEncryptionService>.Instance);

            byte[] plaintext = Encoding.UTF8.GetBytes(
                "Verify key cleanup after authentication failure.");

            byte[] encryptedBytes;

            try
            {
                await using var source = new MemoryStream(plaintext);
                await using var encrypted = new MemoryStream();

                await encryptionService.EncryptLegacyVersion2Async(source, encrypted, TestContext.Current.CancellationToken);

                encryptedBytes = encrypted.ToArray();
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plaintext);

                foreach (byte[] buffer in encryptionKeyService.ReturnedKeyBuffers)
                {
                    CryptographicOperations.ZeroMemory(buffer);
                }
            }

            // Corrupt the encrypted payload to force GCM authentication failure.
            encryptedBytes[^1] ^= 0x01;

            try
            {
                var decryptionService = new DocumentEncryptionService(
                    decryptionKeyService,
                    NullLogger<DocumentEncryptionService>.Instance);

                await using var source = new MemoryStream(encryptedBytes);
                await using var destination = new MemoryStream();

                await Assert.ThrowsAnyAsync<CryptographicException>(
                    () => decryptionService.DecryptAsync(source, destination, TestContext.Current.CancellationToken));

                byte[] returnedKeyBuffer =
                    Assert.Single(decryptionKeyService.ReturnedKeyBuffers);

                Assert.True(
                    returnedKeyBuffer.All(value => value == 0),
                    "The returned key buffer was not cleared after authentication failure.");

                Assert.True(
                    expectedBackingKey.SequenceEqual(
                        decryptionKeyService.BackingKey),
                    "The key-service backing buffer was modified.");
            }
            finally
            {
                CryptographicOperations.ZeroMemory(encryptedBytes);
            }
        }
        finally
        {
            foreach (byte[] buffer in encryptionKeyService.ReturnedKeyBuffers)
            {
                CryptographicOperations.ZeroMemory(buffer);
            }

            foreach (byte[] buffer in decryptionKeyService.ReturnedKeyBuffers)
            {
                CryptographicOperations.ZeroMemory(buffer);
            }

            CryptographicOperations.ZeroMemory(encryptionKeyService.BackingKey);
            CryptographicOperations.ZeroMemory(decryptionKeyService.BackingKey);
            CryptographicOperations.ZeroMemory(expectedBackingKey);
            CryptographicOperations.ZeroMemory(key);
        }
    }

    [Fact]
    public async Task DecryptAsync_ClearsReturnedKeyBufferWhenCancellationOccursAfterKeyAcquisition()
    {
        byte[] key = RandomNumberGenerator.GetBytes(
            EncryptionKeyMaterial.KeySizeBytes);

        Guid keyId = Guid.NewGuid();

        var encryptionKeyService = new TrackingEncryptionKeyService(
            key,
            keyId);

        var decryptionKeyService = new TrackingEncryptionKeyService(
            key,
            keyId);

        byte[] expectedBackingKey =
            [.. decryptionKeyService.BackingKey];

        byte[] plaintext = new byte[2 * 1024 * 1024];
        Array.Fill(plaintext, (byte)'A');

        try
        {
            var encryptionService = new DocumentEncryptionService(
                encryptionKeyService,
                NullLogger<DocumentEncryptionService>.Instance);

            await using var source = new MemoryStream(plaintext);
            await using var encrypted = new MemoryStream();

            await encryptionService.EncryptLegacyVersion2Async(source, encrypted, TestContext.Current.CancellationToken);

            foreach (byte[] buffer in encryptionKeyService.ReturnedKeyBuffers)
            {
                CryptographicOperations.ZeroMemory(buffer);
            }

            encrypted.Position = 0;

            using var cancellationTokenSource =
                new CancellationTokenSource();

            var decryptionService = new DocumentEncryptionService(
                decryptionKeyService,
                NullLogger<DocumentEncryptionService>.Instance);

            await using var destination = new MemoryStream();

            await using var cancellingSource = new CancellingReadStream(
                encrypted,
                cancellationTokenSource);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => decryptionService.DecryptAsync(
                    cancellingSource,
                    destination,
                    cancellationTokenSource.Token,
                    maximumPlaintextBytes: plaintext.Length));

            byte[] returnedKeyBuffer =
                Assert.Single(decryptionKeyService.ReturnedKeyBuffers);

            Assert.True(
                returnedKeyBuffer.All(value => value == 0),
                "The returned key buffer was not cleared after cancellation.");

            Assert.True(
                expectedBackingKey.SequenceEqual(
                    decryptionKeyService.BackingKey),
                "The key-service backing buffer was modified.");
        }
        finally
        {
            foreach (byte[] buffer in encryptionKeyService.ReturnedKeyBuffers)
            {
                CryptographicOperations.ZeroMemory(buffer);
            }

            foreach (byte[] buffer in decryptionKeyService.ReturnedKeyBuffers)
            {
                CryptographicOperations.ZeroMemory(buffer);
            }

            CryptographicOperations.ZeroMemory(encryptionKeyService.BackingKey);
            CryptographicOperations.ZeroMemory(decryptionKeyService.BackingKey);
            CryptographicOperations.ZeroMemory(expectedBackingKey);
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    [Fact]
    public async Task EncryptAsync_ClearsReturnedKeyBufferWhenSourceReadFails()
    {
        byte[] key = RandomNumberGenerator.GetBytes(
            EncryptionKeyMaterial.KeySizeBytes);

        var keyService = new TrackingEncryptionKeyService(
            key,
            Guid.NewGuid());

        byte[] expectedBackingKey = [.. keyService.BackingKey];

        try
        {
            var service = new DocumentEncryptionService(
                keyService,
                NullLogger<DocumentEncryptionService>.Instance);

            await using var source = new FailingDocumentReadStream();
            await using var destination = new MemoryStream();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.EncryptLegacyVersion2Async(source, destination, TestContext.Current.CancellationToken));

            byte[] returnedKeyBuffer =
                Assert.Single(keyService.ReturnedKeyBuffers);

            Assert.True(
                returnedKeyBuffer.All(value => value == 0),
                "The returned key buffer was not cleared after a read failure.");

            Assert.True(
                expectedBackingKey.SequenceEqual(keyService.BackingKey),
                "The key-service backing buffer was modified.");
        }
        finally
        {
            foreach (byte[] buffer in keyService.ReturnedKeyBuffers)
            {
                CryptographicOperations.ZeroMemory(buffer);
            }

            CryptographicOperations.ZeroMemory(keyService.BackingKey);
            CryptographicOperations.ZeroMemory(expectedBackingKey);
            CryptographicOperations.ZeroMemory(key);
        }
    }

    [Fact]
    public async Task EncryptAsync_ClearsReturnedKeyBufferWhenDestinationWriteFails()
    {
        byte[] key = RandomNumberGenerator.GetBytes(
            EncryptionKeyMaterial.KeySizeBytes);

        var keyService = new TrackingEncryptionKeyService(
            key,
            Guid.NewGuid());

        byte[] expectedBackingKey = [.. keyService.BackingKey];
        byte[] plaintext = Encoding.UTF8.GetBytes(
            "Verify cleanup when the encryption destination fails.");

        try
        {
            var service = new DocumentEncryptionService(
                keyService,
                NullLogger<DocumentEncryptionService>.Instance);

            await using var source = new MemoryStream(plaintext);
            await using var destination =
                new FailingDocumentWriteStream(
                    successfulWritesBeforeFailure: 1);

            await Assert.ThrowsAsync<IOException>(
                () => service.EncryptLegacyVersion2Async(source, destination, TestContext.Current.CancellationToken));

            byte[] returnedKeyBuffer =
                Assert.Single(keyService.ReturnedKeyBuffers);

            Assert.True(
                returnedKeyBuffer.All(value => value == 0),
                "The returned key buffer was not cleared after a destination-write failure.");

            Assert.True(
                expectedBackingKey.SequenceEqual(keyService.BackingKey),
                "The key-service backing buffer was modified.");
        }
        finally
        {
            foreach (byte[] buffer in keyService.ReturnedKeyBuffers)
            {
                CryptographicOperations.ZeroMemory(buffer);
            }

            CryptographicOperations.ZeroMemory(keyService.BackingKey);
            CryptographicOperations.ZeroMemory(expectedBackingKey);
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    [Fact]
    public async Task EncryptAsync_ClearsReturnedKeyBufferWhenCancellationOccursAfterKeyAcquisition()
    {
        byte[] key = RandomNumberGenerator.GetBytes(
            EncryptionKeyMaterial.KeySizeBytes);

        var keyService = new TrackingEncryptionKeyService(
            key,
            Guid.NewGuid());

        byte[] expectedBackingKey = [.. keyService.BackingKey];
        byte[] plaintext = new byte[3 * 1024 * 1024];

        Array.Fill(plaintext, (byte)'A');

        using var cancellationTokenSource =
            new CancellationTokenSource();

        try
        {
            var service = new DocumentEncryptionService(
                keyService,
                NullLogger<DocumentEncryptionService>.Instance);

            await using var innerSource = new MemoryStream(plaintext);

            await using var source = new CancellingReadStream(
                innerSource,
                cancellationTokenSource);

            await using var destination = new MemoryStream();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => service.EncryptAsync(
                    source,
                    destination,
                    Guid.NewGuid(),
                    cancellationTokenSource.Token));

            byte[] returnedKeyBuffer =
                Assert.Single(keyService.ReturnedKeyBuffers);

            Assert.True(
                returnedKeyBuffer.All(value => value == 0),
                "The returned key buffer was not cleared after cancellation.");

            Assert.True(
                expectedBackingKey.SequenceEqual(keyService.BackingKey),
                "The key-service backing buffer was modified.");
        }
        finally
        {
            foreach (byte[] buffer in keyService.ReturnedKeyBuffers)
            {
                CryptographicOperations.ZeroMemory(buffer);
            }

            CryptographicOperations.ZeroMemory(keyService.BackingKey);
            CryptographicOperations.ZeroMemory(expectedBackingKey);
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(plaintext);
        }
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

            await encryptionService.EncryptLegacyVersion2Async(source, encrypted, TestContext.Current.CancellationToken);

            encrypted.Position = 0;

            var decryptionService =
                new DocumentEncryptionService(
                    new ExistingKeyOnlyTestEncryptionKeyService(key, keyId),
                    NullLogger<DocumentEncryptionService>.Instance);

            await using var decrypted =
                new MemoryStream();

            await decryptionService.DecryptAsync(encrypted, decrypted, TestContext.Current.CancellationToken);

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

            await service.DecryptAsync(source, destination, TestContext.Current.CancellationToken);

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

            await encryptionService.EncryptLegacyVersion2Async(source, encrypted, TestContext.Current.CancellationToken);

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
                () => decryptionService.DecryptAsync(encrypted, destination, TestContext.Current.CancellationToken));
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

            await encryptionService.EncryptLegacyVersion2Async(source, encrypted, TestContext.Current.CancellationToken);

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
                () => decryptionService.DecryptAsync(encrypted, destination, TestContext.Current.CancellationToken));
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

        await service.EncryptLegacyVersion2Async(source, encrypted, TestContext.Current.CancellationToken);

        encrypted.Position = 0;

        await using var decrypted =
            new MemoryStream();

        await service.DecryptAsync(encrypted, decrypted, TestContext.Current.CancellationToken);

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

        await service.EncryptLegacyVersion2Async(source, encrypted, TestContext.Current.CancellationToken);

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
                service.DecryptAsync(source, destination, TestContext.Current.CancellationToken));
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
                service.DecryptAsync(source, destination, TestContext.Current.CancellationToken));
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

        await service.EncryptLegacyVersion2Async(source, encrypted, TestContext.Current.CancellationToken);

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
                service.DecryptAsync(tamperedSource, destination, TestContext.Current.CancellationToken));
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

        await service.EncryptLegacyVersion2Async(source, encrypted, TestContext.Current.CancellationToken);

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
                service.DecryptAsync(truncatedSource, destination, TestContext.Current.CancellationToken));
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

        await service.EncryptLegacyVersion2Async(source, encrypted, TestContext.Current.CancellationToken);

        encrypted.Position = 0;

        await using var destination =
            new MemoryStream();

        long maximumPlaintextBytes =
            originalContent.Length - 1;

        ResourceLimitExceededException exception =
            await Assert.ThrowsAsync<ResourceLimitExceededException>(
                () =>
                    service.DecryptAsync(encrypted, destination, maximumPlaintextBytes: maximumPlaintextBytes, cancellationToken: TestContext.Current.CancellationToken));

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

        await service.EncryptLegacyVersion2Async(source, encrypted, TestContext.Current.CancellationToken);

        encrypted.Position = 0;

        await using var destination =
            new MemoryStream();

        long maximumPlaintextBytes =
            1024 * 1024;

        ResourceLimitExceededException exception =
            await Assert.ThrowsAsync<ResourceLimitExceededException>(
                () =>
                    service.DecryptAsync(encrypted, destination, maximumPlaintextBytes: maximumPlaintextBytes, cancellationToken: TestContext.Current.CancellationToken));

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

        await service.EncryptLegacyVersion2Async(source, encrypted, TestContext.Current.CancellationToken);

        encrypted.Position = 0;

        await using var destination =
            new MemoryStream();

        await service.DecryptAsync(encrypted, destination, maximumPlaintextBytes: originalContent.Length, cancellationToken: TestContext.Current.CancellationToken);

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
                    service.DecryptAsync(source, destination, maximumPlaintextBytes: maximumPlaintextBytes, cancellationToken: TestContext.Current.CancellationToken));

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

        await service.EncryptLegacyVersion2Async(source, encrypted, TestContext.Current.CancellationToken);

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

        await service.EncryptLegacyVersion2Async(source, encrypted, TestContext.Current.CancellationToken);

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

        await service.EncryptAsync(source, encrypted, documentId, TestContext.Current.CancellationToken);

        byte[] encryptedBytes = encrypted.ToArray();

        Assert.Equal(
            3u,
            BinaryPrimitives.ReadUInt32LittleEndian(
                encryptedBytes.AsSpan(4, sizeof(uint))));

        encrypted.Position = 0;

        await using var decrypted =
            new MemoryStream();

        await service.DecryptAsync(encrypted, decrypted, documentId, TestContext.Current.CancellationToken);

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

        await service.EncryptAsync(source, encrypted, documentId, TestContext.Current.CancellationToken);

        byte[] encryptedBytes = encrypted.ToArray();

        Assert.Equal(
            3u,
            BinaryPrimitives.ReadUInt32LittleEndian(
                encryptedBytes.AsSpan(4, sizeof(uint))));

        encrypted.Position = 0;

        await using var decrypted = new MemoryStream();

        await service.DecryptAsync(encrypted, decrypted, documentId, TestContext.Current.CancellationToken);

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

        await service.EncryptAsync(source, encrypted, documentId, TestContext.Current.CancellationToken);

        byte[] recordWithTrailingData = encrypted.ToArray();

        Array.Resize(
            ref recordWithTrailingData,
            recordWithTrailingData.Length + 1);

        recordWithTrailingData[^1] = 0xA5;

        await using var tamperedSource =
            new MemoryStream(recordWithTrailingData);

        await using var destination = new MemoryStream();

        await Assert.ThrowsAsync<CryptographicException>(
            () => service.DecryptAsync(tamperedSource, destination, documentId, TestContext.Current.CancellationToken));
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

        await service.EncryptAsync(firstSource, firstEncrypted, firstDocumentId, TestContext.Current.CancellationToken);

        await service.EncryptAsync(secondSource, secondEncrypted, secondDocumentId, TestContext.Current.CancellationToken);

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
            () => service.DecryptAsync(substitutedSource, destination, firstDocumentId, TestContext.Current.CancellationToken));
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
        await service.EncryptLegacyVersion2Async(source, encrypted, TestContext.Current.CancellationToken);

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
                () => service.DecryptAsync(truncatedSource, destination, TestContext.Current.CancellationToken));
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

        await service.EncryptAsync(source, encrypted, documentId, TestContext.Current.CancellationToken);

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

        await service.DecryptAsync(encrypted, decrypted, documentId, TestContext.Current.CancellationToken);

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

        await service.EncryptAsync(source, encrypted, encryptionDocumentId, TestContext.Current.CancellationToken);

        encrypted.Position = 0;

        await using var destination =
            new MemoryStream();

        await Assert.ThrowsAsync<AuthenticationTagMismatchException>(
            () => service.DecryptAsync(encrypted, destination, decryptionDocumentId, TestContext.Current.CancellationToken));
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

        await service.EncryptAsync(source, encrypted, documentId, TestContext.Current.CancellationToken);

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
                () => service.DecryptAsync(truncatedSource, destination, documentId, TestContext.Current.CancellationToken));
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

        await service.EncryptAsync(source, encrypted, documentId, TestContext.Current.CancellationToken);

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
            () => service.DecryptAsync(truncatedSource, destination, documentId, TestContext.Current.CancellationToken));
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

        await service.EncryptAsync(source, encrypted, documentId, TestContext.Current.CancellationToken);

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
            () => service.DecryptAsync(reorderedSource, destination, documentId, TestContext.Current.CancellationToken));
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
            () => service.DecryptAsync(source, destination, TestContext.Current.CancellationToken));
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
            () => service.DecryptAsync(source, destination, documentId, TestContext.Current.CancellationToken));
    }

    private sealed class ObservingPlaintextReadStream : Stream
    {
        private readonly byte[] _content;
        private readonly PlaintextReadTermination _termination;
        private readonly CancellationTokenSource _cancellationTokenSource;

        private int _position;
        private bool _hasObservedSubsequentRead;
        private bool _bufferWasClearedBeforeNextRead = true;

        public byte[]? ObservedBuffer { get; private set; }

        public bool BufferWasClearedBeforeNextRead =>
            _hasObservedSubsequentRead &&
            _bufferWasClearedBeforeNextRead;

        public ObservingPlaintextReadStream(
            byte[] content,
            PlaintextReadTermination termination,
            CancellationTokenSource cancellationTokenSource)
        {
            _content = content;
            _termination = termination;
            _cancellationTokenSource = cancellationTokenSource;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;

        public override long Length =>
            throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (!MemoryMarshal.TryGetArray(
                    (ReadOnlyMemory<byte>)buffer,
                    out ArraySegment<byte> segment) ||
                segment.Array is null)
            {
                throw new InvalidOperationException(
                    "The plaintext read buffer was not array-backed.");
            }

            ObservedBuffer ??= segment.Array;

            if (!ReferenceEquals(
                    ObservedBuffer,
                    segment.Array))
            {
                throw new InvalidOperationException(
                    "The encryption operation changed its reusable input buffer.");
            }

            if (_position > 0)
            {
                bool wasCleared =
                    ObservedBuffer.All(value => value == 0);

                _bufferWasClearedBeforeNextRead =
                    _hasObservedSubsequentRead
                        ? _bufferWasClearedBeforeNextRead && wasCleared
                        : wasCleared;

                _hasObservedSubsequentRead = true;
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (_position < _content.Length)
            {
                int bytesToCopy = Math.Min(
                    buffer.Length,
                    _content.Length - _position);

                _content.AsMemory(
                    _position,
                    bytesToCopy).CopyTo(buffer);

                _position += bytesToCopy;

                return ValueTask.FromResult(bytesToCopy);
            }

            switch (_termination)
            {
                case PlaintextReadTermination.EndOfStream:
                    return ValueTask.FromResult(0);

                case PlaintextReadTermination.ThrowAfterContent:
                    throw new IOException(
                        "Simulated source-read failure after returning plaintext.");

                case PlaintextReadTermination.CancelAfterContent:
                    _cancellationTokenSource.Cancel();
                    cancellationToken.ThrowIfCancellationRequested();

                    throw new InvalidOperationException(
                        "Expected cancellation to interrupt the read.");

                default:
                    throw new InvalidOperationException(
                        "Unsupported plaintext-read termination mode.");
            }
        }

        public override int Read(
            byte[] buffer,
            int offset,
            int count) =>
            throw new NotSupportedException();

        public override long Seek(
            long offset,
            SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) =>
            throw new NotSupportedException();

        public override void Write(
            byte[] buffer,
            int offset,
            int count) =>
            throw new NotSupportedException();
    }

    private sealed class FailingDocumentWriteStream : Stream
    {
        private readonly int _successfulWritesBeforeFailure;
        private int _successfulWrites;

        public FailingDocumentWriteStream(
            int successfulWritesBeforeFailure)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(
                successfulWritesBeforeFailure);

            _successfulWritesBeforeFailure =
                successfulWritesBeforeFailure;
        }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;

        public override long Length =>
            throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(
            byte[] buffer,
            int offset,
            int count) =>
            throw new NotSupportedException();

        public override long Seek(
            long offset,
            SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) =>
            throw new NotSupportedException();

        public override void Write(
            byte[] buffer,
            int offset,
            int count)
        {
            RecordWrite();
        }

        public override Task WriteAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RecordWrite();

            return Task.CompletedTask;
        }

        public override ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RecordWrite();

            return ValueTask.CompletedTask;
        }

        private void RecordWrite()
        {
            if (_successfulWrites >= _successfulWritesBeforeFailure)
            {
                throw new IOException(
                    "Simulated document destination-write failure.");
            }

            _successfulWrites++;
        }
    }

    private sealed class FailingAfterByteLimitReadStream : Stream
    {
        private readonly byte[] _content;
        private readonly int _byteLimit;
        private int _position;

        public FailingAfterByteLimitReadStream(
            byte[] content,
            int byteLimit)
        {
            ArgumentNullException.ThrowIfNull(content);

            if (byteLimit < 0 || byteLimit > content.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(byteLimit));
            }

            _content = content;
            _byteLimit = byteLimit;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;

        public override long Length =>
            throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (buffer.IsEmpty)
            {
                return ValueTask.FromResult(0);
            }

            if (_position >= _byteLimit)
            {
                throw new IOException(
                    "Simulated encrypted-source read failure.");
            }

            int bytesToCopy = Math.Min(
                buffer.Length,
                _byteLimit - _position);

            _content.AsMemory(
                _position,
                bytesToCopy).CopyTo(buffer);

            _position += bytesToCopy;

            return ValueTask.FromResult(bytesToCopy);
        }

        public override int Read(
            byte[] buffer,
            int offset,
            int count) =>
            throw new NotSupportedException();

        public override long Seek(
            long offset,
            SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) =>
            throw new NotSupportedException();

        public override void Write(
            byte[] buffer,
            int offset,
            int count) =>
            throw new NotSupportedException();
    }

    private sealed class CancellingDocumentWriteStream : Stream
    {
        private readonly CancellationTokenSource _cancellationTokenSource;

        public CancellingDocumentWriteStream(
            CancellationTokenSource cancellationTokenSource)
        {
            _cancellationTokenSource = cancellationTokenSource;
        }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;

        public override long Length =>
            throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(
            byte[] buffer,
            int offset,
            int count) =>
            throw new NotSupportedException();

        public override long Seek(
            long offset,
            SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) =>
            throw new NotSupportedException();

        public override void Write(
            byte[] buffer,
            int offset,
            int count) =>
            throw new NotSupportedException();

        public override ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            _cancellationTokenSource.Cancel();
            cancellationToken.ThrowIfCancellationRequested();

            return ValueTask.CompletedTask;
        }
    }

    private sealed class FailingDocumentReadStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;

        public override long Length =>
            throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(
            byte[] buffer,
            int offset,
            int count)
        {
            throw new InvalidOperationException(
                "Simulated document read failure.");
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(
                "Simulated document read failure.");
        }

        public override long Seek(
            long offset,
            SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) =>
            throw new NotSupportedException();

        public override void Write(
            byte[] buffer,
            int offset,
            int count) =>
            throw new NotSupportedException();
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

    private sealed class TrackingEncryptionKeyService
        : IEncryptionKeyService
    {
        private readonly byte[] _backingKey;
        private readonly Guid _keyId;

        public TrackingEncryptionKeyService(
            byte[] key,
            Guid keyId)
        {
            _backingKey = [.. key];
            _keyId = keyId;
        }

        public byte[] BackingKey => _backingKey;

        public List<byte[]> ReturnedKeyBuffers { get; } = [];

        public Task<EncryptionKeyMaterial> GetKeyAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            byte[] returnedKeyBuffer = [.. _backingKey];

            ReturnedKeyBuffers.Add(returnedKeyBuffer);

            return Task.FromResult(
                new EncryptionKeyMaterial(
                    returnedKeyBuffer,
                    _keyId,
                    keyVersion: 1));
        }
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

    private static async Task AssertEncryptionInputBufferIsClearedAsync(
        PlaintextReadTermination termination,
        bool failDestinationWrite = false,
        int plaintextLength = 128)
    {
        byte[] key = RandomNumberGenerator.GetBytes(
            EncryptionKeyMaterial.KeySizeBytes);

        byte[] plaintext = new byte[plaintextLength];

        Array.Fill(
            plaintext,
            (byte)'P');

        using var cancellationTokenSource =
            new CancellationTokenSource();

        await using var source =
            new ObservingPlaintextReadStream(
                plaintext,
                termination,
                cancellationTokenSource);

        await using Stream destination =
            failDestinationWrite
                ? new FailingDocumentWriteStream(
                    successfulWritesBeforeFailure: 1)
                : new MemoryStream();

        try
        {
            var service = new DocumentEncryptionService(
                new TestEncryptionKeyService(key),
                NullLogger<DocumentEncryptionService>.Instance);

            if (failDestinationWrite)
            {
                await Assert.ThrowsAsync<IOException>(
                    () => service.EncryptLegacyVersion2Async(
                        source,
                        destination,
                        cancellationTokenSource.Token));
            }
            else
            {
                switch (termination)
                {
                    case PlaintextReadTermination.EndOfStream:
                        await service.EncryptLegacyVersion2Async(
                            source,
                            destination,
                            cancellationTokenSource.Token);
                        break;

                    case PlaintextReadTermination.ThrowAfterContent:
                        await Assert.ThrowsAsync<IOException>(
                            () => service.EncryptLegacyVersion2Async(
                                source,
                                destination,
                                cancellationTokenSource.Token));
                        break;

                    case PlaintextReadTermination.CancelAfterContent:
                        await Assert.ThrowsAnyAsync<OperationCanceledException>(
                            () => service.EncryptLegacyVersion2Async(
                                source,
                                destination,
                                cancellationTokenSource.Token));
                        break;

                    default:
                        throw new InvalidOperationException(
                            "Unsupported plaintext-read termination mode.");
                }
            }

            Assert.NotNull(source.ObservedBuffer);

            Assert.True(
                source.ObservedBuffer!.All(value => value == 0),
                "The plaintext input buffer was not cleared when encryption ended.");

            if (!failDestinationWrite)
            {
                Assert.True(
                    source.BufferWasClearedBeforeNextRead,
                    "The plaintext input buffer was not cleared before the next source read.");
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
            CryptographicOperations.ZeroMemory(key);

            if (source.ObservedBuffer is not null)
            {
                CryptographicOperations.ZeroMemory(
                    source.ObservedBuffer);
            }
        }
    }

    private enum PlaintextReadTermination
    {
        EndOfStream,
        ThrowAfterContent,
        CancelAfterContent
    }

    private static async Task<(
        byte[] Key,
        Guid KeyId,
        byte[] Plaintext,
        byte[] EncryptedBytes)> CreateEncryptedTestPayloadAsync(
            int plaintextLength = 0)
    {
        byte[] key = RandomNumberGenerator.GetBytes(
            EncryptionKeyMaterial.KeySizeBytes);

        Guid keyId = Guid.NewGuid();

        if (plaintextLength < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(plaintextLength));
        }

        byte[] plaintext;

        if (plaintextLength == 0)
        {
            plaintext = Encoding.UTF8.GetBytes(
                "Verify operation-owned decryption plaintext cleanup.");
        }
        else
        {
            plaintext = new byte[plaintextLength];

            RandomNumberGenerator.Fill(plaintext);
        }

        try
        {
            var service = new DocumentEncryptionService(
                new TestEncryptionKeyService(key, keyId),
                NullLogger<DocumentEncryptionService>.Instance);

            await using var source = new MemoryStream(plaintext);
            await using var encrypted = new MemoryStream();

            await service.EncryptLegacyVersion2Async(
                source,
                encrypted);

            return (
                key,
                keyId,
                plaintext,
                encrypted.ToArray());
        }
        catch
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(plaintext);
            throw;
        }
    }

    private static DocumentEncryptionService CreateDecryptionService(
        byte[] key,
        Guid keyId,
        List<byte[]> observedBuffers)
    {
        var service = new DocumentEncryptionService(
            new TestEncryptionKeyService(key, keyId),
            NullLogger<DocumentEncryptionService>.Instance);

        service.DecryptedPlaintextBufferFactoryForTesting = length =>
        {
            byte[] buffer = new byte[length];

            observedBuffers.Add(buffer);

            return buffer;
        };

        return service;
    }

    private static void AssertSingleClearedPlaintextBuffer(
        List<byte[]> observedBuffers)
    {
        byte[] buffer = Assert.Single(observedBuffers);

        Assert.True(
            buffer.All(value => value == 0),
            "The operation-owned decryption plaintext buffer was not cleared.");
    }

    private static void AssertClearedPlaintextBuffers(
        List<byte[]> observedBuffers,
        int expectedCount)
    {
        Assert.Equal(
            expectedCount,
            observedBuffers.Count);

        Assert.All(
            observedBuffers,
            buffer => Assert.True(
                buffer.All(value => value == 0),
                "An operation-owned decryption plaintext buffer was not cleared."));
    }

    private static void ClearDecryptionTestPayload(
        (
            byte[] Key,
            Guid KeyId,
            byte[] Plaintext,
            byte[] EncryptedBytes) testData,
        IEnumerable<byte[]> observedBuffers)
    {
        foreach (byte[] buffer in observedBuffers)
        {
            CryptographicOperations.ZeroMemory(buffer);
        }

        CryptographicOperations.ZeroMemory(testData.Key);
        CryptographicOperations.ZeroMemory(testData.Plaintext);
        CryptographicOperations.ZeroMemory(testData.EncryptedBytes);
    }
}
