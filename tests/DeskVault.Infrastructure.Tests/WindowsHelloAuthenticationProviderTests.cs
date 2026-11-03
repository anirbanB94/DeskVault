using DeskVault.Application.Security.Authentication;
using DeskVault.Infrastructure.Security.Authentication;
using Moq;
using Windows.Security.Credentials.UI;

namespace DeskVault.Infrastructure.Tests;

public sealed class WindowsHelloAuthenticationProviderTests
{
    private static readonly IntPtr TestWindowHandle =
        new(1234);

    [Theory]
    [InlineData(
        UserConsentVerifierAvailability.Available,
        ApplicationAuthenticationAvailability.Available)]
    [InlineData(
        UserConsentVerifierAvailability.DeviceNotPresent,
        ApplicationAuthenticationAvailability.Unavailable)]
    [InlineData(
        UserConsentVerifierAvailability.NotConfiguredForUser,
        ApplicationAuthenticationAvailability.Unavailable)]
    [InlineData(
        UserConsentVerifierAvailability.DisabledByPolicy,
        ApplicationAuthenticationAvailability.Unavailable)]
    [InlineData(
        UserConsentVerifierAvailability.DeviceBusy,
        ApplicationAuthenticationAvailability.Unavailable)]
    [InlineData(
        (UserConsentVerifierAvailability)999,
        ApplicationAuthenticationAvailability.Unavailable)]
    public async Task CheckAvailabilityAsync_MapsWindowsAvailability(
        UserConsentVerifierAvailability windowsAvailability,
        ApplicationAuthenticationAvailability expectedAvailability)
    {
        // Arrange
        var platformApi =
            new Mock<IWindowsHelloPlatformApi>();

        platformApi
            .Setup(x => x.CheckAvailabilityAsync())
            .ReturnsAsync(windowsAvailability);

        var windowHandleProvider =
            new Mock<IWindowsHelloWindowHandleProvider>();

        var provider =
            CreateProvider(
                platformApi,
                windowHandleProvider);

        // Act
        ApplicationAuthenticationAvailability result =
            await provider.CheckAvailabilityAsync(
                TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            expectedAvailability,
            result);

        platformApi.Verify(
            x => x.CheckAvailabilityAsync(),
            Times.Once);

        platformApi.Verify(
            x => x.RequestVerificationForWindowAsync(
                It.IsAny<IntPtr>(),
                It.IsAny<string>()),
            Times.Never);
    }

    [Theory]
    [InlineData(
        UserConsentVerificationResult.Verified,
        ApplicationAuthenticationProviderResult.Succeeded)]
    [InlineData(
        UserConsentVerificationResult.RetriesExhausted,
        ApplicationAuthenticationProviderResult.Failed)]
    [InlineData(
        UserConsentVerificationResult.Canceled,
        ApplicationAuthenticationProviderResult.Cancelled)]
    [InlineData(
        UserConsentVerificationResult.DeviceNotPresent,
        ApplicationAuthenticationProviderResult.Unavailable)]
    [InlineData(
        UserConsentVerificationResult.NotConfiguredForUser,
        ApplicationAuthenticationProviderResult.Unavailable)]
    [InlineData(
        UserConsentVerificationResult.DisabledByPolicy,
        ApplicationAuthenticationProviderResult.Unavailable)]
    [InlineData(
        UserConsentVerificationResult.DeviceBusy,
        ApplicationAuthenticationProviderResult.Unavailable)]
    [InlineData(
        (UserConsentVerificationResult)999,
        ApplicationAuthenticationProviderResult.Unavailable)]
    public async Task RequestAuthenticationAsync_MapsWindowsVerificationResult(
        UserConsentVerificationResult windowsResult,
        ApplicationAuthenticationProviderResult expectedResult)
    {
        // Arrange
        var platformApi =
            new Mock<IWindowsHelloPlatformApi>();

        platformApi
            .Setup(x => x.RequestVerificationForWindowAsync(
                TestWindowHandle,
                WindowsHelloAuthenticationProvider.VerificationMessage))
            .ReturnsAsync(windowsResult);

        var windowHandleProvider =
            new Mock<IWindowsHelloWindowHandleProvider>();

        windowHandleProvider
            .Setup(x => x.GetWindowHandle())
            .Returns(TestWindowHandle);

        var provider =
            CreateProvider(
                platformApi,
                windowHandleProvider);

        // Act
        ApplicationAuthenticationProviderResult result =
            await provider.RequestAuthenticationAsync(
                TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            expectedResult,
            result);

        windowHandleProvider.Verify(
            x => x.GetWindowHandle(),
            Times.Once);

        platformApi.Verify(
            x => x.RequestVerificationForWindowAsync(
                TestWindowHandle,
                WindowsHelloAuthenticationProvider.VerificationMessage),
            Times.Once);
    }

    [Fact]
    public async Task RequestAuthenticationAsync_WhenWindowHandleIsZero_ReturnsUnavailableWithoutRequestingVerification()
    {
        // Arrange
        var platformApi =
            new Mock<IWindowsHelloPlatformApi>();

        var windowHandleProvider =
            new Mock<IWindowsHelloWindowHandleProvider>();

        windowHandleProvider
            .Setup(x => x.GetWindowHandle())
            .Returns(IntPtr.Zero);

        var provider =
            CreateProvider(
                platformApi,
                windowHandleProvider);

        // Act
        ApplicationAuthenticationProviderResult result =
            await provider.RequestAuthenticationAsync(
                TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            ApplicationAuthenticationProviderResult.Unavailable,
            result);

        platformApi.Verify(
            x => x.RequestVerificationForWindowAsync(
                It.IsAny<IntPtr>(),
                It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task RequestAuthenticationAsync_PassesActiveWindowHandleAndSourceControlledMessage()
    {
        // Arrange
        var platformApi =
            new Mock<IWindowsHelloPlatformApi>();

        var windowHandleProvider =
            new Mock<IWindowsHelloWindowHandleProvider>();

        windowHandleProvider
            .Setup(x => x.GetWindowHandle())
            .Returns(TestWindowHandle);

        platformApi
            .Setup(x => x.RequestVerificationForWindowAsync(
                TestWindowHandle,
                WindowsHelloAuthenticationProvider.VerificationMessage))
            .ReturnsAsync(
                UserConsentVerificationResult.Verified);

        var provider =
            CreateProvider(
                platformApi,
                windowHandleProvider);

        // Act
        ApplicationAuthenticationProviderResult result =
            await provider.RequestAuthenticationAsync(
                TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            ApplicationAuthenticationProviderResult.Succeeded,
            result);

        platformApi.Verify(
            x => x.RequestVerificationForWindowAsync(
                TestWindowHandle,
                It.Is<string>(
                    message =>
                        message ==
                        "Verify your identity to unlock DeskVault.")),
            Times.Once);
    }

    [Fact]
    public async Task CheckAvailabilityAsync_WhenCallerIsAlreadyCancelled_DoesNotCallPlatformApi()
    {
        // Arrange
        var platformApi =
            new Mock<IWindowsHelloPlatformApi>();

        var windowHandleProvider =
            new Mock<IWindowsHelloWindowHandleProvider>();

        var provider =
            CreateProvider(
                platformApi,
                windowHandleProvider);

        using var cancellationTokenSource =
            CancellationTokenSource.CreateLinkedTokenSource(
                TestContext.Current.CancellationToken);

        cancellationTokenSource.Cancel();

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                provider.CheckAvailabilityAsync(
                    cancellationTokenSource.Token));

        // Assert
        platformApi.Verify(
            x => x.CheckAvailabilityAsync(),
            Times.Never);
    }

    [Fact]
    public async Task RequestAuthenticationAsync_WhenCallerIsAlreadyCancelled_DoesNotRequestVerification()
    {
        // Arrange
        var platformApi =
            new Mock<IWindowsHelloPlatformApi>();

        var windowHandleProvider =
            new Mock<IWindowsHelloWindowHandleProvider>();

        var provider =
            CreateProvider(
                platformApi,
                windowHandleProvider);

        using var cancellationTokenSource =
            CancellationTokenSource.CreateLinkedTokenSource(
                TestContext.Current.CancellationToken);

        cancellationTokenSource.Cancel();

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                provider.RequestAuthenticationAsync(
                    cancellationTokenSource.Token));

        // Assert
        windowHandleProvider.Verify(
            x => x.GetWindowHandle(),
            Times.Never);

        platformApi.Verify(
            x => x.RequestVerificationForWindowAsync(
                It.IsAny<IntPtr>(),
                It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task RequestAuthenticationAsync_WhenPlatformApiThrows_PropagatesExceptionToApplicationBoundary()
    {
        // Arrange
        var platformApi =
            new Mock<IWindowsHelloPlatformApi>();

        var windowHandleProvider =
            new Mock<IWindowsHelloWindowHandleProvider>();

        windowHandleProvider
            .Setup(x => x.GetWindowHandle())
            .Returns(TestWindowHandle);

        platformApi
            .Setup(x => x.RequestVerificationForWindowAsync(
                TestWindowHandle,
                It.IsAny<string>()))
            .ThrowsAsync(
                new InvalidOperationException(
                    "Synthetic platform failure."));

        var provider =
            CreateProvider(
                platformApi,
                windowHandleProvider);

        // Act
        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () =>
                    provider.RequestAuthenticationAsync(
                        TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(
            "Synthetic platform failure.",
            exception.Message);

        platformApi.Verify(
            x => x.RequestVerificationForWindowAsync(
                TestWindowHandle,
                It.IsAny<string>()),
            Times.Once);
    }

    private static WindowsHelloAuthenticationProvider CreateProvider(
        Mock<IWindowsHelloPlatformApi> platformApi,
        Mock<IWindowsHelloWindowHandleProvider> windowHandleProvider)
    {
        return new WindowsHelloAuthenticationProvider(
            platformApi.Object,
            windowHandleProvider.Object);
    }
}
