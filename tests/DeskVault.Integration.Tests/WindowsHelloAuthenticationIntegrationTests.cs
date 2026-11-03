using DeskVault.Application;
using DeskVault.Application.Interfaces;
using DeskVault.Application.Security.Authentication;
using DeskVault.Application.Security.Sessions;
using DeskVault.Infrastructure;
using DeskVault.Infrastructure.Security.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Windows.Security.Credentials.UI;

namespace DeskVault.Integration.Tests;

public sealed class WindowsHelloAuthenticationIntegrationTests
{
    private static readonly IntPtr TestWindowHandle =
        new(1234);

    [Theory]
    [InlineData(
        UserConsentVerificationResult.Verified,
        ApplicationAuthenticationServiceResult.Succeeded,
        ApplicationSecuritySessionState.Authenticated,
        true)]
    [InlineData(
        UserConsentVerificationResult.RetriesExhausted,
        ApplicationAuthenticationServiceResult.Failed,
        ApplicationSecuritySessionState.Unauthenticated,
        false)]
    [InlineData(
        UserConsentVerificationResult.Canceled,
        ApplicationAuthenticationServiceResult.Cancelled,
        ApplicationSecuritySessionState.Unauthenticated,
        false)]
    [InlineData(
        UserConsentVerificationResult.DeviceNotPresent,
        ApplicationAuthenticationServiceResult.Unavailable,
        ApplicationSecuritySessionState.Unavailable,
        false)]
    [InlineData(
        UserConsentVerificationResult.NotConfiguredForUser,
        ApplicationAuthenticationServiceResult.Unavailable,
        ApplicationSecuritySessionState.Unavailable,
        false)]
    [InlineData(
        UserConsentVerificationResult.DisabledByPolicy,
        ApplicationAuthenticationServiceResult.Unavailable,
        ApplicationSecuritySessionState.Unavailable,
        false)]
    [InlineData(
        UserConsentVerificationResult.DeviceBusy,
        ApplicationAuthenticationServiceResult.Unavailable,
        ApplicationSecuritySessionState.Unavailable,
        false)]
    [InlineData(
        (UserConsentVerificationResult)999,
        ApplicationAuthenticationServiceResult.Unavailable,
        ApplicationSecuritySessionState.Unavailable,
        false)]
    public async Task AuthenticateAsync_IntegratesWindowsHelloResultWithAuthoritativeSession(
        UserConsentVerificationResult windowsResult,
        ApplicationAuthenticationServiceResult expectedServiceResult,
        ApplicationSecuritySessionState expectedSessionState,
        bool expectedProtectedAccess)
    {
        // Arrange
        var platformApi =
            new StubWindowsHelloPlatformApi(
                UserConsentVerifierAvailability.Available,
                windowsResult);

        var windowHandleProvider =
            new StubWindowsHelloWindowHandleProvider(
                TestWindowHandle);

        using ServiceProvider serviceProvider =
            CreateServiceProvider(
                platformApi,
                windowHandleProvider);

        IApplicationAuthenticationService authenticationService =
            serviceProvider.GetRequiredService<IApplicationAuthenticationService>();

        IApplicationSecuritySession session =
            serviceProvider.GetRequiredService<IApplicationSecuritySession>();

        // Act
        ApplicationAuthenticationServiceResult result =
            await authenticationService.AuthenticateAsync(
                TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            expectedServiceResult,
            result);

        Assert.Equal(
            expectedSessionState,
            session.CurrentSnapshot.State);

        Assert.Equal(
            expectedProtectedAccess,
            session.CurrentSnapshot.IsProtectedAccessAvailable);

        Assert.Equal(
            1,
            platformApi.AvailabilityRequestCount);

        Assert.Equal(
            1,
            windowHandleProvider.RequestCount);

        Assert.Equal(
            1,
            platformApi.VerificationRequestCount);

        Assert.Equal(
            TestWindowHandle,
            platformApi.LastWindowHandle);

        Assert.Equal(
            WindowsHelloAuthenticationProvider.VerificationMessage,
            platformApi.LastVerificationMessage);
    }

    [Theory]
    [InlineData(UserConsentVerifierAvailability.DeviceNotPresent)]
    [InlineData(UserConsentVerifierAvailability.NotConfiguredForUser)]
    [InlineData(UserConsentVerifierAvailability.DisabledByPolicy)]
    [InlineData(UserConsentVerifierAvailability.DeviceBusy)]
    [InlineData((UserConsentVerifierAvailability)999)]
    public async Task AuthenticateAsync_WhenWindowsHelloIsUnavailable_DoesNotRequestVerificationOrGrantAccess(
        UserConsentVerifierAvailability windowsAvailability)
    {
        // Arrange
        var platformApi =
            new StubWindowsHelloPlatformApi(
                windowsAvailability,
                UserConsentVerificationResult.Verified);

        var windowHandleProvider =
            new StubWindowsHelloWindowHandleProvider(
                TestWindowHandle);

        using ServiceProvider serviceProvider =
            CreateServiceProvider(
                platformApi,
                windowHandleProvider);

        IApplicationAuthenticationService authenticationService =
            serviceProvider.GetRequiredService<IApplicationAuthenticationService>();

        IApplicationSecuritySession session =
            serviceProvider.GetRequiredService<IApplicationSecuritySession>();

        // Act
        ApplicationAuthenticationServiceResult result =
            await authenticationService.AuthenticateAsync(
                TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            ApplicationAuthenticationServiceResult.Unavailable,
            result);

        Assert.Equal(
            ApplicationSecuritySessionState.Unavailable,
            session.CurrentSnapshot.State);

        Assert.False(
            session.CurrentSnapshot.IsProtectedAccessAvailable);

        Assert.Equal(
            1,
            platformApi.AvailabilityRequestCount);

        Assert.Equal(
            0,
            platformApi.VerificationRequestCount);

        Assert.Equal(
            0,
            windowHandleProvider.RequestCount);
    }

    [Fact]
    public async Task AuthenticateAsync_WhenWindowHandleIsUnavailable_DoesNotRequestVerificationOrGrantAccess()
    {
        // Arrange
        var platformApi =
            new StubWindowsHelloPlatformApi(
                UserConsentVerifierAvailability.Available,
                UserConsentVerificationResult.Verified);

        var windowHandleProvider =
            new StubWindowsHelloWindowHandleProvider(
                IntPtr.Zero);

        using ServiceProvider serviceProvider =
            CreateServiceProvider(
                platformApi,
                windowHandleProvider);

        IApplicationAuthenticationService authenticationService =
            serviceProvider.GetRequiredService<IApplicationAuthenticationService>();

        IApplicationSecuritySession session =
            serviceProvider.GetRequiredService<IApplicationSecuritySession>();

        // Act
        ApplicationAuthenticationServiceResult result =
            await authenticationService.AuthenticateAsync(
                TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            ApplicationAuthenticationServiceResult.Unavailable,
            result);

        Assert.Equal(
            ApplicationSecuritySessionState.Unavailable,
            session.CurrentSnapshot.State);

        Assert.False(
            session.CurrentSnapshot.IsProtectedAccessAvailable);

        Assert.Equal(
            1,
            platformApi.AvailabilityRequestCount);

        Assert.Equal(
            0,
            platformApi.VerificationRequestCount);

        Assert.Equal(
            1,
            windowHandleProvider.RequestCount);
    }

    [Fact]
    public void ProductionComposition_RegistersOnlyWindowsHelloAsAuthenticationProvider()
    {
        // Arrange
        var platformApi =
            new StubWindowsHelloPlatformApi(
                UserConsentVerifierAvailability.Available,
                UserConsentVerificationResult.Verified);

        var windowHandleProvider =
            new StubWindowsHelloWindowHandleProvider(
                TestWindowHandle);

        using ServiceProvider serviceProvider =
            CreateServiceProvider(
                platformApi,
                windowHandleProvider);

        // Act
        IApplicationAuthenticationProvider[] providers =
            serviceProvider
                .GetServices<IApplicationAuthenticationProvider>()
                .ToArray();

        IApplicationAuthenticationService firstAuthenticationService =
            serviceProvider.GetRequiredService<IApplicationAuthenticationService>();

        IApplicationAuthenticationService secondAuthenticationService =
            serviceProvider.GetRequiredService<IApplicationAuthenticationService>();

        IApplicationSecuritySession firstSession =
            serviceProvider.GetRequiredService<IApplicationSecuritySession>();

        IApplicationSecuritySession secondSession =
            serviceProvider.GetRequiredService<IApplicationSecuritySession>();

        // Assert
        IApplicationAuthenticationProvider provider =
            Assert.Single(providers);

        Assert.IsType<WindowsHelloAuthenticationProvider>(
            provider);

        Assert.IsType<ApplicationAuthenticationService>(
            firstAuthenticationService);

        Assert.Same(
            firstAuthenticationService,
            secondAuthenticationService);

        Assert.Same(
            firstSession,
            secondSession);

        Assert.Same(
            platformApi,
            serviceProvider.GetRequiredService<IWindowsHelloPlatformApi>());

        Assert.Same(
            windowHandleProvider,
            serviceProvider.GetRequiredService<IWindowsHelloWindowHandleProvider>());
    }

    private static ServiceProvider CreateServiceProvider(
        StubWindowsHelloPlatformApi platformApi,
        StubWindowsHelloWindowHandleProvider windowHandleProvider)
    {
        var services =
            new ServiceCollection();

        // The Application authentication service is registered by
        // AddApplication and receives its production logger dependency.
        services.AddSingleton<ILogger<ApplicationAuthenticationService>>(
            NullLogger<ApplicationAuthenticationService>.Instance);

        // Retain the same production registration order as the UI
        // composition root.
        services.AddApplication();

        services.AddInfrastructure(
            new ConfigurationBuilder().Build());

        // Replace only the native Windows API boundary and HWND source.
        // The real WindowsHelloAuthenticationProvider remains registered.
        services.RemoveAll<IWindowsHelloPlatformApi>();

        services.AddSingleton<IWindowsHelloPlatformApi>(
            platformApi);

        services.RemoveAll<IWindowsHelloWindowHandleProvider>();

        services.AddSingleton<IWindowsHelloWindowHandleProvider>(
            windowHandleProvider);

        return services.BuildServiceProvider();
    }

    private sealed class StubWindowsHelloPlatformApi
        : IWindowsHelloPlatformApi
    {
        private int _availabilityRequestCount;
        private int _verificationRequestCount;

        public StubWindowsHelloPlatformApi(
            UserConsentVerifierAvailability availability,
            UserConsentVerificationResult verificationResult)
        {
            Availability =
                availability;

            VerificationResult =
                verificationResult;
        }

        public UserConsentVerifierAvailability Availability { get; }

        public UserConsentVerificationResult VerificationResult { get; }

        public int AvailabilityRequestCount =>
            Volatile.Read(ref _availabilityRequestCount);

        public int VerificationRequestCount =>
            Volatile.Read(ref _verificationRequestCount);

        public IntPtr LastWindowHandle { get; private set; }

        public string? LastVerificationMessage { get; private set; }

        public Task<UserConsentVerifierAvailability> CheckAvailabilityAsync()
        {
            Interlocked.Increment(
                ref _availabilityRequestCount);

            return Task.FromResult(
                Availability);
        }

        public Task<UserConsentVerificationResult> RequestVerificationForWindowAsync(
            IntPtr windowHandle,
            string message)
        {
            Interlocked.Increment(
                ref _verificationRequestCount);

            LastWindowHandle =
                windowHandle;

            LastVerificationMessage =
                message;

            return Task.FromResult(
                VerificationResult);
        }
    }

    private sealed class StubWindowsHelloWindowHandleProvider
        : IWindowsHelloWindowHandleProvider
    {
        private int _requestCount;

        public StubWindowsHelloWindowHandleProvider(
            IntPtr windowHandle)
        {
            WindowHandle =
                windowHandle;
        }

        public IntPtr WindowHandle { get; }

        public int RequestCount =>
            Volatile.Read(ref _requestCount);

        public IntPtr GetWindowHandle()
        {
            Interlocked.Increment(
                ref _requestCount);

            return WindowHandle;
        }
    }
}
