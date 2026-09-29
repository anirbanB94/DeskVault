using System.Globalization;
using System.Text;

namespace DeskVault.Application.Documents.Processing;

public sealed class DocumentProcessingTextBuffer
{
    private static readonly Encoding Utf8 = Encoding.UTF8;

    private readonly long _maxBytes;
    private readonly StringBuilder _builder;
    private long _currentBytes;

    public DocumentProcessingTextBuffer(
        long maxBytes)
    {
        if (maxBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxBytes),
                "Maximum processed text size must be greater than zero.");
        }

        _maxBytes = maxBytes;
        _builder = new StringBuilder();
    }

    public long CurrentBytes =>
        _currentBytes;

    public int Length =>
        _builder.Length;

    public void Append(
        string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (value.Length == 0)
        {
            return;
        }

        int appendBytes =
            Utf8.GetByteCount(value);

        EnsureCapacity(
            appendBytes);

        _builder.Append(value);
        _currentBytes += appendBytes;
    }

    public void Append(
        ReadOnlySpan<char> value)
    {
        if (value.IsEmpty)
        {
            return;
        }

        int appendBytes =
            Utf8.GetByteCount(value);

        EnsureCapacity(
            appendBytes);

        _builder.Append(value);
        _currentBytes += appendBytes;
    }

    public void Append(
        char value)
    {
        Span<char> buffer =
            stackalloc char[1];

        buffer[0] = value;

        int appendBytes =
            Utf8.GetByteCount(buffer);

        EnsureCapacity(
            appendBytes);

        _builder.Append(value);
        _currentBytes += appendBytes;
    }

    public void Append(
        int value)
    {
        Append(value.ToString(CultureInfo.InvariantCulture));
    }

    public void AppendLine()
    {
        Append(Environment.NewLine);
    }

    public void AppendLine(
        string value)
    {
        Append(value);
        Append(Environment.NewLine);
    }

    public override string ToString()
    {
        return _builder.ToString();
    }

    private void EnsureCapacity(
        int appendBytes)
    {
        if (_currentBytes >
            _maxBytes - appendBytes)
        {
            throw new ResourceLimitExceededException(
                _maxBytes,
                _currentBytes + appendBytes);
        }
    }
}
