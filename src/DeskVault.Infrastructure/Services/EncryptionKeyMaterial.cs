using System.Security.Cryptography;

namespace DeskVault.Infrastructure.Services;

/// <summary>
/// Represents encryption key bytes and their non-secret lifecycle metadata.
/// </summary>
/// <remarks>
/// The caller owns <see cref="KeyBytes"/> and must clear the buffer after use.
/// The key identifier is metadata and must never be used as key material.
///
/// Version zero is reserved for legacy keys that predate explicit lifecycle
/// metadata. Newly provisioned keys must use a positive version.
/// </remarks>
public sealed class EncryptionKeyMaterial
{
    public const int KeySizeBytes = 32;

    public EncryptionKeyMaterial(
        byte[] keyBytes,
        Guid keyId,
        int keyVersion)
    {
        ArgumentNullException.ThrowIfNull(keyBytes);

        if (keyBytes.Length != KeySizeBytes)
        {
            throw new CryptographicException(
                "The encryption key has an invalid length.");
        }

        if (keyId == Guid.Empty)
        {
            throw new CryptographicException(
                "The encryption key identity is invalid.");
        }

        if (keyVersion < 0)
        {
            throw new CryptographicException(
                "The encryption key version is invalid.");
        }

        KeyBytes = keyBytes;
        KeyId = keyId;
        KeyVersion = keyVersion;
    }

    /// <summary>
    /// Gets the key buffer owned by the caller.
    /// The caller must clear it after use.
    /// </summary>
    public byte[] KeyBytes { get; }

    /// <summary>
    /// Gets the non-secret identifier associated with the key.
    /// </summary>
    public Guid KeyId { get; }

    /// <summary>
    /// Gets the lifecycle version associated with the key.
    /// </summary>
    public int KeyVersion { get; }
}
