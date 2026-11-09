using DeskVault.Application.Security.Authentication;
using DeskVault.Application.Security.Sessions;

namespace DeskVault.Application.Interfaces;

/// <summary>
/// Coordinates application security-session locking and unlocking.
/// </summary>
public interface IApplicationSecuritySessionLifecycleService
{
    /// <summary>
    /// Attempts to lock only the specified authenticated session revision.
    /// A stale revision does not supersede a newer unlock operation.
    /// </summary>
    /// <param name="expectedRevision">
    /// The authenticated session revision whose inactivity deadline expired.
    /// </param>
    /// <param name="snapshot">
    /// The resulting locked snapshot, or the current snapshot when the
    /// expected revision is no longer current.
    /// </param>
    /// <returns>
    /// True when the expected authenticated revision was locked;
    /// otherwise false without changing lifecycle generation or
    /// cancelling an unlock operation.
    /// </returns>
    bool TryLock(
        long expectedRevision,
        out ApplicationSecuritySessionSnapshot snapshot);

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
