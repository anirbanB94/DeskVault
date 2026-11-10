namespace DeskVault.Application.Interfaces;

/// <summary>
/// Owns the Application-level lifecycle that coordinates workstation
/// security notifications with the authoritative application session.
/// </summary>
public interface IWorkstationSecurityMonitor : IWorkstationSecurityReadiness
{
    /// <summary>
    /// Attempts to initialize workstation monitoring. A known initialization
    /// failure leaves readiness unavailable and returns false so the host
    /// can retry without permitting authentication.
    /// </summary>
    Task<bool> TryStartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops workstation monitoring and transitions readiness to Stopped.
    /// The monitor detaches its event handlers and stops the underlying event
    /// source. This operation does not lock, unlock, or otherwise transition
    /// the authoritative application security session. An event-source
    /// cleanup failure is propagated so callers can observe it and retry.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}
