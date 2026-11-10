namespace DeskVault.Application.Security.Workstation;

/// <summary>
/// Carries the failure that prevents an event source from reliably
/// providing workstation security notifications.
/// </summary>
public sealed class WorkstationSecurityEventSourceFailureEventArgs : EventArgs
{
    public WorkstationSecurityEventSourceFailureEventArgs(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Exception = exception;
    }

    /// <summary>
    /// Gets the original failure for safe diagnostic classification.
    /// Consumers must not expose its message or details to the user.
    /// </summary>
    public Exception Exception { get; }
}
