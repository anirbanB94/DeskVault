
using DeskVault.Application.Security.Sessions;

namespace DeskVault.Application.Tests;

public sealed class ApplicationSecuritySessionSnapshotTests
{
    [Fact]
    public void Constructor_WhenRevisionIsZero_PreservesInitialUnauthenticatedState()
    {
        // Arrange
        const long revision = 0;

        // Act
        var snapshot =
            new ApplicationSecuritySessionSnapshot(
                ApplicationSecuritySessionState.Unauthenticated,
                revision);

        // Assert
        Assert.Equal(
            ApplicationSecuritySessionState.Unauthenticated,
            snapshot.State);

        Assert.Equal(
            revision,
            snapshot.Revision);

        Assert.False(
            snapshot.IsProtectedAccessAvailable);
    }

    [Fact]
    public void Constructor_WhenStateIsAuthenticated_AllowsProtectedAccess()
    {
        // Arrange
        const long revision = 5;

        // Act
        var snapshot =
            new ApplicationSecuritySessionSnapshot(
                ApplicationSecuritySessionState.Authenticated,
                revision);

        // Assert
        Assert.Equal(
            ApplicationSecuritySessionState.Authenticated,
            snapshot.State);

        Assert.Equal(
            revision,
            snapshot.Revision);

        Assert.True(
            snapshot.IsProtectedAccessAvailable);
    }

    [Theory]
    [InlineData(ApplicationSecuritySessionState.Unauthenticated)]
    [InlineData(ApplicationSecuritySessionState.Authenticating)]
    [InlineData(ApplicationSecuritySessionState.Locked)]
    [InlineData(ApplicationSecuritySessionState.Unavailable)]
    public void IsProtectedAccessAvailable_WhenSessionIsNotAuthenticated_ReturnsFalse(
        ApplicationSecuritySessionState state)
    {
        // Arrange
        var snapshot =
            new ApplicationSecuritySessionSnapshot(
                state,
                revision: 1);

        // Act
        bool isProtectedAccessAvailable =
            snapshot.IsProtectedAccessAvailable;

        // Assert
        Assert.False(
            isProtectedAccessAvailable);
    }

    [Fact]
    public void Constructor_WhenStateIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var invalidState =
            (ApplicationSecuritySessionState)999;

        // Act
        ArgumentOutOfRangeException exception =
            Assert.Throws<ArgumentOutOfRangeException>(
                () =>
                    new ApplicationSecuritySessionSnapshot(
                        invalidState,
                        revision: 0));

        // Assert
        Assert.Equal(
            "state",
            exception.ParamName);
    }

    [Fact]
    public void Constructor_WhenRevisionIsNegative_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        const long invalidRevision = -1;

        // Act
        ArgumentOutOfRangeException exception =
            Assert.Throws<ArgumentOutOfRangeException>(
                () =>
                    new ApplicationSecuritySessionSnapshot(
                        ApplicationSecuritySessionState.Unauthenticated,
                        invalidRevision));

        // Assert
        Assert.Equal(
            "revision",
            exception.ParamName);
    }
}
