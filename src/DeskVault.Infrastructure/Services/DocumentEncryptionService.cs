using System.Buffers.Binary;
using System.Security.Cryptography;
using DeskVault.Application.Documents.Processing;
using DeskVault.Shared.Resources;
using Microsoft.Extensions.Logging;
using DeskVault.Shared.Extensions;
using DiagnosticOperationCodes = DeskVault.Shared.Resources.DiagnosticOperationCodes;

namespace DeskVault.Infrastructure.Services;

public sealed class DocumentEncryptionService
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int ChunkSize = 1024 * 1024;
    private const int HeaderSize = sizeof(uint) + sizeof(uint);

    private const uint FileMagic = 0x44564C54;
    private const uint LegacyFormatVersion = 2;
    private const uint FormatVersion = 3;

    private const byte ChunkRecordType = 1;
    private const byte FinalRecordType = 2;

    private static readonly byte[] KeyLineageAssociatedDataDomain =
        "DeskVault.DocumentEncryption.KeyLineage.v1"u8.ToArray();

    private static readonly byte[] RecordAssociatedDataDomain =
        "DeskVault.DocumentEncryption.Record.v3"u8.ToArray();

    private readonly IEncryptionKeyService _keyService;
    private readonly ILogger<DocumentEncryptionService> _logger;

    // Allows tests to retain references to operation-owned plaintext buffers
    // at allocation time without participating in the cleanup path.
    internal Func<int, byte[]>? DecryptedPlaintextBufferFactoryForTesting
    {
        get;
        set;
    }

    public DocumentEncryptionService(
        IEncryptionKeyService keyService,
        ILogger<DocumentEncryptionService> logger)
    {
        _keyService = keyService;
        _logger = logger;
    }

    /// <summary>
    /// Writes a legacy version-2 record for compatibility scenarios.
    /// Production writes must use the document-aware EncryptAsync overload.
    /// </summary>
    public Task EncryptLegacyVersion2Async(
        Stream source,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        return EncryptCoreAsync(
            source,
            destination,
            LegacyFormatVersion,
            null,
            cancellationToken);
    }

    /// <summary>
    /// Writes a version-3 record bound to the canonical document identity.
    /// </summary>
    public Task EncryptAsync(
        Stream source,
        Stream destination,
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        if (documentId == Guid.Empty)
        {
            throw new ArgumentException(
                "The document identity must not be empty.",
                nameof(documentId));
        }

        return EncryptCoreAsync(
            source,
            destination,
            FormatVersion,
            documentId,
            cancellationToken);
    }

    private async Task EncryptCoreAsync(
        Stream source,
        Stream destination,
        uint formatVersion,
        Guid? documentId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation(
            LogMessages.DocumentEncryptionStarted);

        byte[]? acquiredKeyBuffer = null;
        byte[]? plaintextBuffer = null;

        try
        {
            EncryptionKeyMaterial keyMaterial =
                await _keyService.GetKeyAsync(
                    cancellationToken);

            byte[] key = keyMaterial.KeyBytes;
            acquiredKeyBuffer = key;

            byte[]? legacyAssociatedData =
                formatVersion == LegacyFormatVersion
                    ? CreateKeyLineageAssociatedData(keyMaterial)
                    : null;

            await WriteHeaderAsync(
                destination,
                formatVersion,
                cancellationToken);

            byte[] buffer = new byte[ChunkSize];
            plaintextBuffer = buffer;

            using var aes = new AesGcm(key, TagSize);

            long chunkIndex = 0;
            long totalPlaintextBytes = 0;

            while (true)
            {
                try
                {
                    int bytesRead = await source.ReadAsync(
                        buffer.AsMemory(0, buffer.Length),
                        cancellationToken);

                    if (bytesRead == 0)
                    {
                        break;
                    }

                    if (formatVersion == FormatVersion &&
                        (chunkIndex == long.MaxValue ||
                         totalPlaintextBytes > long.MaxValue - bytesRead))
                    {
                        throw new CryptographicException(
                            "The encrypted document exceeds supported record limits.");
                    }

                    byte[] nonce = RandomNumberGenerator.GetBytes(
                        NonceSize);

                    byte[] ciphertext = new byte[bytesRead];
                    byte[] tag = new byte[TagSize];

                    byte[] associatedData =
                        legacyAssociatedData ??
                        CreateRecordAssociatedData(
                            keyMaterial,
                            documentId!.Value,
                            ChunkRecordType,
                            chunkIndex,
                            bytesRead);

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

                    totalPlaintextBytes += bytesRead;
                    chunkIndex++;
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(buffer);
                }
            }

            if (formatVersion == FormatVersion)
            {
                await WriteFinalRecordAsync(
                    destination,
                    aes,
                    keyMaterial,
                    documentId!.Value,
                    chunkIndex,
                    totalPlaintextBytes,
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
            _logger.LogSafeFailure(
                LogLevel.Error,
                DiagnosticOperationCodes.DocumentEncryptionFailure,
                LogMessages.DocumentEncryptionFailed,
                ex);

            throw;
        }
        finally
        {
            if (plaintextBuffer is not null)
            {
                CryptographicOperations.ZeroMemory(
                    plaintextBuffer);
            }

            if (acquiredKeyBuffer is not null)
            {
                CryptographicOperations.ZeroMemory(
                    acquiredKeyBuffer);
            }
        }
    }

    /// <summary>
    /// Decrypts legacy version-2 records without requiring document identity.
    /// Version-3 records require the document-aware overload.
    /// </summary>
    public Task DecryptAsync(
        Stream source,
        Stream destination,
        CancellationToken cancellationToken = default,
        long? maximumPlaintextBytes = null)
    {
        return DecryptCoreAsync(
            source,
            destination,
            null,
            cancellationToken,
            maximumPlaintextBytes);
    }

    /// <summary>
    /// Decrypts a record using the canonical document identity.
    /// Supports legacy version-2 records and authenticated version-3 records.
    /// </summary>
    public Task DecryptAsync(
        Stream source,
        Stream destination,
        Guid documentId,
        CancellationToken cancellationToken = default,
        long? maximumPlaintextBytes = null)
    {
        if (documentId == Guid.Empty)
        {
            throw new ArgumentException(
                "The document identity must not be empty.",
                nameof(documentId));
        }

        return DecryptCoreAsync(
            source,
            destination,
            documentId,
            cancellationToken,
            maximumPlaintextBytes);
    }

    private async Task DecryptCoreAsync(
        Stream source,
        Stream destination,
        Guid? documentId,
        CancellationToken cancellationToken,
        long? maximumPlaintextBytes)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (maximumPlaintextBytes is <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumPlaintextBytes),
                "Maximum plaintext bytes must be greater than zero.");
        }

        long totalPlaintextBytes = 0;
        long chunkIndex = 0;
        bool finalRecordFound = false;

        _logger.LogInformation(
            LogMessages.DocumentDecryptionStarted);

        byte[]? acquiredKeyBuffer = null;

        try
        {
            EncryptionKeyMaterial keyMaterial =
                await _keyService.GetKeyAsync(
                    cancellationToken);

            byte[] key = keyMaterial.KeyBytes;
            acquiredKeyBuffer = key;

            uint formatVersion =
                await ReadAndValidateHeaderAsync(
                    source,
                    cancellationToken);

            if (formatVersion == FormatVersion &&
                !documentId.HasValue)
            {
                throw new CryptographicException(
                    "Document identity is required for this encrypted record.");
            }

            byte[]? legacyAssociatedData =
                formatVersion == LegacyFormatVersion
                    ? CreateKeyLineageAssociatedData(keyMaterial)
                    : null;

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

                // In version 3, a zero-length record is reserved for the
                // authenticated terminal marker. It is never a data chunk.
                if (formatVersion == FormatVersion &&
                    ciphertextLength == 0)
                {
                    await ReadExactlyAsync(
                        source,
                        nonce,
                        cancellationToken);

                    await ReadExactlyAsync(
                        source,
                        tag,
                        cancellationToken);

                    byte[] finalAssociatedData =
                        CreateRecordAssociatedData(
                            keyMaterial,
                            documentId!.Value,
                            FinalRecordType,
                            chunkIndex,
                            totalPlaintextBytes);

                    byte[] empty = Array.Empty<byte>();

                    aes.Decrypt(
                        nonce,
                        empty,
                        tag,
                        empty,
                        finalAssociatedData);

                    await EnsureEndOfStreamAsync(
                        source,
                        cancellationToken);

                    finalRecordFound = true;
                    break;
                }

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

                if (formatVersion == FormatVersion &&
                    (chunkIndex == long.MaxValue ||
                     totalPlaintextBytes >
                        long.MaxValue - ciphertextLength))
                {
                    throw new CryptographicException(
                        "The encrypted document exceeds supported record limits.");
                }

                await ReadExactlyAsync(
                    source,
                    nonce,
                    cancellationToken);

                await ReadExactlyAsync(
                    source,
                    tag,
                    cancellationToken);

                byte[] ciphertext = new byte[ciphertextLength];
                byte[]? plaintext = null;

                try
                {
                    plaintext =
                        DecryptedPlaintextBufferFactoryForTesting?.Invoke(
                            ciphertextLength)
                        ?? new byte[ciphertextLength];

                    if (plaintext.Length != ciphertextLength)
                    {
                        throw new InvalidOperationException(
                            "The decryption plaintext buffer has an unexpected length.");
                    }

                    await ReadExactlyAsync(
                        source,
                        ciphertext,
                        cancellationToken);

                    byte[] associatedData =
                        legacyAssociatedData ??
                        CreateRecordAssociatedData(
                            keyMaterial,
                            documentId!.Value,
                            ChunkRecordType,
                            chunkIndex,
                            ciphertextLength);

                    // Plaintext is produced only after this chunk's GCM
                    // authentication succeeds.
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
                    chunkIndex++;
                }
                finally
                {
                    if (plaintext is not null)
                    {
                        CryptographicOperations.ZeroMemory(plaintext);
                    }
                }
            }

            if (formatVersion == FormatVersion &&
                !finalRecordFound)
            {
                throw new CryptographicException(
                    "The encrypted document is missing its authenticated completion record.");
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
            _logger.LogSafeFailure(
                LogLevel.Error,
                DiagnosticOperationCodes.DocumentDecryptionFailure,
                LogMessages.DocumentDecryptionFailed,
                ex);

            throw;
        }
        finally
        {
            if (acquiredKeyBuffer is not null)
            {
                CryptographicOperations.ZeroMemory(
                    acquiredKeyBuffer);
            }
        }
    }

    /// <summary>
    /// Creates associated data for an encrypted document's key lineage.
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

    /// <summary>
    /// Creates version-three associated data for a data chunk or final record.
    /// The record type, sequence index, and declared length are authenticated
    /// along with the key lineage and canonical document identity.
    /// </summary>
    private static byte[] CreateRecordAssociatedData(
        EncryptionKeyMaterial keyMaterial,
        Guid documentId,
        byte recordType,
        long sequenceIndex,
        long declaredLength)
    {
        const int KeyIdLength = 16;
        const int DocumentIdLength = 16;

        byte[] associatedData =
            new byte[
                RecordAssociatedDataDomain.Length +
                KeyIdLength +
                sizeof(int) +
                DocumentIdLength +
                sizeof(byte) +
                sizeof(long) +
                sizeof(long)];

        int offset = 0;

        RecordAssociatedDataDomain.CopyTo(
            associatedData,
            offset);

        offset += RecordAssociatedDataDomain.Length;

        if (!keyMaterial.KeyId.TryWriteBytes(
            associatedData.AsSpan(
                offset,
                KeyIdLength)))
        {
            throw new CryptographicException(
                "The document encryption key identity could not be encoded.");
        }

        offset += KeyIdLength;

        BinaryPrimitives.WriteInt32LittleEndian(
            associatedData.AsSpan(
                offset,
                sizeof(int)),
            keyMaterial.KeyVersion);

        offset += sizeof(int);

        if (!documentId.TryWriteBytes(
            associatedData.AsSpan(
                offset,
                DocumentIdLength)))
        {
            throw new CryptographicException(
                "The document identity could not be encoded.");
        }

        offset += DocumentIdLength;

        associatedData[offset++] = recordType;

        BinaryPrimitives.WriteInt64LittleEndian(
            associatedData.AsSpan(
                offset,
                sizeof(long)),
            sequenceIndex);

        offset += sizeof(long);

        BinaryPrimitives.WriteInt64LittleEndian(
            associatedData.AsSpan(
                offset,
                sizeof(long)),
            declaredLength);

        return associatedData;
    }

    private static async Task WriteHeaderAsync(
        Stream destination,
        uint formatVersion,
        CancellationToken cancellationToken)
    {
        byte[] header = new byte[HeaderSize];

        BinaryPrimitives.WriteUInt32LittleEndian(
            header.AsSpan(0, sizeof(uint)),
            FileMagic);

        BinaryPrimitives.WriteUInt32LittleEndian(
            header.AsSpan(sizeof(uint), sizeof(uint)),
            formatVersion);

        await destination.WriteAsync(
            header,
            cancellationToken);
    }

    /// <summary>
    /// Writes a zero-length terminal record authenticated with the number
    /// of chunks and total plaintext length. Its presence proves that the
    /// writer reached the end of the version-three record.
    /// </summary>
    private static async Task WriteFinalRecordAsync(
        Stream destination,
        AesGcm aes,
        EncryptionKeyMaterial keyMaterial,
        Guid documentId,
        long chunkCount,
        long totalPlaintextBytes,
        CancellationToken cancellationToken)
    {
        byte[] length = new byte[sizeof(int)];

        BinaryPrimitives.WriteInt32LittleEndian(
            length,
            0);

        byte[] nonce = RandomNumberGenerator.GetBytes(
            NonceSize);

        byte[] tag = new byte[TagSize];
        byte[] empty = Array.Empty<byte>();

        byte[] associatedData =
            CreateRecordAssociatedData(
                keyMaterial,
                documentId,
                FinalRecordType,
                chunkCount,
                totalPlaintextBytes);

        aes.Encrypt(
            nonce,
            empty,
            empty,
            tag,
            associatedData);

        await destination.WriteAsync(
            length,
            cancellationToken);

        await destination.WriteAsync(
            nonce,
            cancellationToken);

        await destination.WriteAsync(
            tag,
            cancellationToken);
    }

    private static async Task<uint> ReadAndValidateHeaderAsync(
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
                header.AsSpan(0, sizeof(uint)));

        uint version =
            BinaryPrimitives.ReadUInt32LittleEndian(
                header.AsSpan(sizeof(uint), sizeof(uint)));

        if (magic != FileMagic)
        {
            throw new CryptographicException(
                "The encrypted document has an invalid format.");
        }

        if (version != LegacyFormatVersion &&
            version != FormatVersion)
        {
            throw new CryptographicException(
                "The encrypted document version is not supported.");
        }

        return version;
    }

    private static async Task EnsureEndOfStreamAsync(
        Stream source,
        CancellationToken cancellationToken)
    {
        byte[] trailingByte = new byte[1];

        int bytesRead = await source.ReadAsync(
            trailingByte.AsMemory(),
            cancellationToken);

        if (bytesRead != 0)
        {
            throw new CryptographicException(
                "The encrypted document contains trailing data.");
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
