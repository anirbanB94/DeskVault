
namespace DeskVault.Application.Security.Sessions;

/// <summary>
/// Represents the outcome of an authentication operation.
/// </summary>
public enum ApplicationAuthenticationOutcome
{
    /// <summary>
    /// Authentication completed successfully.
    /// </summary>
    Succeeded = 0,

    /// <summary>
    /// Authentication was attempted but failed.
    /// </summary>
    Failed = 1,

    /// <summary>
    /// Authentication was cancelled.
    /// </summary>
    Cancelled = 2,

    /// <summary>
    /// Authentication could not be performed because the required
    /// authentication capability was unavailable.
    /// </summary>
    Unavailable = 3
}
