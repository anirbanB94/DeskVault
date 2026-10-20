using System.Security.Cryptography;

namespace DeskVault.Infrastructure.Services;

/// <summary>
/// A private plaintext staging stream that clears backing arrays when
/// MemoryStream replaces them during growth and when the stream is disposed.
/// </summary>
internal sealed class ZeroingMemoryStream : MemoryStream
{
    private readonly List<byte[]>? _retiredBuffersForTesting;

    internal ZeroingMemoryStream(
        bool trackRetiredBuffersForTesting = false)
    {
        if (trackRetiredBuffersForTesting)
        {
            _retiredBuffersForTesting = [];
        }
    }

    internal IReadOnlyList<byte[]> RetiredBuffersForTesting =>
        _retiredBuffersForTesting is null
            ? Array.Empty<byte[]>()
            : _retiredBuffersForTesting;

    public override void Write(
        byte[] buffer,
        int offset,
        int count)
    {
        byte[] previousBuffer = GetBuffer();

        try
        {
            base.Write(buffer, offset, count);
        }
        finally
        {
            ClearSupersededBuffer(previousBuffer);
        }
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        byte[] previousBuffer = GetBuffer();

        try
        {
            base.Write(buffer);
        }
        finally
        {
            ClearSupersededBuffer(previousBuffer);
        }
    }

    public override void WriteByte(byte value)
    {
        byte[] previousBuffer = GetBuffer();

        try
        {
            base.WriteByte(value);
        }
        finally
        {
            ClearSupersededBuffer(previousBuffer);
        }
    }

    public override Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        return WriteAsync(
            buffer.AsMemory(offset, count),
            cancellationToken).AsTask();
    }

    public override async ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        byte[] previousBuffer = GetBuffer();

        try
        {
            await base.WriteAsync(
                buffer,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ClearSupersededBuffer(previousBuffer);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            CryptographicOperations.ZeroMemory(GetBuffer());
        }

        base.Dispose(disposing);
    }

    private void ClearSupersededBuffer(byte[] previousBuffer)
    {
        byte[] currentBuffer = GetBuffer();

        if (ReferenceEquals(previousBuffer, currentBuffer))
        {
            return;
        }

        CryptographicOperations.ZeroMemory(previousBuffer);

        // Retain references only for tests that explicitly request tracking.
        // The normal production path retains no history of retired arrays.
        if (_retiredBuffersForTesting is not null &&
            !_retiredBuffersForTesting.Any(
                buffer => ReferenceEquals(buffer, previousBuffer)))
        {
            _retiredBuffersForTesting.Add(previousBuffer);
        }
    }
}
