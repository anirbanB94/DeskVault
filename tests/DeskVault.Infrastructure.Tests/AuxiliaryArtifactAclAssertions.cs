using System.Security.AccessControl;
using System.Security.Principal;

namespace DeskVault.Infrastructure.Tests;

internal static class AuxiliaryArtifactAclAssertions
{
    private static readonly HashSet<SecurityIdentifier> BroadPrincipals =
    [
        new(WellKnownSidType.WorldSid, null),
        new(WellKnownSidType.AuthenticatedUserSid, null),
        new(WellKnownSidType.BuiltinUsersSid, null),
        new(WellKnownSidType.InteractiveSid, null),
        new(WellKnownSidType.BuiltinGuestsSid, null)
    ];

    private const FileSystemRights SensitiveAccessRights =
        FileSystemRights.ReadData |
        FileSystemRights.ReadExtendedAttributes |
        FileSystemRights.ReadAttributes |
        FileSystemRights.ReadPermissions |
        FileSystemRights.ExecuteFile |
        FileSystemRights.WriteData |
        FileSystemRights.AppendData |
        FileSystemRights.WriteExtendedAttributes |
        FileSystemRights.WriteAttributes |
        FileSystemRights.Delete |
        FileSystemRights.DeleteSubdirectoriesAndFiles |
        FileSystemRights.ChangePermissions |
        FileSystemRights.TakeOwnership;
    private const FileSystemRights RequiredTrustedAccessRights =
        FileSystemRights.ReadData |
        FileSystemRights.WriteData |
        FileSystemRights.Delete;

    internal static FileSystemAccessRule[] CaptureAccessRules(
        string path,
        bool isDirectory)
    {
        FileSystemSecurity security = isDirectory
            ? new DirectoryInfo(path).GetAccessControl(
                AccessControlSections.Access)
            : new FileInfo(path).GetAccessControl(
                AccessControlSections.Access);

        return security
            .GetAccessRules(
                includeExplicit: true,
                includeInherited: true,
                targetType: typeof(SecurityIdentifier))
            .OfType<FileSystemAccessRule>()
            .ToArray();
    }

    internal static void AssertNoBroadPrincipalSensitiveAllowRules(
        IReadOnlyCollection<FileSystemAccessRule> rules,
        string label,
        string path)
    {
        FileSystemAccessRule[] unsafeRules = rules
            .Where(rule =>
                rule.AccessControlType == AccessControlType.Allow &&
                rule.IdentityReference is SecurityIdentifier sid &&
                BroadPrincipals.Contains(sid) &&
                (rule.FileSystemRights & SensitiveAccessRights) != 0)
            .ToArray();

        string details = string.Join(
            Environment.NewLine,
            unsafeRules.Select(rule =>
                $"{rule.IdentityReference}: {rule.FileSystemRights}; " +
                $"Inherited={rule.IsInherited}"));

        Assert.True(
            unsafeRules.Length == 0,
            $"Unexpected broad-principal access on {label} at '{path}'." +
            $"{Environment.NewLine}{details}");
    }

    internal static void AssertProtectedArtifactAcl(
        IReadOnlyCollection<FileSystemAccessRule> rules,
        string label,
        string path)
    {
        SecurityIdentifier[] trustedPrincipals =
            GetExpectedTrustedPrincipals();

        AssertNoBroadPrincipalSensitiveAllowRules(
            rules,
            label,
            path);

        AssertOnlyTrustedPrincipalsHaveSensitiveAllowRules(
            rules,
            trustedPrincipals,
            label,
            path);

        AssertNoRequiredAccessDenyRulesForCurrentToken(
            rules,
            label,
            path);

        AssertTrustedPrincipalsRetainRequiredAccess(
            rules,
            trustedPrincipals,
            label,
            path);
    }

    internal static void AssertOnlyTrustedPrincipalsHaveSensitiveAllowRules(
        IReadOnlyCollection<FileSystemAccessRule> rules,
        IReadOnlyCollection<SecurityIdentifier> trustedPrincipals,
        string label,
        string path)
    {
        HashSet<SecurityIdentifier> trustedSet =
            trustedPrincipals.ToHashSet();

        FileSystemAccessRule[] unsafeRules = rules
            .Where(rule =>
                rule.AccessControlType == AccessControlType.Allow &&
                rule.IdentityReference is SecurityIdentifier sid &&
                !trustedSet.Contains(sid) &&
                (rule.FileSystemRights & SensitiveAccessRights) != 0)
            .ToArray();

        string details = string.Join(
            Environment.NewLine,
            unsafeRules.Select(rule =>
                $"{rule.IdentityReference}: {rule.FileSystemRights}; " +
                $"Inherited={rule.IsInherited}"));

        Assert.True(
            unsafeRules.Length == 0,
            $"Unexpected sensitive Allow ACE for a non-trusted principal on {label} at '{path}'." +
            $"{Environment.NewLine}{details}");
    }

    private static void AssertNoRequiredAccessDenyRulesForCurrentToken(
        IReadOnlyCollection<FileSystemAccessRule> rules,
        string label,
        string path)
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();

        HashSet<SecurityIdentifier> tokenSids = [];

        if (identity.User is SecurityIdentifier userSid)
        {
            tokenSids.Add(userSid);
        }

        if (identity.Groups is not null)
        {
            foreach (IdentityReference group in identity.Groups)
            {
                if (group is SecurityIdentifier groupSid)
                {
                    tokenSids.Add(groupSid);
                }
            }
        }

        FileSystemAccessRule[] deniedRules = rules
            .Where(rule =>
                rule.AccessControlType == AccessControlType.Deny &&
                rule.IdentityReference is SecurityIdentifier sid &&
                tokenSids.Contains(sid) &&
                (rule.FileSystemRights & RequiredTrustedAccessRights) != 0)
            .ToArray();

        string details = string.Join(
            Environment.NewLine,
            deniedRules.Select(rule =>
                $"{rule.IdentityReference}: {rule.FileSystemRights}; " +
                $"Inherited={rule.IsInherited}"));

        Assert.True(
            deniedRules.Length == 0,
            $"Found a required-right Deny ACE affecting the current Windows token on {label} at '{path}'." +
            $"{Environment.NewLine}{details}");
    }

    internal static SecurityIdentifier[] GetExpectedTrustedPrincipals()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();

        SecurityIdentifier currentUserSid = identity.User
            ?? throw new InvalidOperationException(
                "The current Windows identity does not have a user SID.");

        return
        [
            currentUserSid,
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null)
        ];
    }

    internal static void AssertTrustedPrincipalsRetainRequiredAccess(
        IReadOnlyCollection<FileSystemAccessRule> rules,
        IReadOnlyCollection<SecurityIdentifier> trustedPrincipals,
        string label,
        string path)
    {
        foreach (SecurityIdentifier trustedPrincipal in trustedPrincipals)
        {
            // A direct Deny ACE for the same SID overrides matching Allow rights.
            // Reject it before accepting the principal's union of Allow rights.
            FileSystemRights deniedRights = rules
                .Where(rule =>
                    rule.AccessControlType == AccessControlType.Deny &&
                    rule.IdentityReference is SecurityIdentifier sid &&
                    sid.Equals(trustedPrincipal))
                .Aggregate(
                    (FileSystemRights)0,
                    (currentRights, rule) => currentRights | rule.FileSystemRights);

            FileSystemRights deniedRequiredRights =
                RequiredTrustedAccessRights & deniedRights;

            Assert.True(
                deniedRequiredRights == (FileSystemRights)0,
                $"Unexpected direct Deny ACE for trusted principal '{trustedPrincipal}' on {label} at '{path}'. " +
                $"Denied required rights: {deniedRequiredRights}.");
            FileSystemRights allowedRights = rules
                .Where(rule =>
                    rule.AccessControlType == AccessControlType.Allow &&
                    rule.IdentityReference is SecurityIdentifier sid &&
                    sid.Equals(trustedPrincipal))
                .Aggregate(
                    (FileSystemRights)0,
                    (currentRights, rule) => currentRights | rule.FileSystemRights);

            FileSystemRights missingRights =
                RequiredTrustedAccessRights & ~allowedRights;

            Assert.True(
                missingRights == (FileSystemRights)0,
                $"Expected trusted principal '{trustedPrincipal}' to retain read/write/delete Allow rights on {label} at '{path}'. " +
                $"Missing: {missingRights}; collected Allow rights: {allowedRights}.");
        }
    }
}
