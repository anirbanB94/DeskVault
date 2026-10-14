using System.Buffers.Binary;
using System.Security.Cryptography;

namespace DeskVault.Infrastructure.Services;

internal enum EncryptionKeyPurpose : byte
{
    Document = 1,
    Database = 2
}

/// <summary>
/// Encodes and decodes the plaintext payload protected by Windows DPAPI.
/// </summary>
/// <remarks>
/// New payloads include a purpose marker so a document-encryption key
/// payload cannot be accepted as a database-encryption key payload, or
/// vice versa.
///
/// Legacy payloads contain only the 32-byte encryption key. Their identity
/// is derived deterministically from the protected artifact and the
/// expected purpose. Legacy artifacts are not rewritten by this codec.
///
/// Callers must clear encoded and decoded plaintext buffers when they are
/// no longer needed.
/// </remarks>
internal static class ProtectedEncryptionKeyPayload
{
    private const int MagicLength = 4;
    private const int FormatVersionOffset = MagicLength;
    private const int PurposeOffset = FormatVersionOffset + 1;
    private const int GuidLength = 16;
    private const int KeyIdOffset = PurposeOffset + 1;
    private const int KeyVersionOffset = KeyIdOffset + GuidLength;
    private const int KeyBytesOffset = KeyVersionOffset + sizeof(int);

    private const byte CurrentFormatVersion = 1;

    private const int PayloadLength =
        KeyBytesOffset + EncryptionKeyMaterial.KeySizeBytes;

    private static ReadOnlySpan<byte> Magic => "DVKP"u8;

    private static ReadOnlySpan<byte> LegacyIdentityDomain =>
        "DeskVault.LegacyProtectedKeyIdentity.v1"u8;

    private static Guid GetLegacyFallbackKeyId(
        EncryptionKeyPurpose purpose)
    {
        return purpose switch
        {
            EncryptionKeyPurpose.Document =>
                Guid.Parse("00000001-0000-0000-0000-000000000000"),

            EncryptionKeyPurpose.Database =>
                Guid.Parse("00000002-0000-0000-0000-000000000000"),

            _ => throw new ArgumentOutOfRangeException(
                nameof(purpose))
        };
    }

    /// <summary>
    /// Creates a plaintext payload for a newly provisioned encryption key.
    /// The returned buffer contains sensitive key bytes.
    /// </summary>
    internal static byte[] Encode(
        byte[] keyBytes,
        EncryptionKeyPurpose purpose,
        Guid keyId,
        int keyVersion)
    {
        ArgumentNullException.ThrowIfNull(keyBytes);

        ValidatePurpose(purpose);

        if (keyBytes.Length != EncryptionKeyMaterial.KeySizeBytes)
        {
            throw new CryptographicException(
                "The encryption key has an invalid length.");
        }

        if (keyId == Guid.Empty)
        {
            throw new CryptographicException(
                "The encryption key identity is invalid.");
        }

        if (keyVersion <= 0)
        {
            throw new CryptographicException(
                "A newly provisioned encryption key must have a positive version.");
        }

        byte[] payload = new byte[PayloadLength];

        Magic.CopyTo(payload.AsSpan(0, MagicLength));
        payload[FormatVersionOffset] = CurrentFormatVersion;
        payload[PurposeOffset] = (byte)purpose;

        if (!keyId.TryWriteBytes(
                payload.AsSpan(KeyIdOffset, GuidLength)))
        {
            CryptographicOperations.ZeroMemory(payload);

            throw new CryptographicException(
                "The encryption key identity could not be encoded.");
        }

        BinaryPrimitives.WriteInt32LittleEndian(
            payload.AsSpan(KeyVersionOffset, sizeof(int)),
            keyVersion);

        keyBytes.CopyTo(payload.AsSpan(KeyBytesOffset));

        return payload;
    }

    /// <summary>
    /// Decodes a DPAPI-unprotected payload and validates its purpose.
    /// </summary>
    /// <param name="payload">
    /// The plaintext returned by DPAPI. The caller remains responsible
    /// for clearing this buffer.
    /// </param>
    /// <param name="protectedArtifact">
    /// The original persisted DPAPI-protected artifact. This is required
    /// to derive stable identity metadata for a legacy payload.
    /// </param>
    /// <param name="expectedPurpose">
    /// The key purpose expected by the retrieving service.
    /// </param>
    internal static EncryptionKeyMaterial Decode(
        ReadOnlySpan<byte> payload,
        ReadOnlySpan<byte> protectedArtifact,
        EncryptionKeyPurpose expectedPurpose)
    {
        ValidatePurpose(expectedPurpose);

        // Legacy payloads contain the key bytes only. Their purpose is
        // inferred from the canonical artifact/service that retrieved them.
        if (payload.Length == EncryptionKeyMaterial.KeySizeBytes)
        {
            return DecodeLegacy(
                payload,
                protectedArtifact,
                expectedPurpose);
        }

        if (payload.Length != PayloadLength)
        {
            throw new CryptographicException(
                "The protected encryption key payload has an invalid length.");
        }

        if (!payload[..MagicLength].SequenceEqual(Magic))
        {
            throw new CryptographicException(
                "The protected encryption key payload has an invalid marker.");
        }

        if (payload[FormatVersionOffset] != CurrentFormatVersion)
        {
            throw new CryptographicException(
                "The protected encryption key payload format is unsupported.");
        }

        if (payload[PurposeOffset] != (byte)expectedPurpose)
        {
            throw new CryptographicException(
                "The protected encryption key purpose does not match the retrieving service.");
        }

        Guid keyId = new(
            payload.Slice(KeyIdOffset, GuidLength));

        if (keyId == Guid.Empty)
        {
            throw new CryptographicException(
                "The encryption key identity is invalid.");
        }

        int keyVersion = BinaryPrimitives.ReadInt32LittleEndian(
            payload.Slice(KeyVersionOffset, sizeof(int)));

        if (keyVersion <= 0)
        {
            throw new CryptographicException(
                "The encryption key version is invalid.");
        }

        byte[] keyBytes = payload
            .Slice(KeyBytesOffset, EncryptionKeyMaterial.KeySizeBytes)
            .ToArray();

        try
        {
            return new EncryptionKeyMaterial(
                keyBytes,
                keyId,
                keyVersion);
        }
        catch
        {
            CryptographicOperations.ZeroMemory(keyBytes);
            throw;
        }
    }

    private static EncryptionKeyMaterial DecodeLegacy(
        ReadOnlySpan<byte> payload,
        ReadOnlySpan<byte> protectedArtifact,
        EncryptionKeyPurpose purpose)
    {
        if (protectedArtifact.IsEmpty)
        {
            throw new CryptographicException(
                "The protected artifact is required to identify a legacy encryption key.");
        }

        Guid keyId = DeriveLegacyKeyId(
            protectedArtifact,
            purpose);

        byte[] keyBytes = payload.ToArray();

        try
        {
            return new EncryptionKeyMaterial(
                keyBytes,
                keyId,
                keyVersion: 0);
        }
        catch
        {
            CryptographicOperations.ZeroMemory(keyBytes);
            throw;
        }
    }

    private static Guid DeriveLegacyKeyId(
        ReadOnlySpan<byte> protectedArtifact,
        EncryptionKeyPurpose purpose)
    {
        using IncrementalHash hash =
            IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        hash.AppendData(LegacyIdentityDomain);

        Span<byte> encodedPurpose = stackalloc byte[1];
        encodedPurpose[0] = (byte)purpose;

        hash.AppendData(encodedPurpose);
        hash.AppendData(protectedArtifact);

        byte[] digest = hash.GetHashAndReset();

        try
        {
            Guid keyId = new(digest.AsSpan(0, GuidLength));

            if (keyId == Guid.Empty)
            {
                keyId = new Guid(
                    digest.AsSpan(GuidLength, GuidLength));
            }

            if (keyId == Guid.Empty)
            {
                keyId = GetLegacyFallbackKeyId(purpose);
            }

            return keyId;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(digest);
        }
    }

    private static void ValidatePurpose(
        EncryptionKeyPurpose purpose)
    {
        if (purpose is not EncryptionKeyPurpose.Document
            and not EncryptionKeyPurpose.Database)
        {
            throw new ArgumentOutOfRangeException(
                nameof(purpose),
                "The encryption key purpose is unsupported.");
        }
    }
}
