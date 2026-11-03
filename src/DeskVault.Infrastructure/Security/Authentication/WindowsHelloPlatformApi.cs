using Windows.Security.Credentials.UI;

namespace DeskVault.Infrastructure.Security.Authentication;

/// <summary>
/// Provides the concrete Windows Hello desktop API implementation.
/// </summary>
public sealed class WindowsHelloPlatformApi : IWindowsHelloPlatformApi
{
    /// <inheritdoc />
    public async Task<UserConsentVerifierAvailability> CheckAvailabilityAsync()
    {
        return await UserConsentVerifier.CheckAvailabilityAsync();
    }

    /// <inheritdoc />
    public async Task<UserConsentVerificationResult> RequestVerificationForWindowAsync(
        IntPtr windowHandle,
        string message)
    {
        if (windowHandle == IntPtr.Zero)
        {
            throw new ArgumentException(
                "A valid application window handle is required.",
                nameof(windowHandle));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return await UserConsentVerifierInterop.RequestVerificationForWindowAsync(
            windowHandle,
            message);
    }
}
