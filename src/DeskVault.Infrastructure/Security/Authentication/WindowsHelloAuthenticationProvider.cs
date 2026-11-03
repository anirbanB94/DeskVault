using DeskVault.Application.Interfaces;
using DeskVault.Application.Security.Authentication;
using Windows.Security.Credentials.UI;

namespace DeskVault.Infrastructure.Security.Authentication;

/// <summary>
/// Implements the Application authentication-provider contract
/// using Windows Hello user verification.
/// </summary>
public sealed class WindowsHelloAuthenticationProvider
    : IApplicationAuthenticationProvider
{
    /// <summary>
    /// The source-controlled message displayed by Windows during verification.
    /// </summary>
    public const string VerificationMessage =
        "Verify your identity to unlock DeskVault.";

    private readonly IWindowsHelloPlatformApi _platformApi;
    private readonly IWindowsHelloWindowHandleProvider _windowHandleProvider;

    public WindowsHelloAuthenticationProvider(
        IWindowsHelloPlatformApi platformApi,
        IWindowsHelloWindowHandleProvider windowHandleProvider)
    {
        ArgumentNullException.ThrowIfNull(platformApi);
        ArgumentNullException.ThrowIfNull(windowHandleProvider);

        _platformApi = platformApi;
        _windowHandleProvider = windowHandleProvider;
    }

    /// <inheritdoc />
    public async Task<ApplicationAuthenticationAvailability> CheckAvailabilityAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        UserConsentVerifierAvailability availability =
            await _platformApi.CheckAvailabilityAsync();

        cancellationToken.ThrowIfCancellationRequested();

        return availability switch
        {
            UserConsentVerifierAvailability.Available =>
                ApplicationAuthenticationAvailability.Available,

            _ =>
                ApplicationAuthenticationAvailability.Unavailable
        };
    }

    /// <inheritdoc />
    public async Task<ApplicationAuthenticationProviderResult> RequestAuthenticationAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IntPtr windowHandle =
            _windowHandleProvider.GetWindowHandle();

        if (windowHandle == IntPtr.Zero)
        {
            return ApplicationAuthenticationProviderResult.Unavailable;
        }

        UserConsentVerificationResult verificationResult =
            await _platformApi.RequestVerificationForWindowAsync(
                windowHandle,
                VerificationMessage);

        cancellationToken.ThrowIfCancellationRequested();

        return verificationResult switch
        {
            UserConsentVerificationResult.Verified =>
                ApplicationAuthenticationProviderResult.Succeeded,

            UserConsentVerificationResult.RetriesExhausted =>
                ApplicationAuthenticationProviderResult.Failed,

            UserConsentVerificationResult.Canceled =>
                ApplicationAuthenticationProviderResult.Cancelled,

            UserConsentVerificationResult.DeviceNotPresent =>
                ApplicationAuthenticationProviderResult.Unavailable,

            UserConsentVerificationResult.NotConfiguredForUser =>
                ApplicationAuthenticationProviderResult.Unavailable,

            UserConsentVerificationResult.DisabledByPolicy =>
                ApplicationAuthenticationProviderResult.Unavailable,

            UserConsentVerificationResult.DeviceBusy =>
                ApplicationAuthenticationProviderResult.Unavailable,

            _ =>
                ApplicationAuthenticationProviderResult.Unavailable
        };
    }
}
