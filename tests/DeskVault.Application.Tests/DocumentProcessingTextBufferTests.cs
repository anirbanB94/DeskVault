using DeskVault.Application.Documents.Processing;
using System.Text;

namespace DeskVault.Application.Tests;

public sealed class DocumentProcessingTextBufferTests
{
    [Fact]
    public void Constructor_NonPositiveLimit_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new DocumentProcessingTextBuffer(0));

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new DocumentProcessingTextBuffer(-1));
    }

    [Fact]
    public void Append_ExactLimit_Succeeds()
    {
        var buffer =
            new DocumentProcessingTextBuffer(5);

        buffer.Append("12345");

        Assert.Equal(
            5,
            buffer.CurrentBytes);

        Assert.Equal(
            "12345",
            buffer.ToString());
    }

    [Fact]
    public void Append_WhenLimitWouldBeExceeded_ThrowsResourceLimitExceededException()
    {
        var buffer =
            new DocumentProcessingTextBuffer(5);

        buffer.Append("1234");

        ResourceLimitExceededException exception =
            Assert.Throws<ResourceLimitExceededException>(
                () =>
                    buffer.Append("56"));

        Assert.Equal(
            5,
            exception.LimitBytes);

        Assert.Equal(
            6,
            exception.AttemptedBytes);

        Assert.Equal(
            4,
            buffer.CurrentBytes);

        Assert.Equal(
            "1234",
            buffer.ToString());
    }

    [Fact]
    public void Append_CumulativeWrites_UseUtf8ByteCount()
    {
        const string value =
            "é";

        var buffer =
            new DocumentProcessingTextBuffer(2);

        buffer.Append(value);

        Assert.Equal(
            Encoding.UTF8.GetByteCount(value),
            buffer.CurrentBytes);

        Assert.Equal(
            value,
            buffer.ToString());

        ResourceLimitExceededException exception =
            Assert.Throws<ResourceLimitExceededException>(
                () =>
                    buffer.Append("a"));

        Assert.Equal(
            2,
            exception.LimitBytes);

        Assert.Equal(
            3,
            exception.AttemptedBytes);

        Assert.Equal(
            value,
            buffer.ToString());
    }

    [Fact]
    public void AppendLine_ContributesNewlineToResourceBudget()
    {
        string expectedText =
            "abc" +
            Environment.NewLine;

        var buffer =
            new DocumentProcessingTextBuffer(
                Encoding.UTF8.GetByteCount(
                    expectedText));

        buffer.AppendLine("abc");

        Assert.Equal(
            Encoding.UTF8.GetByteCount(
                expectedText),
            buffer.CurrentBytes);

        Assert.Equal(
            expectedText,
            buffer.ToString());

        Assert.Throws<ResourceLimitExceededException>(
            () =>
                buffer.Append("d"));
    }

    [Fact]
    public void AppendInt_UsesInvariantRepresentation()
    {
        var buffer =
            new DocumentProcessingTextBuffer(20);

        buffer.Append(123456);

        Assert.Equal(
            6,
            buffer.CurrentBytes);

        Assert.Equal(
            "123456",
            buffer.ToString());
    }

    [Fact]
    public void Append_Span_HandlesSupplementaryUnicodeCharacterCorrectly()
    {
        const string value =
            "𐀀";

        var buffer =
            new DocumentProcessingTextBuffer(4);

        buffer.Append(
            value.AsSpan());

        Assert.Equal(
            4,
            buffer.CurrentBytes);

        Assert.Equal(
            value,
            buffer.ToString());
    }

    [Fact]
    public void Append_Span_WhenLimitWouldBeExceeded_ThrowsBeforeMutation()
    {
        const string value =
            "𐀀";

        var buffer =
            new DocumentProcessingTextBuffer(4);

        buffer.Append("abc");

        ResourceLimitExceededException exception =
            Assert.Throws<ResourceLimitExceededException>(
                () =>
                    buffer.Append(
                        value.AsSpan()));

        Assert.Equal(
            4,
            exception.LimitBytes);

        Assert.Equal(
            7,
            exception.AttemptedBytes);

        Assert.Equal(
            "abc",
            buffer.ToString());

        Assert.Equal(
            3,
            buffer.CurrentBytes);
    }
}
