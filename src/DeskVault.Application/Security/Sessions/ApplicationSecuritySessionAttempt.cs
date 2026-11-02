
namespace DeskVault.Application.Security.Sessions;

/// <summary>
/// Represents an opaque, session-bound authentication operation token.
///
/// The token identifies the session revision and non-authorized state
/// from which an authentication operation originated. It allows the
/// session coordinator to reject stale or out-of-order completions.
/// </summary>
public sealed class ApplicationSecuritySessionAttempt
{
    internal ApplicationSecuritySessionAttempt(
        Guid sessionId,
        long revision,
        ApplicationSecuritySessionState previousState)
    {
        if (sessionId == Guid.Empty)
        {
            throw new ArgumentException(
                "A valid security-session identifier is required.",
                nameof(sessionId));
        }

        if (revision <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(revision),
                "Security-session attempt revision must be positive.");
        }

        if (!Enum.IsDefined(
                typeof(ApplicationSecuritySessionState),
                previousState))
        {
            throw new ArgumentOutOfRangeException(
                nameof(previousState),
                "Previous security-session state is invalid.");
        }

        if (previousState is not (
            ApplicationSecuritySessionState.Unauthenticated or
            ApplicationSecuritySessionState.Locked))
        {
            throw new ArgumentException(
                "Authentication must start from an unauthenticated or locked state.",
                nameof(previousState));
        }

        SessionId = sessionId;
        Revision = revision;
        PreviousState = previousState;
    }

    /// <summary>
    /// Gets the identifier of the security session that created this attempt.
    /// </summary>
    internal Guid SessionId { get; }

    /// <summary>
    /// Gets the revision associated with this authentication attempt.
    /// </summary>
    public long Revision { get; }

    /// <summary>
    /// Gets the non-authorized state from which authentication began.
    /// </summary>
    internal ApplicationSecuritySessionState PreviousState { get; }
}
