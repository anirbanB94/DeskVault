using Windows.Security.Credentials.UI;

namespace DeskVault.Infrastructure.Security.Authentication;

/// <summary>
/// Isolates the Windows Hello WinRT APIs so that authentication
/// result mapping can be tested without invoking the native UI.
/// </summary>
public interface IWindowsHelloPlatformApi
{
    /// <summary>
    /// Checks whether Windows user verification is currently available.
    /// </summary>
    Task<UserConsentVerifierAvailability> CheckAvailabilityAsync();

    /// <summary>
    /// Requests Windows user verification for the specified application window.
    /// </summary>
    Task<UserConsentVerificationResult> RequestVerificationForWindowAsync(
        IntPtr windowHandle,
        string message);
}
