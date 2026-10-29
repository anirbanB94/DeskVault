using System.Security.AccessControl;
using System.Security.Principal;
using DeskVault.Infrastructure.Persistence;
using DeskVault.Infrastructure.Services;

namespace DeskVault.Infrastructure.Tests;

public sealed class AuxiliaryArtifactProtectionIntegrationTests
{
    [Fact]
    public void BroadPrincipalAclAssertion_RejectsSensitiveAllowRule()
    {
        // Arrange: simulate an unsafe World SID allow ACE without changing a real ACL.
        var broadPrincipal =
            new SecurityIdentifier(WellKnownSidType.WorldSid, null);

        var unsafeRule =
            new FileSystemAccessRule(
                broadPrincipal,
                FileSystemRights.ReadData,
                AccessControlType.Allow);

        // Act
        Xunit.Sdk.TrueException exception =
            Assert.Throws<Xunit.Sdk.TrueException>(
                () => AuxiliaryArtifactAclAssertions
                    .AssertNoBroadPrincipalSensitiveAllowRules(
                        [unsafeRule],
                        "negative-control artifact",
                        "<synthetic path>"));

        // Assert
        Assert.Contains(
            "Unexpected broad-principal access",
            exception.Message);
    }

    [Fact]
    public void TrustedPrincipalAclAssertion_RejectsDirectDenyAceForRequiredRight()
    {
        // Arrange: the principal has the required Allow rights, but Delete is
        // explicitly denied to the same SID. The positive check must not pass.
        SecurityIdentifier[] expectedPrincipals =
            AuxiliaryArtifactAclAssertions.GetExpectedTrustedPrincipals();

        SecurityIdentifier trustedPrincipal =
            expectedPrincipals[0];

        var allowRule =
            new FileSystemAccessRule(
                trustedPrincipal,
                FileSystemRights.ReadData |
                    FileSystemRights.WriteData |
                    FileSystemRights.Delete,
                AccessControlType.Allow);

        var denyRule =
            new FileSystemAccessRule(
                trustedPrincipal,
                FileSystemRights.Delete,
                AccessControlType.Deny);

        // Act
        Xunit.Sdk.TrueException exception =
            Assert.Throws<Xunit.Sdk.TrueException>(
                () => AuxiliaryArtifactAclAssertions
                    .AssertTrustedPrincipalsRetainRequiredAccess(
                        [allowRule, denyRule],
                        [trustedPrincipal],
                        "negative-control artifact",
                        "<synthetic path>"));

        // Assert
        Assert.Contains(
            "Unexpected direct Deny ACE",
            exception.Message);
    }

    [Fact]
    public void TrustedPrincipalAclAssertion_RejectsMissingRequiredRights()
    {
        // Arrange: an empty ACL snapshot must not pass positive access checks.
        SecurityIdentifier[] expectedPrincipals =
            AuxiliaryArtifactAclAssertions.GetExpectedTrustedPrincipals();

        // Act
        Xunit.Sdk.TrueException exception =
            Assert.Throws<Xunit.Sdk.TrueException>(
                () => AuxiliaryArtifactAclAssertions
                    .AssertTrustedPrincipalsRetainRequiredAccess(
                        Array.Empty<FileSystemAccessRule>(),
                        expectedPrincipals,
                        "negative-control artifact",
                        "<synthetic path>"));

        // Assert
        Assert.Contains(
            "Expected trusted principal",
            exception.Message);
    }

    [Fact]
    public void SensitiveAllowAssertion_RejectsNonTrustedPrincipal()
    {
        // Arrange: use a syntactically valid SID that is not one of the
        // explicit trusted principals or the existing broad-principal set.
        var untrustedPrincipal =
            new SecurityIdentifier("S-1-5-21-111111111-222222222-333333333-4444");

        var unsafeRule =
            new FileSystemAccessRule(
                untrustedPrincipal,
                FileSystemRights.ReadData,
                AccessControlType.Allow);

        // Act
        Xunit.Sdk.TrueException exception =
            Assert.Throws<Xunit.Sdk.TrueException>(
                () => AuxiliaryArtifactAclAssertions
                    .AssertOnlyTrustedPrincipalsHaveSensitiveAllowRules(
                        [unsafeRule],
                        AuxiliaryArtifactAclAssertions.GetExpectedTrustedPrincipals(),
                        "negative-control artifact",
                        "<synthetic path>"));

        // Assert
        Assert.Contains(
            "Unexpected sensitive Allow ACE",
            exception.Message);
    }

    [Fact]
    public void SyntheticArtifactPaths_DoNotGrantSensitiveRightsToBroadPrincipals()
    {
        // Arrange
        Assert.True(
            OperatingSystem.IsWindows(),
            "This integration test requires Windows ACL support.");

        string localApplicationData =
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        Assert.False(
            string.IsNullOrWhiteSpace(localApplicationData),
            "LocalApplicationData must resolve to a valid directory.");

        string applicationRootDirectory =
            new DeskVaultDataPaths().RootDirectory;

        Assert.Equal(
            Path.GetFullPath(
                Path.Combine(localApplicationData, "DeskVault")),
            Path.GetFullPath(applicationRootDirectory));

        bool applicationRootExisted =
            Directory.Exists(applicationRootDirectory);

        string rootDirectory = Path.Combine(
            applicationRootDirectory,
            $"PBI234-ACL-{Guid.NewGuid():N}");

        string documentsDirectory = Path.Combine(rootDirectory, "Documents");
        string securityDirectory = Path.Combine(rootDirectory, "Security");

        ArtifactTarget[] targets =
        [
            new("application data root", applicationRootDirectory, true),
            new("isolated artifact test root", rootDirectory, true),
            new("Documents directory", documentsDirectory, true),
            new("Security directory", securityDirectory, true),
            new("database", Path.Combine(rootDirectory, "DeskVault.db"), false),
            new("migration staging file", Path.Combine(rootDirectory, "DeskVault.db.migration"), false),
            new("migration backup", Path.Combine(rootDirectory, "DeskVault.db.migration-backup"), false),
            new("initialization lock", Path.Combine(rootDirectory, "DeskVault.db.initialization.lock"), false),
            new("SQLite WAL candidate", Path.Combine(rootDirectory, "DeskVault.db-wal"), false),
            new("SQLite SHM candidate", Path.Combine(rootDirectory, "DeskVault.db-shm"), false),
            new("SQLite journal candidate", Path.Combine(rootDirectory, "DeskVault.db-journal"), false),
            new("document candidate", Path.Combine(documentsDirectory, "synthetic-document.dvault"), false),
            new("master key candidate", Path.Combine(securityDirectory, "master.key"), false),
            new("database key candidate", Path.Combine(securityDirectory, "database.key"), false)
        ];

        try
        {
            Directory.CreateDirectory(documentsDirectory);
            Directory.CreateDirectory(securityDirectory);
            CreateSyntheticFiles(targets);

            // Act
            AclSnapshot[] observations = targets
                .Select(CaptureAcl)
                .ToArray();

            // Assert
            foreach (AclSnapshot observation in observations)
            {
                AuxiliaryArtifactAclAssertions
                    .AssertProtectedArtifactAcl(
                        observation.Rules,
                        observation.Target.Label,
                        observation.Target.Path);
            }
        }
        finally
        {
            // Delete only this test's isolated subtree.
            if (Directory.Exists(rootDirectory))
            {
                Directory.Delete(rootDirectory, recursive: true);
            }

            // If this test had to create the application root, remove it only
            // if it remains empty after deleting this test's unique subtree.
            if (!applicationRootExisted &&
                Directory.Exists(applicationRootDirectory) &&
                !Directory.EnumerateFileSystemEntries(applicationRootDirectory).Any())
            {
                Directory.Delete(applicationRootDirectory);
            }
        }
    }

    [Fact]
    public async Task VaultInitializationCoordinator_WhenAcquiresLock_CreatesProtectedEmptyLockUnderApplicationRoot()
    {
        // Arrange
        Assert.True(
            OperatingSystem.IsWindows(),
            "This integration test requires Windows ACL and file-sharing semantics.");

        string localApplicationData =
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);

        Assert.False(
            string.IsNullOrWhiteSpace(localApplicationData));

        string applicationRootDirectory =
            new DeskVaultDataPaths().RootDirectory;

        Assert.Equal(
            Path.GetFullPath(
                Path.Combine(localApplicationData, "DeskVault")),
            Path.GetFullPath(applicationRootDirectory));

        bool applicationRootExisted =
            Directory.Exists(applicationRootDirectory);

        string rootDirectory =
            Path.Combine(
                applicationRootDirectory,
                $"PBI234-Lock-{Guid.NewGuid():N}");

        string lockPath =
            Path.Combine(
                rootDirectory,
                "DeskVault.db.initialization.lock");

        try
        {
            var dataPaths =
                new DeskVaultDataPaths(rootDirectory);

            var coordinator =
                new VaultInitializationCoordinator(dataPaths);

            bool operationExecuted = false;

            // Act: inspect the actual lock while the coordinator holds it.
            await coordinator.ExecuteAsync(
                cancellationToken =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    operationExecuted = true;

                    Assert.True(
                        File.Exists(lockPath),
                        $"Expected the coordinator to create '{lockPath}'.");

                    Assert.Equal(
                        0L,
                        new FileInfo(lockPath).Length);

                    // Verify that a competing exclusive open is denied while
                    // the coordinator still owns the lock handle.
                    Assert.Throws<IOException>(
                        () =>
                        {
                            using var competingHandle =
                                new FileStream(
                                    lockPath,
                                    FileMode.Open,
                                    FileAccess.ReadWrite,
                                    FileShare.None);
                        });

                    return Task.CompletedTask;
                },
                TestContext.Current.CancellationToken);

            // Assert the coordinator ran and released its lock handle.
            Assert.True(operationExecuted);
            Assert.True(File.Exists(lockPath));
            Assert.Equal(0L, new FileInfo(lockPath).Length);

            using (var releasedHandle =
                new FileStream(
                    lockPath,
                    FileMode.Open,
                    FileAccess.ReadWrite,
                    FileShare.None))
            {
                // Successfully opening the file demonstrates that the
                // coordinator has released the exclusive handle.
            }

            var lockRules =
                AuxiliaryArtifactAclAssertions.CaptureAccessRules(
                    lockPath,
                    isDirectory: false);

            AuxiliaryArtifactAclAssertions.AssertProtectedArtifactAcl(
                lockRules,
                "actual initialization coordination lock",
                lockPath);

            var testRootRules =
                AuxiliaryArtifactAclAssertions.CaptureAccessRules(
                    rootDirectory,
                    isDirectory: true);

            AuxiliaryArtifactAclAssertions.AssertProtectedArtifactAcl(
                testRootRules,
                "isolated application-root child",
                rootDirectory);
        }
        finally
        {
            // Only remove the unique directory created by this test.
            if (Directory.Exists(rootDirectory))
            {
                Directory.Delete(rootDirectory, recursive: true);
            }

            if (!applicationRootExisted &&
                Directory.Exists(applicationRootDirectory) &&
                !Directory.EnumerateFileSystemEntries(applicationRootDirectory).Any())
            {
                Directory.Delete(applicationRootDirectory);
            }
        }
    }

    private static void CreateSyntheticFiles(IEnumerable<ArtifactTarget> targets)
    {
        const string syntheticContent = "PBI234-SYNTHETIC-ACL-TEST-MARKER";

        foreach (ArtifactTarget target in targets.Where(target => !target.IsDirectory))
        {
            File.WriteAllText(target.Path, syntheticContent);
        }
    }

    private static AclSnapshot CaptureAcl(ArtifactTarget target)
    {
        FileSystemAccessRule[] rules =
            AuxiliaryArtifactAclAssertions.CaptureAccessRules(
                target.Path,
                target.IsDirectory);

        return new AclSnapshot(target, rules);
    }

    private sealed record ArtifactTarget(
        string Label,
        string Path,
        bool IsDirectory);

    private sealed record AclSnapshot(
        ArtifactTarget Target,
        IReadOnlyList<FileSystemAccessRule> Rules);
}
