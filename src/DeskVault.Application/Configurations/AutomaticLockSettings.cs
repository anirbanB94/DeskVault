namespace DeskVault.Application.Configurations;

/// <summary>
/// Immutable, validated automatic-lock settings for one application process.
/// </summary>
public sealed class AutomaticLockSettings
{
    public const int MinimumInactivityTimeoutMinutes = 1;

    public const int MaximumInactivityTimeoutMinutes = 1440;

    public static AutomaticLockSettings Disabled { get; } =
        new(enabled: false, inactivityTimeoutMinutes: null);

    public AutomaticLockSettings(
        bool enabled,
        int? inactivityTimeoutMinutes)
    {
        if (enabled && inactivityTimeoutMinutes is null)
        {
            throw new ArgumentException(
                "An enabled automatic-lock policy requires an inactivity timeout.",
                nameof(inactivityTimeoutMinutes));
        }

        if (inactivityTimeoutMinutes is int minutes &&
            (minutes < MinimumInactivityTimeoutMinutes ||
             minutes > MaximumInactivityTimeoutMinutes))
        {
            throw new ArgumentOutOfRangeException(
                nameof(inactivityTimeoutMinutes),
                $"The inactivity timeout must be between " +
                $"{MinimumInactivityTimeoutMinutes} and " +
                $"{MaximumInactivityTimeoutMinutes} minutes.");
        }

        Enabled = enabled;
        InactivityTimeoutMinutes = inactivityTimeoutMinutes;
    }

    /// <summary>
    /// Gets whether automatic locking is enabled.
    /// </summary>
    public bool Enabled { get; }

    /// <summary>
    /// Gets the validated inactivity timeout in minutes.
    /// Null is permitted only when automatic locking is disabled.
    /// </summary>
    public int? InactivityTimeoutMinutes { get; }

    /// <summary>
    /// Gets the inactivity timeout as a TimeSpan.
    /// </summary>
    public TimeSpan? InactivityTimeout =>
        InactivityTimeoutMinutes is int minutes
            ? TimeSpan.FromMinutes(minutes)
            : null;
}
