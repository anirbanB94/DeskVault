
namespace DeskVault.Application.Security.Sessions;

/// <summary>
/// Represents the authoritative authorization state of the
/// application's protected vault session.
/// </summary>
public enum ApplicationSecuritySessionState
{
    /// <summary>
    /// No successful authentication has been established.
    /// </summary>
    Unauthenticated = 0,

    /// <summary>
    /// An authentication or re-authentication operation is in progress.
    /// Protected access remains unavailable.
    /// </summary>
    Authenticating = 1,

    /// <summary>
    /// Successful authentication has established protected vault access.
    /// </summary>
    Authenticated = 2,

    /// <summary>
    /// Protected vault access has been explicitly withdrawn.
    /// Re-authentication is required.
    /// </summary>
    Locked = 3,

    /// <summary>
    /// A required security capability or condition cannot be established
    /// reliably. Protected access must remain unavailable.
    /// </summary>
    Unavailable = 4
}
