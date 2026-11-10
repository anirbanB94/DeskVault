namespace DeskVault.Application.Security.Workstation;

/// <summary>
/// Carries a framework-neutral workstation security notification.
/// </summary>
public sealed class WorkstationSecurityEventArgs : EventArgs
{
    public WorkstationSecurityEventArgs(WorkstationSecurityEventKind kind)
    {
        if (!Enum.IsDefined(typeof(WorkstationSecurityEventKind), kind))
        {
            throw new ArgumentOutOfRangeException(
                nameof(kind),
                "Workstation security event kind is invalid.");
        }

        Kind = kind;
    }

    /// <summary>Gets the normalized workstation security event kind.</summary>
    public WorkstationSecurityEventKind Kind { get; }
}
