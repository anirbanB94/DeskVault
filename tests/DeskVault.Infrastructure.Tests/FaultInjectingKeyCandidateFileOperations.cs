using System.IO;
using DeskVault.Infrastructure.Services;

namespace DeskVault.Infrastructure.Tests;

internal sealed class FaultInjectingKeyCandidateFileOperations :
    IKeyCandidateFileOperations
{
    private readonly IKeyCandidateFileOperations _inner =
        new KeyCandidateFileOperations();

    public string? CandidateFilePathOverride { get; init; }

    public bool FailAfterPartialWrite { get; init; }

    public bool FailDurableFlush { get; init; }

    public bool FailDelete { get; init; }

    public string? LastCandidateFilePath { get; private set; }

    public int DeleteCallCount { get; private set; }

    public int PublishCallCount { get; private set; }

    public string GetCandidateFilePath(
        string securityDirectory,
        string canonicalKeyFileName)
    {
        string candidateFilePath =
            CandidateFilePathOverride ??
            _inner.GetCandidateFilePath(
                securityDirectory,
                canonicalKeyFileName);

        LastCandidateFilePath = candidateFilePath;

        return candidateFilePath;
    }

    public Stream CreateCandidateFile(
        string candidateFilePath)
    {
        Stream candidateFile =
            _inner.CreateCandidateFile(
                candidateFilePath);

        if (!FailAfterPartialWrite)
        {
            return candidateFile;
        }

        return new PartialWriteThenFailStream(
            candidateFile);
    }

    public void FlushCandidateFileToDisk(
        Stream candidateFile)
    {
        Stream streamToFlush =
            candidateFile is PartialWriteThenFailStream partialWriteStream
                ? partialWriteStream.InnerStream
                : candidateFile;

        if (FailDurableFlush)
        {
            throw new IOException(
                "Injected candidate durable-flush failure.");
        }

        _inner.FlushCandidateFileToDisk(
            streamToFlush);
    }

    public void PublishCandidateFile(
        string candidateFilePath,
        string canonicalKeyFilePath)
    {
        PublishCallCount++;

        _inner.PublishCandidateFile(
            candidateFilePath,
            canonicalKeyFilePath);
    }

    public void DeleteCandidateFile(
        string candidateFilePath)
    {
        DeleteCallCount++;

        if (FailDelete)
        {
            throw new IOException(
                "Injected candidate cleanup failure.");
        }

        _inner.DeleteCandidateFile(
            candidateFilePath);
    }

    private sealed class PartialWriteThenFailStream : Stream
    {
        private readonly Stream _inner;
        private bool _writeAttempted;

        public PartialWriteThenFailStream(
            Stream inner)
        {
            _inner = inner;
        }

        public Stream InnerStream => _inner;

        public override bool CanRead => _inner.CanRead;

        public override bool CanSeek => _inner.CanSeek;

        public override bool CanWrite => _inner.CanWrite;

        public override long Length => _inner.Length;

        public override long Position
        {
            get => _inner.Position;
            set => _inner.Position = value;
        }

        public override void Flush()
        {
            _inner.Flush();
        }

        public override Task FlushAsync(
            CancellationToken cancellationToken)
        {
            return _inner.FlushAsync(
                cancellationToken);
        }

        public override int Read(
            byte[] buffer,
            int offset,
            int count)
        {
            return _inner.Read(
                buffer,
                offset,
                count);
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            return _inner.ReadAsync(
                buffer,
                cancellationToken);
        }

        public override long Seek(
            long offset,
            SeekOrigin origin)
        {
            return _inner.Seek(
                offset,
                origin);
        }

        public override void SetLength(
            long value)
        {
            _inner.SetLength(value);
        }

        public override void Write(
            byte[] buffer,
            int offset,
            int count)
        {
            _inner.Write(
                buffer,
                offset,
                count);
        }

        public override void Write(
            ReadOnlySpan<byte> buffer)
        {
            _inner.Write(buffer);
        }

        public override Task WriteAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            return WriteAsync(
                buffer.AsMemory(offset, count),
                cancellationToken).AsTask();
        }

        public override async ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (!_writeAttempted)
            {
                _writeAttempted = true;

                int partialLength =
                    Math.Max(1, buffer.Length / 2);

                await _inner.WriteAsync(
                    buffer[..partialLength],
                    cancellationToken);

                throw new IOException(
                    "Injected partial candidate write failure.");
            }

            await _inner.WriteAsync(
                buffer,
                cancellationToken);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            await _inner.DisposeAsync();

            GC.SuppressFinalize(this);
        }
    }
}
