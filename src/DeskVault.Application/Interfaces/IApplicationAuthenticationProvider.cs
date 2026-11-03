using DeskVault.Application.Security.Authentication;

namespace DeskVault.Application.Interfaces;

/// <summary>
/// Provides an authentication capability without exposing
/// platform-specific authentication APIs to the Application layer.
/// </summary>
public interface IApplicationAuthenticationProvider
{
    /// <summary>
    /// Checks whether the authentication capability is currently available.
    /// Availability does not constitute successful authentication.
    /// </summary>
    Task<ApplicationAuthenticationAvailability> CheckAvailabilityAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests fresh user verification through the configured provider.
    /// </summary>
    Task<ApplicationAuthenticationProviderResult> RequestAuthenticationAsync(
        CancellationToken cancellationToken = default);
}
