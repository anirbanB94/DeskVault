namespace DeskVault.Application.Interfaces;

/// <summary>
/// Accepts qualifying user activity for the current authenticated
/// application security session.
/// </summary>
public interface IApplicationUserActivityService
{
    /// <summary>
    /// Attempts to record qualifying user activity.
    /// </summary>
    /// <param name="observedSessionRevision">
    /// The session revision observed by the activity source when the
    /// input occurred.
    /// </param>
    /// <returns>
    /// True when the activity is accepted and updates the inactivity
    /// baseline; otherwise false.
    /// </returns>
    /// <remarks>
    /// Activity associated with a stale session revision, a
    /// non-authenticated session, or a session whose inactivity deadline
    /// has been reached is rejected.
    ///
    /// The caller is responsible for reporting only deliberate user
    /// activity. Passive pointer movement, background work, timers,
    /// search, indexing, and other automated work are not qualifying
    /// activity.
    /// </remarks>
    bool TryReportQualifyingActivity(long observedSessionRevision);
}
