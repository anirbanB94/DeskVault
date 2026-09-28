using System.Security.Cryptography;

namespace DeskVault.Infrastructure.Services;

internal sealed class HashingReadStream : Stream
{
    private readonly Stream _innerStream;
    private readonly IncrementalHash _hash;

    public HashingReadStream(
        Stream innerStream)
    {
        ArgumentNullException.ThrowIfNull(innerStream);

        _innerStream = innerStream;

        _hash =
            IncrementalHash.CreateHash(
                HashAlgorithmName.SHA256);
    }

    public string GetHashHex()
    {
        byte[] hash =
            _hash.GetHashAndReset();

        return Convert.ToHexString(hash)
            .ToLowerInvariant();
    }

    public override bool CanRead =>
        _innerStream.CanRead;

    public override bool CanSeek =>
        _innerStream.CanSeek;

    public override bool CanWrite =>
        false;

    public override long Length =>
        _innerStream.Length;

    public override long Position
    {
        get => _innerStream.Position;
        set => _innerStream.Position = value;
    }

    public override void Flush()
    {
        _innerStream.Flush();
    }

    public override Task FlushAsync(
        CancellationToken cancellationToken)
    {
        return _innerStream.FlushAsync(
            cancellationToken);
    }

    public override int Read(
        byte[] buffer,
        int offset,
        int count)
    {
        int bytesRead =
            _innerStream.Read(
                buffer,
                offset,
                count);

        if (bytesRead > 0)
        {
            _hash.AppendData(
                buffer.AsSpan(
                    offset,
                    bytesRead));
        }

        return bytesRead;
    }

    public override int Read(
        Span<byte> buffer)
    {
        int bytesRead =
            _innerStream.Read(
                buffer);

        if (bytesRead > 0)
        {
            _hash.AppendData(
                buffer[..bytesRead]);
        }

        return bytesRead;
    }

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        int bytesRead =
            await _innerStream.ReadAsync(
                buffer,
                cancellationToken);

        if (bytesRead > 0)
        {
            _hash.AppendData(
                buffer.Span[..bytesRead]);
        }

        return bytesRead;
    }

    public override async Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken)
    {
        int bytesRead =
            await _innerStream.ReadAsync(
                buffer,
                offset,
                count,
                cancellationToken);

        if (bytesRead > 0)
        {
            _hash.AppendData(
                buffer.AsSpan(
                    offset,
                    bytesRead));
        }

        return bytesRead;
    }

    public override long Seek(
        long offset,
        SeekOrigin origin)
    {
        return _innerStream.Seek(
            offset,
            origin);
    }

    public override void SetLength(
        long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(
        byte[] buffer,
        int offset,
        int count)
    {
        throw new NotSupportedException();
    }

    protected override void Dispose(
        bool disposing)
    {
        if (disposing)
        {
            _hash.Dispose();
            _innerStream.Dispose();
        }

        base.Dispose(disposing);
    }

    public override ValueTask DisposeAsync()
    {
        _hash.Dispose();

        return _innerStream.DisposeAsync();
    }
}
