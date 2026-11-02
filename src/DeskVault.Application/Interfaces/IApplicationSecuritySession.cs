using DeskVault.Application.Security.Sessions;

namespace DeskVault.Application.Interfaces;

/// <summary>
/// Exposes the authoritative runtime security session for the vault.
/// </summary>
public interface IApplicationSecuritySession
{
    /// <summary>
    /// Gets the latest committed session snapshot.
    /// </summary>
    ApplicationSecuritySessionSnapshot CurrentSnapshot { get; }

    /// <summary>
    /// Raised for each committed state transition, in revision order.
    /// </summary>
    /// <remarks>
    /// Handlers run synchronously on the thread draining the notification queue.
    /// No synchronization context is captured; UI consumers must marshal updates.
    /// A delivered snapshot may already be older than <see cref="CurrentSnapshot"/>;
    /// compare its revision before applying it.
    /// </remarks>
    event Action<ApplicationSecuritySessionSnapshot>? StateChanged;

    /// <summary>
    /// Starts authentication from Unauthenticated or re-authentication from Locked.
    /// </summary>
    /// <returns>
    /// The token required to complete this authentication attempt.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// The current state does not allow authentication.
    /// </exception>
    ApplicationSecuritySessionAttempt BeginAuthentication();

    /// <summary>
    /// Applies an outcome only for the active authentication attempt.
    /// </summary>
    /// <returns>
    /// True when accepted, including accepted failure or cancellation outcomes;
    /// false when the attempt is stale, foreign, completed, or invalidated.
    /// </returns>
    bool TryCompleteAuthentication(
        ApplicationSecuritySessionAttempt attempt,
        ApplicationAuthenticationOutcome outcome);

    /// <summary>
    /// Withdraws protected access and invalidates any active attempt.
    /// </summary>
    /// <remarks>
    /// Idempotent. Does not clear an Unavailable state.
    /// </remarks>
    ApplicationSecuritySessionSnapshot Lock();

    /// <summary>
    /// Fails closed and invalidates any active authentication attempt.
    /// </summary>
    /// <remarks>Idempotent.</remarks>
    ApplicationSecuritySessionSnapshot MarkUnavailable();

    /// <summary>
    /// Moves Unavailable to Locked after the required security capability recovers.
    /// </summary>
    /// <remarks>
    /// Does not grant access; successful re-authentication is still required.
    /// </remarks>
    ApplicationSecuritySessionSnapshot RestoreAvailability();
}
