using System.Security.Cryptography;

namespace DeskVault.Infrastructure.Services;

internal sealed class HashingWriteStream : Stream
{
    private readonly IncrementalHash _hash =
        IncrementalHash.CreateHash(
            HashAlgorithmName.SHA256);

    private bool _disposed;

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => !_disposed;

    public override long Length =>
        throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public byte[] GetHashAndReset()
    {
        ThrowIfDisposed();

        return _hash.GetHashAndReset();
    }

    public override void Flush()
    {
        ThrowIfDisposed();
    }

    public override Task FlushAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        return Task.CompletedTask;
    }

    public override int Read(
        byte[] buffer,
        int offset,
        int count)
    {
        throw new NotSupportedException();
    }

    public override long Seek(
        long offset,
        SeekOrigin origin)
    {
        throw new NotSupportedException();
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
        ArgumentNullException.ThrowIfNull(buffer);

        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(offset));
        }

        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(count));
        }

        if (offset > buffer.Length - count)
        {
            throw new ArgumentException(
                "Offset and count exceed the source buffer.");
        }

        ThrowIfDisposed();

        _hash.AppendData(
            buffer,
            offset,
            count);
    }

    public override void Write(
        ReadOnlySpan<byte> buffer)
    {
        ThrowIfDisposed();

        _hash.AppendData(buffer);
    }

    public override Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Write(
            buffer,
            offset,
            count);

        return Task.CompletedTask;
    }

    public override ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Write(buffer.Span);

        return ValueTask.CompletedTask;
    }

    protected override void Dispose(
        bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            _hash.Dispose();
        }

        base.Dispose(disposing);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(
                nameof(HashingWriteStream));
        }
    }
}
