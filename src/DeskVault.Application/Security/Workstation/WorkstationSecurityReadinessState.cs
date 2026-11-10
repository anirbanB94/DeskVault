namespace DeskVault.Application.Security.Workstation;

/// <summary>
/// Represents the lifecycle state of required workstation security monitoring.
/// Only Ready permits protected application authentication.
/// </summary>
public enum WorkstationSecurityReadinessState
{
    /// <summary>Monitoring has not yet been initialized.</summary>
    NotStarted = 0,

    /// <summary>Native registration or monitor initialization is in progress.</summary>
    Starting = 1,

    /// <summary>Required workstation security notifications are registered.</summary>
    Ready = 2,

    /// <summary>Monitoring is unavailable or a required capability failed.</summary>
    Unavailable = 3,

    /// <summary>Monitoring has been stopped and cannot be restarted.</summary>
    Stopped = 4
}
