using DeskVault.UI.Resources;
using Microsoft.Extensions.Hosting;

namespace DeskVault.UI.Tests;

public sealed class ProgramStartupTests
{
    [Fact]
    public void TryBuildHost_WhenHostConstructionFails_ShowsSafeStartupErrorAndReturnsNull()
    {
        // Arrange
        const string failureSentinel =
            "PBI190_AUTOMATIC_LOCK_CONFIGURATION_FAILURE_SENTINEL";

        int buildAttempts = 0;
        string? displayedMessage = null;
        string? displayedTitle = null;

        // Act
        IHost? host = DeskVault.UI.Program.TryBuildHost(
            () =>
            {
                buildAttempts++;

                throw new InvalidOperationException(
                    failureSentinel);
            },
            (message, title) =>
            {
                displayedMessage = message;
                displayedTitle = title;
            });

        // Assert
        Assert.Null(host);
        Assert.Equal(1, buildAttempts);

        Assert.Equal(
            UiMessages.UnableToInitializeStartupConfiguration,
            displayedMessage);

        Assert.Equal(
            UiMessages.StartupConfigurationFailedTitle,
            displayedTitle);

        Assert.DoesNotContain(
            failureSentinel,
            displayedMessage,
            StringComparison.Ordinal);

        Assert.Contains(
            "Security:AutomaticLock:Enabled",
            displayedMessage,
            StringComparison.Ordinal);

        Assert.Contains(
            "Security:AutomaticLock:InactivityTimeoutMinutes",
            displayedMessage,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TryBuildHost_WhenHostConstructionSucceeds_ReturnsHostWithoutShowingError()
    {
        // Arrange
        using IHost expectedHost =
            Host.CreateApplicationBuilder().Build();

        bool startupErrorShown = false;

        // Act
        IHost? actualHost = DeskVault.UI.Program.TryBuildHost(
            () => expectedHost,
            (_, _) => startupErrorShown = true);

        // Assert
        Assert.Same(expectedHost, actualHost);
        Assert.False(startupErrorShown);
    }
}