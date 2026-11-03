namespace DeskVault.Application.Security.Authentication;

/// <summary>
/// Represents the outcome of the Application authentication
/// orchestration, including whether the session accepted the attempt.
/// </summary>
public enum ApplicationAuthenticationServiceResult
{
    /// <summary>
    /// Authentication succeeded and the session accepted the outcome.
    /// </summary>
    Succeeded = 0,

    /// <summary>
    /// Authentication failed and the session accepted the outcome.
    /// </summary>
    Failed = 1,

    /// <summary>
    /// Authentication was cancelled and the session accepted the outcome.
    /// </summary>
    Cancelled = 2,

    /// <summary>
    /// Authentication capability or verification was unavailable,
    /// and the session accepted the unavailable outcome.
    /// </summary>
    Unavailable = 3,

    /// <summary>
    /// Authentication could not begin because the current
    /// session state does not permit a new attempt.
    /// </summary>
    NotPermitted = 4,

    /// <summary>
    /// The session rejected the completion because the attempt
    /// was invalidated, stale, or superseded.
    /// </summary>
    Superseded = 5,

    /// <summary>
    /// Another authentication operation is already in progress
    /// through this service instance.
    /// </summary>
    AlreadyInProgress = 6
}
