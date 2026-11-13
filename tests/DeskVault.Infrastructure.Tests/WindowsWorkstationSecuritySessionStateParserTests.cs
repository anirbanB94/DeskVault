using DeskVault.Infrastructure.Security.Workstation;

namespace DeskVault.Infrastructure.Tests;

public sealed class WindowsWorkstationSecuritySessionStateParserTests
{
    private const uint ProcessSessionId = 42;

    // The parser receives the expected size from the native ABI layout
    // calculated by the production native API. This fixture value is
    // deliberately independent of a particular machine architecture.
    private const int ExpectedStructureSize = 256;

    [Fact]
    public void Parse_WhenSessionIsActiveAndUnlocked_ReturnsUnlockedState()
    {
        // Arrange
        WindowsWorkstationSecuritySessionStateSnapshot snapshot =
            CreateSnapshot(
                connectionState:
                    (int)WindowsWorkstationWtsConnectionState.Active,
                sessionFlags:
                    (int)WindowsWorkstationWtsSessionFlags.Unlock);

        // Act
        WindowsWorkstationSecuritySessionState result =
            WindowsWorkstationSecuritySessionStateParser.Parse(
                snapshot,
                ExpectedStructureSize,
                ProcessSessionId);

        // Assert
        Assert.Equal(ProcessSessionId, result.ProcessSessionId);
        Assert.Equal(ProcessSessionId, result.SessionId);

        Assert.Equal(
            WindowsWorkstationWtsConnectionState.Active,
            result.ConnectionState);

        Assert.Equal(
            WindowsWorkstationWtsSessionFlags.Unlock,
            result.SessionFlags);

        Assert.Equal(
            WindowsWorkstationSessionAccessState.Unlocked,
            result.AccessState);

        Assert.True(result.IsUnlocked);
    }

    [Fact]
    public void Parse_WhenSessionIsActiveAndLocked_ReturnsLockedState()
    {
        // Arrange
        WindowsWorkstationSecuritySessionStateSnapshot snapshot =
            CreateSnapshot(
                connectionState:
                    (int)WindowsWorkstationWtsConnectionState.Active,
                sessionFlags:
                    (int)WindowsWorkstationWtsSessionFlags.Lock);

        // Act
        WindowsWorkstationSecuritySessionState result =
            WindowsWorkstationSecuritySessionStateParser.Parse(
                snapshot,
                ExpectedStructureSize,
                ProcessSessionId);

        // Assert
        Assert.Equal(
            WindowsWorkstationSessionAccessState.Locked,
            result.AccessState);

        Assert.False(result.IsUnlocked);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    public void Parse_WhenSessionIsNotActive_ReturnsDisconnectedState(
        int connectionState)
    {
        // Arrange
        WindowsWorkstationSecuritySessionStateSnapshot snapshot =
            CreateSnapshot(
                connectionState: connectionState,
                sessionFlags:
                    (int)WindowsWorkstationWtsSessionFlags.Unlock);

        // Act
        WindowsWorkstationSecuritySessionState result =
            WindowsWorkstationSecuritySessionStateParser.Parse(
                snapshot,
                ExpectedStructureSize,
                ProcessSessionId);

        // Assert
        Assert.Equal(
            WindowsWorkstationSessionAccessState.Disconnected,
            result.AccessState);

        Assert.False(result.IsUnlocked);
    }

    [Fact]
    public void Parse_WhenWtsStructureLevelIsUnsupported_ThrowsInvalidDataException()
    {
        // Arrange
        WindowsWorkstationSecuritySessionStateSnapshot snapshot =
            CreateSnapshot(level: 2);

        // Act
        InvalidDataException exception =
            Assert.Throws<InvalidDataException>(
                () => WindowsWorkstationSecuritySessionStateParser.Parse(
                    snapshot,
                    ExpectedStructureSize,
                    ProcessSessionId));

        // Assert
        Assert.Contains("structure level", exception.Message);
    }

    [Theory]
    [InlineData(255)]
    [InlineData(257)]
    public void Parse_WhenWtsStructureSizeDoesNotMatchExpectedSize_ThrowsInvalidDataException(
        uint returnedSize)
    {
        // Arrange
        WindowsWorkstationSecuritySessionStateSnapshot snapshot =
            CreateSnapshot(structureSize: returnedSize);

        // Act
        InvalidDataException exception =
            Assert.Throws<InvalidDataException>(
                () => WindowsWorkstationSecuritySessionStateParser.Parse(
                    snapshot,
                    ExpectedStructureSize,
                    ProcessSessionId));

        // Assert
        Assert.Contains("structure size", exception.Message);
    }

    [Fact]
    public void Parse_WhenProcessSessionAndWtsSessionDiffer_ThrowsInvalidDataException()
    {
        // Arrange
        WindowsWorkstationSecuritySessionStateSnapshot snapshot =
            CreateSnapshot(sessionId: ProcessSessionId + 1);

        // Act
        InvalidDataException exception =
            Assert.Throws<InvalidDataException>(
                () => WindowsWorkstationSecuritySessionStateParser.Parse(
                    snapshot,
                    ExpectedStructureSize,
                    ProcessSessionId));

        // Assert
        Assert.Contains("process session", exception.Message);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10)]
    [InlineData(100)]
    public void Parse_WhenConnectionStateIsUnknown_ThrowsInvalidDataException(
        int connectionState)
    {
        // Arrange
        WindowsWorkstationSecuritySessionStateSnapshot snapshot =
            CreateSnapshot(connectionState: connectionState);

        // Act
        InvalidDataException exception =
            Assert.Throws<InvalidDataException>(
                () => WindowsWorkstationSecuritySessionStateParser.Parse(
                    snapshot,
                    ExpectedStructureSize,
                    ProcessSessionId));

        // Assert
        Assert.Contains("connection state", exception.Message);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(int.MaxValue)]
    public void Parse_WhenSessionFlagsAreUnknown_ThrowsInvalidDataException(
        int sessionFlags)
    {
        // Arrange
        WindowsWorkstationSecuritySessionStateSnapshot snapshot =
            CreateSnapshot(sessionFlags: sessionFlags);

        // Act
        InvalidDataException exception =
            Assert.Throws<InvalidDataException>(
                () => WindowsWorkstationSecuritySessionStateParser.Parse(
                    snapshot,
                    ExpectedStructureSize,
                    ProcessSessionId));

        // Assert
        Assert.Contains("lock flags", exception.Message);
    }

    [Fact]
    public void Parse_WhenExpectedStructureSizeIsNotPositive_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        WindowsWorkstationSecuritySessionStateSnapshot snapshot =
            CreateSnapshot();

        // Act
        ArgumentOutOfRangeException exception =
            Assert.Throws<ArgumentOutOfRangeException>(
                () => WindowsWorkstationSecuritySessionStateParser.Parse(
                    snapshot,
                    expectedStructureSize: 0,
                    ProcessSessionId));

        // Assert
        Assert.Equal("expectedStructureSize", exception.ParamName);
    }

    private static WindowsWorkstationSecuritySessionStateSnapshot CreateSnapshot(
        uint level = 1,
        uint structureSize = ExpectedStructureSize,
        uint sessionId = ProcessSessionId,
        int connectionState =
            (int)WindowsWorkstationWtsConnectionState.Active,
        int sessionFlags =
            (int)WindowsWorkstationWtsSessionFlags.Unlock)
    {
        return new WindowsWorkstationSecuritySessionStateSnapshot(
            Level: level,
            StructureSize: structureSize,
            SessionId: sessionId,
            ConnectionState: connectionState,
            SessionFlags: sessionFlags);
    }
}
