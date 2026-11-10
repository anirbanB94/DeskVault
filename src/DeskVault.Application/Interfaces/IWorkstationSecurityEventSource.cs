using DeskVault.Application.Security.Workstation;

namespace DeskVault.Application.Interfaces;

/// <summary>
/// Provides framework-neutral workstation security notifications.
/// Implementations isolate platform-specific notification APIs.
/// </summary>
public interface IWorkstationSecurityEventSource
{
    /// <summary>
    /// Raised when the operating system reports a workstation security event.
    /// </summary>
    event EventHandler<WorkstationSecurityEventArgs>? SecurityEventReceived;

    /// <summary>
    /// Raised when an established event source can no longer provide
    /// workstation security notifications reliably.
    /// </summary>
    event EventHandler<WorkstationSecurityEventSourceFailureEventArgs>? MonitoringFailed;

    /// <summary>
    /// Initializes native notification resources and establishes event
    /// registration. The operation must not complete successfully until
    /// the source is ready to receive notifications. After a failed start
    /// and a successful StopAsync cleanup, the same source instance must
    /// support another StartAsync attempt.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops notification delivery and releases any partially or fully
    /// initialized resources. This operation must be safe when startup did
    /// not complete and must be safe to call repeatedly.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}
