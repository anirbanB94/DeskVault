using DeskVault.Application.Security.Authentication;

namespace DeskVault.Application.Interfaces;

/// <summary>
/// Coordinates authentication-provider operations with the
/// authoritative Application security session.
/// </summary>
public interface IApplicationAuthenticationService
{
    /// <summary>
    /// Checks authentication availability and, when permitted,
    /// performs a session-bound authentication attempt.
    /// </summary>
    /// <param name="cancellationToken">
    /// Cancels the caller's wait and invalidates the active
    /// authentication attempt when cancellation is requested.
    /// </param>
    /// <returns>
    /// The result of the authentication operation and its acceptance
    /// by the authoritative security session.
    /// </returns>
    Task<ApplicationAuthenticationServiceResult> AuthenticateAsync(
        CancellationToken cancellationToken = default);
}
