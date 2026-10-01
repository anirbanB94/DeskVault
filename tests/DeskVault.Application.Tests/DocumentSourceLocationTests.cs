using DeskVault.Application.Documents.Provenance;

namespace DeskVault.Application.Tests;

public sealed class DocumentSourceLocationTests
{
    [Fact]
    public void Constructor_ValidLineRange_PreservesStartAndEndLines()
    {
        // Act
        var location =
            new DocumentSourceLocation(
                startLine: 3,
                endLine: 7);

        // Assert
        Assert.Equal(
            3,
            location.StartLine);

        Assert.Equal(
            7,
            location.EndLine);
    }

    [Fact]
    public void Constructor_SingleLine_PreservesSameStartAndEndLine()
    {
        // Act
        var location =
            new DocumentSourceLocation(
                startLine: 5,
                endLine: 5);

        // Assert
        Assert.Equal(
            5,
            location.StartLine);

        Assert.Equal(
            5,
            location.EndLine);
    }

    [Fact]
    public void Constructor_ZeroStartLine_ThrowsArgumentOutOfRangeException()
    {
        // Act
        ArgumentOutOfRangeException exception =
            Assert.Throws<ArgumentOutOfRangeException>(
                () =>
                    new DocumentSourceLocation(
                        startLine: 0,
                        endLine: 1));

        // Assert
        Assert.Equal(
            "startLine",
            exception.ParamName);
    }

    [Fact]
    public void Constructor_NegativeStartLine_ThrowsArgumentOutOfRangeException()
    {
        // Act
        ArgumentOutOfRangeException exception =
            Assert.Throws<ArgumentOutOfRangeException>(
                () =>
                    new DocumentSourceLocation(
                        startLine: -1,
                        endLine: 1));

        // Assert
        Assert.Equal(
            "startLine",
            exception.ParamName);
    }

    [Fact]
    public void Constructor_EndLineBeforeStartLine_ThrowsArgumentOutOfRangeException()
    {
        // Act
        ArgumentOutOfRangeException exception =
            Assert.Throws<ArgumentOutOfRangeException>(
                () =>
                    new DocumentSourceLocation(
                        startLine: 5,
                        endLine: 4));

        // Assert
        Assert.Equal(
            "endLine",
            exception.ParamName);
    }
}
