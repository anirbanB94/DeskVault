namespace DeskVault.Application.Security.Authentication;

/// <summary>
/// Represents whether an authentication provider can currently
/// perform user verification.
/// </summary>
public enum ApplicationAuthenticationAvailability
{
    /// <summary>
    /// The authentication capability is available for verification.
    /// </summary>
    Available = 0,

    /// <summary>
    /// The authentication capability cannot currently be established.
    /// </summary>
    Unavailable = 1
}
