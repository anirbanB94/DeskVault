using System.Globalization;
using DeskVault.Application.Configurations;
using Microsoft.Extensions.Configuration;

namespace DeskVault.UI.Hosting;

/// <summary>
/// Reads and strictly validates the effective automatic-lock configuration
/// at the production composition boundary.
/// </summary>
internal static class AutomaticLockSettingsFactory
{
    private const string SectionPath = "Security:AutomaticLock";

    private const string EnabledKey = "Enabled";

    private const string InactivityTimeoutMinutesKey =
        "InactivityTimeoutMinutes";

    public static AutomaticLockSettings Create(
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        IConfigurationSection section =
            configuration.GetSection(SectionPath);

        IConfigurationSection[] children =
            section.GetChildren().ToArray();

        // JSON configuration can represent an explicitly empty object
        // as a provider entry whose value is null and which has no children.
        // Check the providers as well as the effective section value so
        // that an explicitly present but empty section fails closed.
        bool sectionKeyExistsInProvider =
            configuration is IConfigurationRoot configurationRoot &&
            configurationRoot.Providers.Any(
                provider =>
                    provider.TryGet(section.Path, out _));

        // An absent section preserves the backward-compatible disabled
        // behavior. A present scalar, provider entry, or section containing
        // child settings must satisfy the explicit configuration contract.
        bool sectionPresent =
            section.Value is not null ||
            children.Length > 0 ||
            sectionKeyExistsInProvider;

        if (!sectionPresent)
        {
            return AutomaticLockSettings.Disabled;
        }

        string? enabledValue =
            section[EnabledKey];

        if (!bool.TryParse(
                enabledValue,
                out bool enabled))
        {
            throw new InvalidOperationException(
                $"{SectionPath}:{EnabledKey} must be supplied " +
                "as a valid Boolean when the section is present.");
        }

        string? timeoutValue =
            section[InactivityTimeoutMinutesKey];

        bool timeoutWasSupplied =
            children.Any(
                child =>
                    string.Equals(
                        child.Key,
                        InactivityTimeoutMinutesKey,
                        StringComparison.OrdinalIgnoreCase))
            || timeoutValue is not null;

        int? timeoutMinutes = null;

        if (timeoutWasSupplied)
        {
            if (!int.TryParse(
                    timeoutValue,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int parsedTimeout))
            {
                throw new InvalidOperationException(
                    $"{SectionPath}:{InactivityTimeoutMinutesKey} " +
                    "must be a valid integer when supplied.");
            }

            if (parsedTimeout <
                    AutomaticLockSettings.MinimumInactivityTimeoutMinutes ||
                parsedTimeout >
                    AutomaticLockSettings.MaximumInactivityTimeoutMinutes)
            {
                throw new InvalidOperationException(
                    $"{SectionPath}:{InactivityTimeoutMinutesKey} must be " +
                    $"between {AutomaticLockSettings.MinimumInactivityTimeoutMinutes} " +
                    $"and {AutomaticLockSettings.MaximumInactivityTimeoutMinutes}.");
            }

            timeoutMinutes = parsedTimeout;
        }

        if (enabled && timeoutMinutes is null)
        {
            throw new InvalidOperationException(
                $"{SectionPath}:{InactivityTimeoutMinutesKey} is required " +
                "when automatic locking is enabled.");
        }

        return new AutomaticLockSettings(
            enabled,
            timeoutMinutes);
    }
}