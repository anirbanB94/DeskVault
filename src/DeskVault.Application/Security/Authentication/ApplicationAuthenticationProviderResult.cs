namespace DeskVault.Application.Security.Authentication;

/// <summary>
/// Represents the result returned by an authentication provider.
/// </summary>
public enum ApplicationAuthenticationProviderResult
{
    /// <summary>
    /// The provider successfully verified the user.
    /// </summary>
    Succeeded = 0,

    /// <summary>
    /// The provider attempted verification but it failed.
    /// </summary>
    Failed = 1,

    /// <summary>
    /// The user cancelled the provider's verification request.
    /// </summary>
    Cancelled = 2,

    /// <summary>
    /// The provider could not complete verification because
    /// its required capability was unavailable.
    /// </summary>
    Unavailable = 3
}
