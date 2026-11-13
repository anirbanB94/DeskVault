namespace DeskVault.Infrastructure.Security.Workstation;

/// <summary>
/// Represents the connection states returned by the Windows
/// Remote Desktop Services WTS API.
/// </summary>
internal enum WindowsWorkstationWtsConnectionState : int
{
    Active = 0,
    Connected = 1,
    ConnectQuery = 2,
    Shadow = 3,
    Disconnected = 4,
    Idle = 5,
    Listen = 6,
    Reset = 7,
    Down = 8,
    Init = 9
}

/// <summary>
/// Represents the session lock flags returned by WTSSessionInfoEx.
/// </summary>
internal enum WindowsWorkstationWtsSessionFlags : int
{
    Lock = 0,
    Unlock = 1
}

/// <summary>
/// Represents the security interpretation of the DeskVault process session.
/// </summary>
internal enum WindowsWorkstationSessionAccessState
{
    Unlocked,
    Locked,
    Disconnected
}

/// <summary>
/// Contains the validated fields extracted from a native WTSINFOEXW
/// response before it is interpreted as a workstation security state.
/// </summary>
internal readonly record struct WindowsWorkstationSecuritySessionStateSnapshot(
    uint Level,
    uint StructureSize,
    uint SessionId,
    int ConnectionState,
    int SessionFlags);

/// <summary>
/// Represents a validated snapshot of the Windows session that owns
/// the DeskVault process.
/// </summary>
/// <remarks>
/// ProcessSessionId and SessionId are equal by construction of the
/// validation parser. Both are retained to make the session identity
/// explicit to downstream monitoring and diagnostic code.
/// </remarks>
internal sealed record WindowsWorkstationSecuritySessionState(
    uint ProcessSessionId,
    uint SessionId,
    WindowsWorkstationWtsConnectionState ConnectionState,
    WindowsWorkstationWtsSessionFlags SessionFlags,
    WindowsWorkstationSessionAccessState AccessState)
{
    /// <summary>
    /// Gets whether the validated process session is actively unlocked.
    /// This value is observational and must never authenticate or unlock
    /// the DeskVault application.
    /// </summary>
    public bool IsUnlocked =>
        AccessState == WindowsWorkstationSessionAccessState.Unlocked;
}
