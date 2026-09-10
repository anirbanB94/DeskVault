using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace DeskVault.Application.Documents.Chunking;

public static class DocumentChunkIdentity
{
    private const string IdentityDomain =
        "DeskVault.DocumentChunk.v1";

    public static Guid CreateLogicalId(
        Guid documentId,
        int order)
    {
        byte[] domainBytes =
            Encoding.UTF8.GetBytes(
                IdentityDomain);

        Span<byte> documentIdBytes =
            stackalloc byte[16];

        bool documentIdWritten =
            documentId.TryWriteBytes(
                documentIdBytes);

        if (!documentIdWritten)
        {
            throw new InvalidOperationException(
                "The document identifier could not be encoded.");
        }

        Span<byte> orderBytes =
            stackalloc byte[4];

        BinaryPrimitives.WriteInt32LittleEndian(
            orderBytes,
            order);

        byte[] input =
            new byte[
                domainBytes.Length +
                documentIdBytes.Length +
                orderBytes.Length];

        domainBytes.CopyTo(
            input,
            0);

        documentIdBytes.CopyTo(
            input.AsSpan(
                domainBytes.Length,
                documentIdBytes.Length));

        orderBytes.CopyTo(
            input.AsSpan(
                domainBytes.Length +
                documentIdBytes.Length,
                orderBytes.Length));

        byte[] hash =
            SHA256.HashData(
                input);

        return new Guid(
            hash.AsSpan(0, 16));
    }

    public static string ComputeContentHash(
        string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        byte[] contentBytes =
            Encoding.UTF8.GetBytes(
                text);

        byte[] hash =
            SHA256.HashData(
                contentBytes);

        return Convert.ToHexString(
                hash)
            .ToLowerInvariant();
    }
}
