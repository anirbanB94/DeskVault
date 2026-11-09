namespace DeskVault.Application.Interfaces;

/// <summary>
/// Coordinates automatic inactivity locking for the authoritative
/// application security session.
/// </summary>
public interface IApplicationAutomaticLockService
{
    /// <summary>
    /// Starts automatic-lock monitoring and establishes a fresh baseline
    /// when the current session is authenticated.
    /// </summary>
    void Start();

    /// <summary>
    /// Evaluates the current authenticated session against its inactivity
    /// deadline.
    /// </summary>
    void EvaluateInactivity();

    /// <summary>
    /// Stops monitoring and discards the current inactivity baseline.
    /// </summary>
    void Stop();
}
