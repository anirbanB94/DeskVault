using System.Buffers.Binary;
using System.Security.Cryptography;
using DeskVault.Infrastructure.Services;
using Xunit;

namespace DeskVault.Infrastructure.Tests;

public sealed class ProtectedEncryptionKeyPayloadTests
{
    private const int ExpectedPayloadLength = 58;
    private const int ExpectedFormatVersion = 1;
    private const int PurposeOffset = 5;
    private const int KeyIdOffset = 6;
    private const int KeyVersionOffset = 22;
    private const int KeyBytesOffset = 26;
    private const int GuidLength = 16;

    [Fact]
    public void Encode_ThenDecode_PreservesKeyAndLifecycleMetadata()
    {
        // Arrange
        byte[] keyBytes = CreateKeyBytes();
        byte[] protectedArtifact = CreateProtectedArtifact();
        byte[] payload = Array.Empty<byte>();
        byte[] decodedKeyBytes = Array.Empty<byte>();

        Guid keyId = Guid.Parse(
            "8d93d2a8-5b2e-4a31-98c7-3f03b73127d4");

        const int keyVersion = 1;

        try
        {
            // Act
            payload = ProtectedEncryptionKeyPayload.Encode(
                keyBytes,
                EncryptionKeyPurpose.Document,
                keyId,
                keyVersion);

            EncryptionKeyMaterial decoded =
                ProtectedEncryptionKeyPayload.Decode(
                    payload,
                    protectedArtifact,
                    EncryptionKeyPurpose.Document);

            decodedKeyBytes = decoded.KeyBytes;

            // Assert
            Assert.Equal(keyBytes, decodedKeyBytes);
            Assert.NotSame(keyBytes, decodedKeyBytes);
            Assert.Equal(keyId, decoded.KeyId);
            Assert.Equal(keyVersion, decoded.KeyVersion);
        }
        finally
        {
            Clear(keyBytes, protectedArtifact, payload, decodedKeyBytes);
        }
    }

    [Fact]
    public void Encode_WritesExpectedPayloadLayout()
    {
        // Arrange
        byte[] keyBytes = CreateKeyBytes();
        byte[] payload = Array.Empty<byte>();

        Guid keyId = Guid.Parse(
            "b348b2bc-bd40-41f5-a02c-3a2cdb3b718e");

        const int keyVersion = 3;

        try
        {
            // Act
            payload = ProtectedEncryptionKeyPayload.Encode(
                keyBytes,
                EncryptionKeyPurpose.Document,
                keyId,
                keyVersion);

            // Assert
            Assert.Equal(ExpectedPayloadLength, payload.Length);
            Assert.Equal(
                new byte[] { 0x44, 0x56, 0x4B, 0x50 },
                payload.AsSpan(0, 4).ToArray());

            Assert.Equal(ExpectedFormatVersion, payload[4]);

            Assert.Equal(
                keyId,
                new Guid(payload.AsSpan(KeyIdOffset, GuidLength)));

            Assert.Equal(
                keyVersion,
                BinaryPrimitives.ReadInt32LittleEndian(
                    payload.AsSpan(KeyVersionOffset, sizeof(int))));

            Assert.Equal(
                keyBytes,
                payload.AsSpan(KeyBytesOffset).ToArray());
        }
        finally
        {
            Clear(keyBytes, payload);
        }
    }

    [Fact]
    public void Decode_LegacyPayload_ReturnsVersionZeroAndStableIdentity()
    {
        // Arrange
        byte[] legacyPayload = CreateKeyBytes();
        byte[] protectedArtifact = CreateProtectedArtifact();

        byte[] firstKeyBytes = Array.Empty<byte>();
        byte[] secondKeyBytes = Array.Empty<byte>();

        try
        {
            // Act
            EncryptionKeyMaterial first =
                ProtectedEncryptionKeyPayload.Decode(
                    legacyPayload,
                    protectedArtifact,
                    EncryptionKeyPurpose.Document);

            firstKeyBytes = first.KeyBytes;

            EncryptionKeyMaterial second =
                ProtectedEncryptionKeyPayload.Decode(
                    legacyPayload,
                    protectedArtifact,
                    EncryptionKeyPurpose.Document);

            secondKeyBytes = second.KeyBytes;

            // Assert
            Assert.NotEqual(Guid.Empty, first.KeyId);
            Assert.Equal(0, first.KeyVersion);
            Assert.Equal(first.KeyId, second.KeyId);
            Assert.Equal(0, second.KeyVersion);

            Assert.Equal(legacyPayload, firstKeyBytes);
            Assert.Equal(legacyPayload, secondKeyBytes);

            Assert.NotSame(legacyPayload, firstKeyBytes);
            Assert.NotSame(firstKeyBytes, secondKeyBytes);
        }
        finally
        {
            Clear(
                legacyPayload,
                protectedArtifact,
                firstKeyBytes,
                secondKeyBytes);
        }
    }

    [Fact]
    public void Decode_LegacyPayload_DifferentArtifactsProduceDifferentIdentities()
    {
        // Arrange
        byte[] legacyPayload = CreateKeyBytes();
        byte[] firstArtifact = CreateProtectedArtifact(0x01);
        byte[] secondArtifact = CreateProtectedArtifact(0x02);

        byte[] firstKeyBytes = Array.Empty<byte>();
        byte[] secondKeyBytes = Array.Empty<byte>();

        try
        {
            // Act
            EncryptionKeyMaterial first =
                ProtectedEncryptionKeyPayload.Decode(
                    legacyPayload,
                    firstArtifact,
                    EncryptionKeyPurpose.Document);

            firstKeyBytes = first.KeyBytes;

            EncryptionKeyMaterial second =
                ProtectedEncryptionKeyPayload.Decode(
                    legacyPayload,
                    secondArtifact,
                    EncryptionKeyPurpose.Document);

            secondKeyBytes = second.KeyBytes;

            // Assert
            Assert.NotEqual(first.KeyId, second.KeyId);
            Assert.Equal(0, first.KeyVersion);
            Assert.Equal(0, second.KeyVersion);
        }
        finally
        {
            Clear(
                legacyPayload,
                firstArtifact,
                secondArtifact,
                firstKeyBytes,
                secondKeyBytes);
        }
    }

    [Fact]
    public void Decode_RejectsUnsupportedPayloadFormatVersion()
    {
        // Arrange
        byte[] keyBytes = CreateKeyBytes();
        byte[] protectedArtifact = CreateProtectedArtifact();
        byte[] payload = Array.Empty<byte>();

        try
        {
            payload = ProtectedEncryptionKeyPayload.Encode(
                keyBytes,
                EncryptionKeyPurpose.Document,
                Guid.NewGuid(),
                keyVersion: 1);

            payload[4] = ExpectedFormatVersion + 1;

            // Act
            Action act = () =>
                ProtectedEncryptionKeyPayload.Decode(
                    payload,
                    protectedArtifact,
                    EncryptionKeyPurpose.Document);

            // Assert
            Assert.Throws<CryptographicException>(act);
        }
        finally
        {
            Clear(keyBytes, protectedArtifact, payload);
        }
    }

    [Fact]
    public void Decode_RejectsInvalidPayloadMarker()
    {
        // Arrange
        byte[] keyBytes = CreateKeyBytes();
        byte[] protectedArtifact = CreateProtectedArtifact();
        byte[] payload = Array.Empty<byte>();

        try
        {
            payload = ProtectedEncryptionKeyPayload.Encode(
                keyBytes,
                EncryptionKeyPurpose.Document,
                Guid.NewGuid(),
                keyVersion: 1);

            payload[0] ^= 0xFF;

            // Act
            Action act = () =>
                ProtectedEncryptionKeyPayload.Decode(
                    payload,
                    protectedArtifact,
                    EncryptionKeyPurpose.Document);

            // Assert
            Assert.Throws<CryptographicException>(act);
        }
        finally
        {
            Clear(keyBytes, protectedArtifact, payload);
        }
    }

    [Fact]
    public void Decode_RejectsInvalidPayloadLength()
    {
        // Arrange
        byte[] malformedPayload = new byte[ExpectedPayloadLength - 1];
        byte[] protectedArtifact = CreateProtectedArtifact();

        try
        {
            // Act
            Action act = () =>
                ProtectedEncryptionKeyPayload.Decode(
                    malformedPayload,
                    protectedArtifact,
                    EncryptionKeyPurpose.Document);

            // Assert
            Assert.Throws<CryptographicException>(act);
        }
        finally
        {
            Clear(malformedPayload, protectedArtifact);
        }
    }

    [Fact]
    public void Decode_RejectsEmptyKeyIdentity()
    {
        // Arrange
        byte[] keyBytes = CreateKeyBytes();
        byte[] protectedArtifact = CreateProtectedArtifact();
        byte[] payload = Array.Empty<byte>();

        try
        {
            payload = ProtectedEncryptionKeyPayload.Encode(
                keyBytes,
                EncryptionKeyPurpose.Document,
                Guid.NewGuid(),
                keyVersion: 1);

            payload.AsSpan(KeyIdOffset, GuidLength).Clear();

            // Act
            Action act = () =>
                ProtectedEncryptionKeyPayload.Decode(
                    payload,
                    protectedArtifact,
                    EncryptionKeyPurpose.Document);

            // Assert
            Assert.Throws<CryptographicException>(act);
        }
        finally
        {
            Clear(keyBytes, protectedArtifact, payload);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Decode_RejectsNonPositiveLifecycleVersion(int keyVersion)
    {
        // Arrange
        byte[] keyBytes = CreateKeyBytes();
        byte[] protectedArtifact = CreateProtectedArtifact();
        byte[] payload = Array.Empty<byte>();

        try
        {
            payload = ProtectedEncryptionKeyPayload.Encode(
                keyBytes,
                EncryptionKeyPurpose.Document,
                Guid.NewGuid(),
                keyVersion: 1);

            BinaryPrimitives.WriteInt32LittleEndian(
                payload.AsSpan(KeyVersionOffset, sizeof(int)),
                keyVersion);

            // Act
            Action act = () =>
                ProtectedEncryptionKeyPayload.Decode(
                    payload,
                    protectedArtifact,
                    EncryptionKeyPurpose.Document);

            // Assert
            Assert.Throws<CryptographicException>(act);
        }
        finally
        {
            Clear(keyBytes, protectedArtifact, payload);
        }
    }

    [Fact]
    public void Decode_RejectsLegacyPayloadWithoutProtectedArtifact()
    {
        // Arrange
        byte[] legacyPayload = CreateKeyBytes();

        try
        {
            // Act
            Action act = () =>
                ProtectedEncryptionKeyPayload.Decode(
                    legacyPayload,
                    ReadOnlySpan<byte>.Empty,
                    EncryptionKeyPurpose.Document);

            // Assert
            Assert.Throws<CryptographicException>(act);
        }
        finally
        {
            Clear(legacyPayload);
        }
    }

    [Fact]
    public void Encode_RejectsNullKeyBytes()
    {
        // Arrange
        byte[]? keyBytes = null;

        // Act
        Action act = () =>
            ProtectedEncryptionKeyPayload.Encode(
                keyBytes!,
                EncryptionKeyPurpose.Document,
                Guid.NewGuid(),
                keyVersion: 1);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void Encode_RejectsInvalidKeyLength()
    {
        // Arrange
        byte[] keyBytes = new byte[
            EncryptionKeyMaterial.KeySizeBytes - 1];

        try
        {
            // Act
            Action act = () =>
                ProtectedEncryptionKeyPayload.Encode(
                    keyBytes,
                    EncryptionKeyPurpose.Document,
                    Guid.NewGuid(),
                    keyVersion: 1);

            // Assert
            Assert.Throws<CryptographicException>(act);
        }
        finally
        {
            Clear(keyBytes);
        }
    }

    [Fact]
    public void Encode_RejectsEmptyKeyIdentity()
    {
        // Arrange
        byte[] keyBytes = CreateKeyBytes();

        try
        {
            // Act
            Action act = () =>
                ProtectedEncryptionKeyPayload.Encode(
                    keyBytes,
                    EncryptionKeyPurpose.Document,
                    Guid.Empty,
                    keyVersion: 1);

            // Assert
            Assert.Throws<CryptographicException>(act);
        }
        finally
        {
            Clear(keyBytes);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Encode_RejectsNonPositiveLifecycleVersion(int keyVersion)
    {
        // Arrange
        byte[] keyBytes = CreateKeyBytes();

        try
        {
            // Act
            Action act = () =>
                ProtectedEncryptionKeyPayload.Encode(
                    keyBytes,
                    EncryptionKeyPurpose.Document,
                    Guid.NewGuid(),
                    keyVersion);

            // Assert
            Assert.Throws<CryptographicException>(act);
        }
        finally
        {
            Clear(keyBytes);
        }
    }

    [Fact]
    public void Decode_RejectsPayloadForDifferentKeyPurpose()
    {
        // Arrange
        byte[] keyBytes = CreateKeyBytes();
        byte[] protectedArtifact = CreateProtectedArtifact();
        byte[] payload = Array.Empty<byte>();

        try
        {
            payload = ProtectedEncryptionKeyPayload.Encode(
                keyBytes,
                EncryptionKeyPurpose.Document,
                Guid.NewGuid(),
                keyVersion: 1);

            // Act
            Action act = () =>
                ProtectedEncryptionKeyPayload.Decode(
                    payload,
                    protectedArtifact,
                    EncryptionKeyPurpose.Database);

            // Assert
            Assert.Throws<CryptographicException>(act);
        }
        finally
        {
            Clear(keyBytes, protectedArtifact, payload);
        }
    }

    [Fact]
    public void Decode_LegacyPayload_DifferentPurposesProduceDifferentIdentities()
    {
        // Arrange
        byte[] legacyPayload = CreateKeyBytes();
        byte[] protectedArtifact = CreateProtectedArtifact();

        byte[] documentKeyBytes = Array.Empty<byte>();
        byte[] databaseKeyBytes = Array.Empty<byte>();

        try
        {
            // Act
            EncryptionKeyMaterial documentKey =
                ProtectedEncryptionKeyPayload.Decode(
                    legacyPayload,
                    protectedArtifact,
                    EncryptionKeyPurpose.Document);

            documentKeyBytes = documentKey.KeyBytes;

            EncryptionKeyMaterial databaseKey =
                ProtectedEncryptionKeyPayload.Decode(
                    legacyPayload,
                    protectedArtifact,
                    EncryptionKeyPurpose.Database);

            databaseKeyBytes = databaseKey.KeyBytes;

            // Assert
            Assert.NotEqual(documentKey.KeyId, databaseKey.KeyId);
            Assert.Equal(0, documentKey.KeyVersion);
            Assert.Equal(0, databaseKey.KeyVersion);
        }
        finally
        {
            Clear(
                legacyPayload,
                protectedArtifact,
                documentKeyBytes,
                databaseKeyBytes);
        }
    }

    private static byte[] CreateKeyBytes()
    {
        byte[] keyBytes = new byte[EncryptionKeyMaterial.KeySizeBytes];

        for (int index = 0; index < keyBytes.Length; index++)
        {
            keyBytes[index] = (byte)index;
        }

        return keyBytes;
    }

    private static byte[] CreateProtectedArtifact(
        byte distinguishingByte = 0x01)
    {
        return new byte[]
        {
            0x44,
            0x56,
            0x50,
            distinguishingByte
        };
    }

    private static void Clear(params byte[][] buffers)
    {
        foreach (byte[] buffer in buffers)
        {
            CryptographicOperations.ZeroMemory(buffer);
        }
    }
}
