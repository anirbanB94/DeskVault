
namespace DeskVault.Application.Security.Sessions;

/// <summary>
/// Provides an immutable snapshot of the authoritative application
/// security-session state.
/// </summary>
public sealed class ApplicationSecuritySessionSnapshot
{
    public ApplicationSecuritySessionSnapshot(
        ApplicationSecuritySessionState state,
        long revision)
    {
        if (!Enum.IsDefined(
                typeof(ApplicationSecuritySessionState),
                state))
        {
            throw new ArgumentOutOfRangeException(
                nameof(state),
                "Application security-session state is invalid.");
        }

        if (revision < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(revision),
                "Security-session revision cannot be negative.");
        }

        State = state;
        Revision = revision;
    }

    /// <summary>
    /// Gets the current security-session state.
    /// </summary>
    public ApplicationSecuritySessionState State { get; }

    /// <summary>
    /// Gets the monotonically increasing revision associated with
    /// the observed session state.
    /// </summary>
    public long Revision { get; }

    /// <summary>
    /// Gets whether protected vault access is currently authorized.
    /// Authorization is granted exclusively in the Authenticated state.
    /// </summary>
    public bool IsProtectedAccessAvailable =>
        State == ApplicationSecuritySessionState.Authenticated;
}
