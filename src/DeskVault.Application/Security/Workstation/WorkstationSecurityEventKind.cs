namespace DeskVault.Application.Security.Workstation;

/// <summary>
/// Describes a framework-neutral workstation security notification.
/// </summary>
public enum WorkstationSecurityEventKind
{
    /// <summary>The current Windows session was locked.</summary>
    SessionLocked = 0,

    /// <summary>The current Windows session was unlocked.</summary>
    SessionUnlocked = 1,

    /// <summary>The current Windows session was logged off.</summary>
    SessionLoggedOff = 2,

    /// <summary>A console or remote session connected.</summary>
    SessionConnected = 3,

    /// <summary>A console or remote session disconnected.</summary>
    SessionDisconnected = 4,

    /// <summary>The workstation is entering a suspend or hibernate transition.</summary>
    SystemSuspending = 5,

    /// <summary>The workstation resumed from a suspend or hibernate transition.</summary>
    SystemResumed = 6
}
