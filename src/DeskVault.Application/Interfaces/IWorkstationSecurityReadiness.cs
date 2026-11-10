using DeskVault.Application.Security.Workstation;

namespace DeskVault.Application.Interfaces;

/// <summary>
/// Exposes whether the required workstation security monitoring capability
/// is currently ready for protected application authentication.
/// </summary>
public interface IWorkstationSecurityReadiness
{
    /// <summary>
    /// Gets the latest immutable workstation-monitor readiness snapshot.
    /// </summary>
    WorkstationSecurityReadinessSnapshot CurrentSnapshot { get; }

    /// <summary>
    /// Raised whenever the readiness state or its revision changes.
    /// </summary>
    event Action<WorkstationSecurityReadinessSnapshot>? ReadinessChanged;

    /// <summary>
    /// Restores an Unavailable application security session to Locked only
    /// when the supplied readiness revision is still the current Ready
    /// revision. This operation never authenticates the user.
    /// </summary>
    /// <param name="expectedReadinessRevision">
    /// The readiness revision observed before the authentication attempt.
    /// </param>
    /// <returns>
    /// True when the required monitoring capability remains ready for the
    /// supplied revision; false when readiness changed or is not Ready.
    /// </returns>
    bool TryRestoreSessionAvailability(long expectedReadinessRevision);
}
