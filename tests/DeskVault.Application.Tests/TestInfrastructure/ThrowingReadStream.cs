namespace DeskVault.Application.Tests.TestInfrastructure;

public sealed class ThrowingReadStream : Stream
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
        SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public override void SetLength(long value)
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

    public override void Flush()
    {
    }
}
