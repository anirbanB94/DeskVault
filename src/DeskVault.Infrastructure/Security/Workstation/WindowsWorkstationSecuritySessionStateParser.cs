namespace DeskVault.Infrastructure.Security.Workstation;

/// <summary>
/// Validates the native WTS session information required to determine
/// the security state of the DeskVault process session.
/// </summary>
internal static class WindowsWorkstationSecuritySessionStateParser
{
    private const uint SupportedWtsInfoExLevel = 1;

    private const int WtsSessionStateLock = 0;

    private const int WtsSessionStateUnlock = 1;

    private const int MinimumConnectionState =
        (int)WindowsWorkstationWtsConnectionState.Active;

    private const int MaximumConnectionState =
        (int)WindowsWorkstationWtsConnectionState.Init;

    /// <summary>
    /// Validates and translates a native WTS session information snapshot.
    /// </summary>
    /// <param name="snapshot">
    /// The fields extracted from the WTSINFOEXW native response.
    /// </param>
    /// <param name="expectedStructureSize">
    /// The expected native WTSINFOEXW structure size calculated by the
    /// current process architecture's native structure layout.
    /// </param>
    /// <param name="processSessionId">
    /// The session identifier obtained for the DeskVault process through
    /// ProcessIdToSessionId.
    /// </param>
    /// <returns>A validated session-state snapshot.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The expected native structure size is invalid.
    /// </exception>
    /// <exception cref="InvalidDataException">
    /// The native response is malformed, unsupported, or does not belong
    /// to the DeskVault process session.
    /// </exception>
    public static WindowsWorkstationSecuritySessionState Parse(
        WindowsWorkstationSecuritySessionStateSnapshot snapshot,
        int expectedStructureSize,
        uint processSessionId)
    {
        if (expectedStructureSize <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expectedStructureSize),
                expectedStructureSize,
                "The expected WTS structure size must be positive.");
        }

        if (snapshot.Level != SupportedWtsInfoExLevel)
        {
            throw new InvalidDataException(
                "The WTS response contains an unsupported structure level.");
        }

        if (snapshot.StructureSize != (uint)expectedStructureSize)
        {
            throw new InvalidDataException(
                "The WTS response structure size does not match the current native layout.");
        }

        if (snapshot.SessionId != processSessionId)
        {
            throw new InvalidDataException(
                "The WTS response session does not match the DeskVault process session.");
        }

        if (snapshot.ConnectionState < MinimumConnectionState ||
            snapshot.ConnectionState > MaximumConnectionState)
        {
            throw new InvalidDataException(
                "The WTS response contains an unknown session connection state.");
        }

        WindowsWorkstationWtsSessionFlags sessionFlags =
            snapshot.SessionFlags switch
            {
                WtsSessionStateLock =>
                    WindowsWorkstationWtsSessionFlags.Lock,

                WtsSessionStateUnlock =>
                    WindowsWorkstationWtsSessionFlags.Unlock,

                _ =>
                    throw new InvalidDataException(
                        "The WTS response contains unknown session lock flags.")
            };

        WindowsWorkstationWtsConnectionState connectionState =
            (WindowsWorkstationWtsConnectionState)snapshot.ConnectionState;

        WindowsWorkstationSessionAccessState accessState =
            connectionState != WindowsWorkstationWtsConnectionState.Active
                ? WindowsWorkstationSessionAccessState.Disconnected
                : sessionFlags == WindowsWorkstationWtsSessionFlags.Lock
                    ? WindowsWorkstationSessionAccessState.Locked
                    : WindowsWorkstationSessionAccessState.Unlocked;

        return new WindowsWorkstationSecuritySessionState(
            ProcessSessionId: processSessionId,
            SessionId: snapshot.SessionId,
            ConnectionState: connectionState,
            SessionFlags: sessionFlags,
            AccessState: accessState);
    }
}
