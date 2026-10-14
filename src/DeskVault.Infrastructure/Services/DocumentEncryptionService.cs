using System.Buffers.Binary;
using System.Security.Cryptography;
using DeskVault.Application.Documents.Processing;
using DeskVault.Shared.Resources;
using Microsoft.Extensions.Logging;

namespace DeskVault.Infrastructure.Services;

public sealed class DocumentEncryptionService
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int ChunkSize = 1024 * 1024;
    private const int HeaderSize = 4 + 4;

    private const uint FileMagic = 0x44564C54;
    private static readonly byte[] KeyLineageAssociatedDataDomain =
        "DeskVault.DocumentEncryption.KeyLineage.v1"u8.ToArray();
    private const uint FormatVersion = 2;

    private readonly IEncryptionKeyService _keyService;
    private readonly ILogger<DocumentEncryptionService> _logger;

    public DocumentEncryptionService(
        IEncryptionKeyService keyService,
        ILogger<DocumentEncryptionService> logger)
    {
        _keyService = keyService;
        _logger = logger;
    }

    public async Task EncryptAsync(
        Stream source,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation(
            LogMessages.DocumentEncryptionStarted);

        try
        {
            EncryptionKeyMaterial keyMaterial =
                await _keyService.GetOrCreateKeyAsync(
                    cancellationToken);

            byte[] key = keyMaterial.KeyBytes;

            byte[] associatedData =
                CreateKeyLineageAssociatedData(keyMaterial);

            await WriteHeaderAsync(
                destination,
                cancellationToken);

            byte[] buffer = new byte[ChunkSize];

            using var aes = new AesGcm(key, TagSize);

            int bytesRead;

            while ((bytesRead = await source.ReadAsync(
                buffer.AsMemory(0, buffer.Length),
                cancellationToken)) > 0)
            {
                byte[] nonce = RandomNumberGenerator.GetBytes(
                    NonceSize);

                byte[] ciphertext = new byte[bytesRead];
                byte[] tag = new byte[TagSize];

                aes.Encrypt(
                    nonce,
                    buffer.AsSpan(0, bytesRead),
                    ciphertext,
                    tag,
                    associatedData);

                byte[] length = new byte[sizeof(int)];

                BinaryPrimitives.WriteInt32LittleEndian(
                    length,
                    bytesRead);

                await destination.WriteAsync(
                    length,
                    cancellationToken);

                await destination.WriteAsync(
                    nonce,
                    cancellationToken);

                await destination.WriteAsync(
                    tag,
                    cancellationToken);

                await destination.WriteAsync(
                    ciphertext,
                    cancellationToken);
            }

            _logger.LogInformation(
                LogMessages.DocumentEncryptionCompleted);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                LogMessages.DocumentEncryptionFailed);

            throw;
        }
    }

    public async Task DecryptAsync(
        Stream source,
        Stream destination,
        CancellationToken cancellationToken = default,
        long? maximumPlaintextBytes = null)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (maximumPlaintextBytes is <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumPlaintextBytes),
                "Maximum plaintext bytes must be greater than zero.");
        }

        long totalPlaintextBytes = 0L;

        _logger.LogInformation(
            LogMessages.DocumentDecryptionStarted);

        try
        {
            EncryptionKeyMaterial keyMaterial =
                await _keyService.GetKeyAsync(
                    cancellationToken);

            byte[] key = keyMaterial.KeyBytes;

            byte[] associatedData =
                CreateKeyLineageAssociatedData(keyMaterial);

            await ReadAndValidateHeaderAsync(
                source,
                cancellationToken);

            using var aes = new AesGcm(key, TagSize);

            byte[] lengthBuffer = new byte[sizeof(int)];
            byte[] nonce = new byte[NonceSize];
            byte[] tag = new byte[TagSize];

            while (await TryReadAsync(
                source,
                lengthBuffer,
                cancellationToken))
            {
                int ciphertextLength =
                    BinaryPrimitives.ReadInt32LittleEndian(
                        lengthBuffer);

                if (ciphertextLength <= 0 ||
                    ciphertextLength > ChunkSize)
                {
                    throw new CryptographicException(
                        "The encrypted document contains an invalid chunk.");
                }

                if (maximumPlaintextBytes.HasValue &&
                    totalPlaintextBytes >
                        maximumPlaintextBytes.Value - ciphertextLength)
                {
                    throw new ResourceLimitExceededException(
                        maximumPlaintextBytes.Value,
                        totalPlaintextBytes + ciphertextLength);
                }

                await ReadExactlyAsync(
                    source,
                    nonce,
                    cancellationToken);

                await ReadExactlyAsync(
                    source,
                    tag,
                    cancellationToken);

                byte[] ciphertext =
                    new byte[ciphertextLength];

                await ReadExactlyAsync(
                    source,
                    ciphertext,
                    cancellationToken);

                byte[] plaintext =
                    new byte[ciphertextLength];

                aes.Decrypt(
                    nonce,
                    ciphertext,
                    tag,
                    plaintext,
                    associatedData);

                await destination.WriteAsync(
                    plaintext,
                    cancellationToken);

                totalPlaintextBytes += ciphertextLength;
            }

            _logger.LogInformation(
                LogMessages.DocumentDecryptionCompleted);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                LogMessages.DocumentDecryptionFailed);

            throw;
        }
    }

    /// <summary>
    /// Creates authenticated metadata that binds document ciphertext to its
    /// encryption key identity and lifecycle version.
    /// </summary>
    /// <remarks>
    /// Legacy version-zero keys use empty associated data to preserve
    /// compatibility with existing version-two encrypted documents.
    /// </remarks>
    private static byte[] CreateKeyLineageAssociatedData(
        EncryptionKeyMaterial keyMaterial)
    {
        if (keyMaterial.KeyVersion == 0)
        {
            return Array.Empty<byte>();
        }

        const int KeyIdLength = 16;

        int keyVersionOffset =
            KeyLineageAssociatedDataDomain.Length + KeyIdLength;

        byte[] associatedData =
            new byte[
                KeyLineageAssociatedDataDomain.Length +
                KeyIdLength +
                sizeof(int)];

        KeyLineageAssociatedDataDomain.CopyTo(
            associatedData,
            0);

        if (!keyMaterial.KeyId.TryWriteBytes(
                associatedData.AsSpan(
                    KeyLineageAssociatedDataDomain.Length,
                    KeyIdLength)))
        {
            CryptographicOperations.ZeroMemory(
                associatedData);

            throw new CryptographicException(
                "The document encryption key identity could not be encoded.");
        }

        BinaryPrimitives.WriteInt32LittleEndian(
            associatedData.AsSpan(
                keyVersionOffset,
                sizeof(int)),
            keyMaterial.KeyVersion);

        return associatedData;
    }

    private static async Task WriteHeaderAsync(
        Stream destination,
        CancellationToken cancellationToken)
    {
        byte[] header = new byte[HeaderSize];

        BinaryPrimitives.WriteUInt32LittleEndian(
            header.AsSpan(0, 4),
            FileMagic);

        BinaryPrimitives.WriteUInt32LittleEndian(
            header.AsSpan(4, 4),
            FormatVersion);

        await destination.WriteAsync(
            header,
            cancellationToken);
    }

    private static async Task ReadAndValidateHeaderAsync(
        Stream source,
        CancellationToken cancellationToken)
    {
        byte[] header = new byte[HeaderSize];

        await ReadExactlyAsync(
            source,
            header,
            cancellationToken);

        uint magic =
            BinaryPrimitives.ReadUInt32LittleEndian(
                header.AsSpan(0, 4));

        uint version =
            BinaryPrimitives.ReadUInt32LittleEndian(
                header.AsSpan(4, 4));

        if (magic != FileMagic)
        {
            throw new CryptographicException(
                "The encrypted document has an invalid format.");
        }

        if (version != FormatVersion)
        {
            throw new CryptographicException(
                "The encrypted document version is not supported.");
        }
    }

    private static async Task<bool> TryReadAsync(
        Stream source,
        byte[] buffer,
        CancellationToken cancellationToken)
    {
        int offset = 0;

        while (offset < buffer.Length)
        {
            int bytesRead = await source.ReadAsync(
                buffer.AsMemory(offset),
                cancellationToken);

            if (bytesRead == 0)
            {
                if (offset == 0)
                {
                    return false;
                }

                throw new CryptographicException(
                    "The encrypted document is truncated.");
            }

            offset += bytesRead;
        }

        return true;
    }

    private static async Task ReadExactlyAsync(
        Stream source,
        byte[] buffer,
        CancellationToken cancellationToken)
    {
        int offset = 0;

        while (offset < buffer.Length)
        {
            int bytesRead = await source.ReadAsync(
                buffer.AsMemory(offset),
                cancellationToken);

            if (bytesRead == 0)
            {
                throw new CryptographicException(
                    "The encrypted document is truncated.");
            }

            offset += bytesRead;
        }
    }
}
