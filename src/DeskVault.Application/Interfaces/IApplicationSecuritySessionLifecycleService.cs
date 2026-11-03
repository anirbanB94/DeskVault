using DeskVault.Application.Security.Authentication;
using DeskVault.Application.Security.Sessions;

namespace DeskVault.Application.Interfaces;

/// <summary>
/// Coordinates application security-session locking and unlocking.
/// </summary>
public interface IApplicationSecuritySessionLifecycleService
{
    /// <summary>
    /// Locks the session and invalidates any in-flight unlock operation.
    /// </summary>
    ApplicationSecuritySessionSnapshot Lock();

    /// <summary>
    /// Authenticates to restore protected access.
    /// </summary>
    Task<ApplicationAuthenticationServiceResult> UnlockAsync(
        CancellationToken cancellationToken = default);
}
