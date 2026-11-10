namespace DeskVault.Application.Security.Workstation;

/// <summary>
/// Provides an immutable snapshot of workstation security-monitor readiness.
/// </summary>
public sealed class WorkstationSecurityReadinessSnapshot
{
    public WorkstationSecurityReadinessSnapshot(
        WorkstationSecurityReadinessState state,
        long revision)
    {
        if (!Enum.IsDefined(typeof(WorkstationSecurityReadinessState), state))
        {
            throw new ArgumentOutOfRangeException(
                nameof(state),
                "Workstation security readiness state is invalid.");
        }

        if (revision < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(revision),
                "Workstation security readiness revision cannot be negative.");
        }

        State = state;
        Revision = revision;
    }

    /// <summary>Gets the current readiness state.</summary>
    public WorkstationSecurityReadinessState State { get; }

    /// <summary>Gets the monotonically increasing readiness revision.</summary>
    public long Revision { get; }

    /// <summary>Gets whether workstation monitoring currently permits authentication.</summary>
    public bool IsReady => State == WorkstationSecurityReadinessState.Ready;
}
