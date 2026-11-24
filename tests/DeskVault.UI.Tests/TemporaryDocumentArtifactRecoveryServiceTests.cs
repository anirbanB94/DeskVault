using DeskVault.UI.Services.Document;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;

namespace DeskVault.UI.Tests;

/// <summary>
/// Verifies conservative recovery of DeskVault-owned temporary document artifacts.
/// </summary>
public sealed class TemporaryDocumentArtifactRecoveryServiceTests
{
    /// <summary>
    /// Verifies that a file occupying the expected namespace path is reported
    /// as a recovery failure instead of being treated as an empty namespace.
    /// </summary>
    [Fact]
    public void Recover_ReportsInvalidNamespaceInsteadOfTreatingItAsEmpty()
    {
        // Arrange
        using var temporaryRoot = new TemporaryRoot();

        string applicationDirectoryPath =
            Path.Combine(
                temporaryRoot.DirectoryPath,
                "DeskVault");

        Directory.CreateDirectory(applicationDirectoryPath);

        string namespacePath =
            Path.Combine(
                applicationDirectoryPath,
                "TemporaryDocuments");

        File.WriteAllText(
            namespacePath,
            "This file is not the DeskVault temporary-document directory.");

        var store = CreateStore(temporaryRoot);
        var recovery = CreateRecoveryService(store);

        // Act
        TemporaryDocumentArtifactRecoveryResult result =
            recovery.Recover();

        // Assert
        Assert.Equal(0, result.CandidateCount);

        TemporaryDocumentArtifactRecoveryFinding finding =
            Assert.Single(result.Findings);

        Assert.Equal(
            "<namespace>",
            finding.OwnershipFileName);

        Assert.Equal(
            TemporaryDocumentArtifactRecoveryDisposition.Failed,
            finding.Disposition);

        Assert.Equal(
            nameof(InvalidDataException),
            finding.FailureCategory);

        Assert.True(File.Exists(namespacePath));
    }

    /// <summary>
    /// Verifies that recovery preserves an existing file when its creation
    /// ownership cannot be confirmed by a CreationPending record.
    /// </summary>
    [Fact]
    public void Recover_PreservesExistingFileWhenCreationOwnershipIsUnconfirmed()
    {
        // Arrange
        using var temporaryRoot = new TemporaryRoot();

        var setupStore = CreateStore(temporaryRoot);

        TemporaryDocumentArtifact setupArtifact =
            setupStore.CreateArtifact("setup.txt");

        setupArtifact.MarkCleanupEligible();

        Assert.True(setupArtifact.TryCleanup().Succeeded);

        Guid artifactId = Guid.NewGuid();
        string artifactFileName = $"{artifactId:N}.txt";
        string artifactPath =
            Path.Combine(setupStore.DirectoryPath, artifactFileName);

        const string existingContent =
            "Content that does not belong to the interrupted creation.";

        File.WriteAllText(artifactPath, existingContent);

        // This file simulates a pre-existing filesystem object.
        // Give the test process explicit cleanup rights without changing
        // the production namespace or artifact security policy.
        GrantFixtureCleanupAccess(artifactPath);

        var store = new TemporaryDocumentArtifactStore(
            temporaryRoot.DirectoryPath,
            () => artifactId);

        using TemporaryDocumentArtifactOwnership ownership =
            store.CreateOwnershipReservation(
                artifactId,
                artifactFileName);

        string metadataPath = ownership.MetadataPath;

        Assert.Equal(
            TemporaryDocumentArtifactLifecycleState.CreationPending,
            ownership.CurrentState);

        ownership.ReleaseLease();

        var recovery = CreateRecoveryService(store);

        try
        {
            // Act
            TemporaryDocumentArtifactRecoveryResult result =
                recovery.Recover();

            // Assert
            Assert.Equal(1, result.CandidateCount);
            Assert.Equal(1, result.PreservedCount);
            Assert.Equal(0, result.CleanedCount);
            Assert.Equal(0, result.FailedCount);

            TemporaryDocumentArtifactRecoveryFinding finding =
                Assert.Single(result.Findings);

            Assert.Equal(
                TemporaryDocumentArtifactRecoveryDisposition.PreservedUnconfirmedCreation,
                finding.Disposition);

            Assert.Equal(
                TemporaryDocumentArtifactLifecycleState.CreationPending,
                finding.State);

            Assert.True(File.Exists(artifactPath));
            Assert.True(File.Exists(metadataPath));

            Assert.Equal(
                existingContent,
                File.ReadAllText(artifactPath));
        }
        finally
        {
            // Remove only the isolated files created by this test.
            // The explicit fixture ACL allows this cleanup.
            File.Delete(artifactPath);
            File.Delete(metadataPath);
        }
    }

    /// <summary>
    /// Verifies that an abandoned creation reservation is removed when no
    /// artifact was ever created.
    /// </summary>
    [Fact]
    public void Recover_RemovesCreationReservationWhenArtifactWasNeverCreated()
    {
        // Arrange
        using var temporaryRoot = new TemporaryRoot();

        var store = CreateStore(temporaryRoot);

        Guid artifactId = Guid.NewGuid();
        string artifactFileName = $"{artifactId:N}.txt";

        using TemporaryDocumentArtifactOwnership ownership =
            store.CreateOwnershipReservation(
                artifactId,
                artifactFileName);

        string artifactPath =
            Path.Combine(store.DirectoryPath, artifactFileName);
        string metadataPath = ownership.MetadataPath;

        Assert.False(File.Exists(artifactPath));
        Assert.True(File.Exists(metadataPath));

        ownership.ReleaseLease();

        var recovery = CreateRecoveryService(store);

        // Act
        TemporaryDocumentArtifactRecoveryResult result =
            recovery.Recover();

        // Assert
        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.CleanedCount);
        Assert.Equal(0, result.PreservedCount);
        Assert.Equal(0, result.FailedCount);

        Assert.Equal(
            TemporaryDocumentArtifactRecoveryDisposition.RemovedEmptyCreationReservation,
            Assert.Single(result.Findings).Disposition);

        Assert.False(File.Exists(artifactPath));
        Assert.False(File.Exists(metadataPath));
    }

    /// <summary>
    /// Verifies that an abandoned Preparing artifact is removed after its owner lease is released.
    /// </summary>
    [Fact]
    public async Task Recover_CleansAbandonedPreparingArtifactAndOwnershipMetadata()
    {
        // Arrange
        using var temporaryRoot = new TemporaryRoot();

        var store = CreateStore(temporaryRoot);

        TemporaryDocumentArtifact artifact =
            await CreatePreparedArtifactAsync(store);

        string artifactPath = artifact.FilePath;
        string metadataPath = artifact.OwnershipMetadataPath;

        Assert.Equal(
            TemporaryDocumentArtifactLifecycleState.Preparing,
            artifact.OwnershipState);

        artifact.ReleaseOwnershipLease();

        var recovery = CreateRecoveryService(store);

        // Act
        TemporaryDocumentArtifactRecoveryResult result =
            recovery.Recover();

        // Assert
        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.CleanedCount);
        Assert.Equal(0, result.PreservedCount);
        Assert.Equal(0, result.FailedCount);

        Assert.False(File.Exists(artifactPath));
        Assert.False(File.Exists(metadataPath));

        Assert.Equal(
            TemporaryDocumentArtifactRecoveryDisposition.Cleaned,
            Assert.Single(result.Findings).Disposition);
    }

    /// <summary>
    /// Verifies that recovery preserves an artifact whose process ownership lease is still active.
    /// </summary>
    [Fact]
    public async Task Recover_PreservesArtifactWhenOwnerLeaseIsLive()
    {
        // Arrange
        using var temporaryRoot = new TemporaryRoot();

        var store = CreateStore(temporaryRoot);

        TemporaryDocumentArtifact artifact =
            await CreatePreparedArtifactAsync(store);

        string artifactPath = artifact.FilePath;
        string metadataPath = artifact.OwnershipMetadataPath;

        var recovery = CreateRecoveryService(store);

        // Act
        TemporaryDocumentArtifactRecoveryResult result =
            recovery.Recover();

        // Assert
        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.PreservedCount);

        Assert.Equal(
            TemporaryDocumentArtifactRecoveryDisposition.PreservedActiveLease,
            Assert.Single(result.Findings).Disposition);

        Assert.True(File.Exists(artifactPath));
        Assert.True(File.Exists(metadataPath));

        // Clean up through the still-active owner.
        artifact.MarkCleanupEligible();

        Assert.True(artifact.TryCleanup().Succeeded);
    }

    /// <summary>
    /// Verifies that recovery never deletes an artifact marked as potentially
    /// required by an external consumer, even after its process lease is released.
    /// </summary>
    [Fact]
    public async Task Recover_PreservesExternalConsumerArtifactAfterLeaseReleased()
    {
        // Arrange
        using var temporaryRoot = new TemporaryRoot();

        var store = CreateStore(temporaryRoot);

        TemporaryDocumentArtifact artifact =
            await CreatePreparedArtifactAsync(store);

        artifact.MarkExternalConsumerMayNeedArtifact();

        string artifactPath = artifact.FilePath;
        string metadataPath = artifact.OwnershipMetadataPath;

        artifact.ReleaseOwnershipLease();

        var recovery = CreateRecoveryService(store);

        // Act
        TemporaryDocumentArtifactRecoveryResult result =
            recovery.Recover();

        // Assert
        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.PreservedCount);

        TemporaryDocumentArtifactRecoveryFinding finding =
            Assert.Single(result.Findings);

        Assert.Equal(
            TemporaryDocumentArtifactRecoveryDisposition.PreservedExternalConsumer,
            finding.Disposition);

        Assert.Equal(
            TemporaryDocumentArtifactLifecycleState.ExternalConsumerMayNeedArtifact,
            finding.State);

        Assert.True(File.Exists(artifactPath));
        Assert.True(File.Exists(metadataPath));

        // Remove isolated fixture files after verifying preservation.
        File.Delete(artifactPath);
        File.Delete(metadataPath);
    }

    /// <summary>
    /// Verifies that malformed ownership metadata is preserved instead of being
    /// used as evidence for deleting a plaintext artifact.
    /// </summary>
    [Fact]
    public async Task Recover_PreservesArtifactWithMalformedOwnershipMetadata()
    {
        // Arrange
        using var temporaryRoot = new TemporaryRoot();

        var store = CreateStore(temporaryRoot);

        TemporaryDocumentArtifact artifact =
            await CreatePreparedArtifactAsync(store);

        string artifactPath = artifact.FilePath;
        string metadataPath = artifact.OwnershipMetadataPath;

        artifact.ReleaseOwnershipLease();

        await File.WriteAllTextAsync(
            metadataPath,
            "not-a-valid-ownership-record\n",
            TestContext.Current.CancellationToken);

        var recovery = CreateRecoveryService(store);

        // Act
        TemporaryDocumentArtifactRecoveryResult result =
            recovery.Recover();

        // Assert
        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.PreservedCount);

        Assert.Equal(
            TemporaryDocumentArtifactRecoveryDisposition.PreservedInvalidOwnership,
            Assert.Single(result.Findings).Disposition);

        Assert.True(File.Exists(artifactPath));
        Assert.True(File.Exists(metadataPath));

        // Remove isolated fixture files after verifying preservation.
        File.Delete(artifactPath);
        File.Delete(metadataPath);
    }

    /// <summary>
    /// Verifies that recovery removes the ownership record when its cleanup-eligible
    /// artifact was already deleted by an earlier interrupted cleanup.
    /// </summary>
    [Fact]
    public async Task Recover_RemovesOwnershipMetadataWhenArtifactIsAlreadyAbsent()
    {
        // Arrange
        using var temporaryRoot = new TemporaryRoot();

        var store = CreateStore(temporaryRoot);

        TemporaryDocumentArtifact artifact =
            await CreatePreparedArtifactAsync(store);

        artifact.MarkCleanupEligible();

        string artifactPath = artifact.FilePath;
        string metadataPath = artifact.OwnershipMetadataPath;

        artifact.ReleaseOwnershipLease();
        File.Delete(artifactPath);

        var recovery = CreateRecoveryService(store);

        // Act
        TemporaryDocumentArtifactRecoveryResult result =
            recovery.Recover();

        TemporaryDocumentArtifactRecoveryResult secondResult =
            recovery.Recover();

        // Assert
        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.CleanedCount);
        Assert.Equal(0, result.FailedCount);

        Assert.Equal(
            TemporaryDocumentArtifactRecoveryDisposition.Cleaned,
            Assert.Single(result.Findings).Disposition);

        Assert.Equal(0, secondResult.CandidateCount);
        Assert.Empty(secondResult.Findings);

        Assert.False(File.Exists(artifactPath));
        Assert.False(File.Exists(metadataPath));
    }

    /// <summary>
    /// Verifies that repeated recovery is safe after the artifact and ownership
    /// metadata have already been removed.
    /// </summary>
    [Fact]
    public async Task Recover_IsIdempotentWhenArtifactAndMetadataAreAlreadyRemoved()
    {
        // Arrange
        using var temporaryRoot = new TemporaryRoot();

        var store = CreateStore(temporaryRoot);

        TemporaryDocumentArtifact artifact =
            await CreatePreparedArtifactAsync(store);

        artifact.MarkCleanupEligible();

        string artifactPath = artifact.FilePath;
        string metadataPath = artifact.OwnershipMetadataPath;

        artifact.ReleaseOwnershipLease();

        var recovery = CreateRecoveryService(store);

        // Act
        TemporaryDocumentArtifactRecoveryResult firstResult =
            recovery.Recover();

        TemporaryDocumentArtifactRecoveryResult secondResult =
            recovery.Recover();

        // Assert
        Assert.Equal(1, firstResult.CleanedCount);

        Assert.Equal(0, secondResult.CandidateCount);
        Assert.Empty(secondResult.Findings);

        Assert.False(File.Exists(artifactPath));
        Assert.False(File.Exists(metadataPath));
    }

    /// <summary>
    /// Verifies that a deletion failure for one artifact does not prevent
    /// recovery from cleaning another safe artifact.
    /// </summary>
    [Fact]
    public async Task Recover_ContinuesAfterPerArtifactDeletionFailure()
    {
        // Arrange
        using var temporaryRoot = new TemporaryRoot();

        var store = CreateStore(temporaryRoot);

        TemporaryDocumentArtifact blockedArtifact =
            await CreatePreparedArtifactAsync(store);

        TemporaryDocumentArtifact cleanableArtifact =
            await CreatePreparedArtifactAsync(store);

        blockedArtifact.MarkCleanupEligible();
        cleanableArtifact.MarkCleanupEligible();

        string blockedPath = blockedArtifact.FilePath;
        string blockedMetadataPath = blockedArtifact.OwnershipMetadataPath;

        string cleanablePath = cleanableArtifact.FilePath;
        string cleanableMetadataPath = cleanableArtifact.OwnershipMetadataPath;

        blockedArtifact.ReleaseOwnershipLease();
        cleanableArtifact.ReleaseOwnershipLease();

        var recovery = CreateRecoveryService(store);

        using (var fileLock = new FileStream(
            blockedPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.None))
        {
            // Act
            TemporaryDocumentArtifactRecoveryResult result =
                recovery.Recover();

            // Assert
            Assert.Equal(2, result.CandidateCount);
            Assert.Equal(1, result.CleanedCount);
            Assert.Equal(1, result.FailedCount);

            TemporaryDocumentArtifactRecoveryFinding failedFinding =
                Assert.Single(result.Findings, static finding => finding.Disposition ==
                            TemporaryDocumentArtifactRecoveryDisposition.Failed);

            Assert.Equal(
                Path.GetFileName(blockedMetadataPath),
                failedFinding.OwnershipFileName);

            Assert.Equal(
                TemporaryDocumentArtifactLifecycleState.CleanupEligible,
                failedFinding.State);

            Assert.Equal(
                nameof(IOException),
                failedFinding.FailureCategory);

            Assert.True(File.Exists(blockedPath));
            Assert.True(File.Exists(blockedMetadataPath));

            Assert.False(File.Exists(cleanablePath));
            Assert.False(File.Exists(cleanableMetadataPath));
        }

        // Act: retry after the blocking handle has been released.
        TemporaryDocumentArtifactRecoveryResult retryResult =
            recovery.Recover();

        // Assert
        Assert.Equal(1, retryResult.CleanedCount);
        Assert.Equal(0, retryResult.FailedCount);

        Assert.False(File.Exists(blockedPath));
        Assert.False(File.Exists(blockedMetadataPath));
    }

    /// <summary>
    /// Verifies that recovery does not scan or delete unrelated files,
    /// unowned files in the dedicated namespace, or nested ownership-like files.
    /// </summary>
    [Fact]
    public async Task Recover_LeavesUnrelatedTemporaryFilesUntouched()
    {
        // Arrange
        using var temporaryRoot = new TemporaryRoot();

        string unrelatedFilePath =
            Path.Combine(
                temporaryRoot.DirectoryPath,
                "unrelated-document.txt");

        const string unrelatedContent =
            "Unrelated operating-system temporary content.";

        await File.WriteAllTextAsync(
            unrelatedFilePath,
            unrelatedContent,
            TestContext.Current.CancellationToken);

        // Prepare the nested directory before the store applies its
        // restrictive namespace ACL. The production namespace deliberately
        // does not grant permission to create subdirectories.
        string namespacePath =
            Path.Combine(
                temporaryRoot.DirectoryPath,
                "DeskVault",
                "TemporaryDocuments");

        string nestedDirectoryPath =
            Path.Combine(
                namespacePath,
                "nested");

        Directory.CreateDirectory(nestedDirectoryPath);

        string nestedOwnershipPath =
            Path.Combine(
                nestedDirectoryPath,
                "nested-artifact.owner.json");

        const string nestedOwnershipContent =
            "Nested ownership-like metadata is outside the enumerated namespace.";

        await File.WriteAllTextAsync(
            nestedOwnershipPath,
            nestedOwnershipContent,
            TestContext.Current.CancellationToken);

        var store = CreateStore(temporaryRoot);

        TemporaryDocumentArtifact artifact =
            await CreatePreparedArtifactAsync(store);

        // Creating an ordinary file in the dedicated namespace is permitted.
        string unownedFilePath =
            Path.Combine(
                store.DirectoryPath,
                "unowned-notes.txt");

        const string unownedContent =
            "An ordinary file in the dedicated namespace is not an owned artifact.";

        await File.WriteAllTextAsync(
            unownedFilePath,
            unownedContent,
            TestContext.Current.CancellationToken);

        artifact.MarkCleanupEligible();
        artifact.ReleaseOwnershipLease();

        var recovery = CreateRecoveryService(store);

        // Act
        TemporaryDocumentArtifactRecoveryResult result =
            recovery.Recover();

        // Assert: only the top-level DeskVault-owned artifact is considered.
        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(1, result.CleanedCount);
        Assert.Equal(0, result.PreservedCount);
        Assert.Equal(0, result.FailedCount);

        Assert.Equal(
            TemporaryDocumentArtifactRecoveryDisposition.Cleaned,
            Assert.Single(result.Findings).Disposition);

        Assert.True(File.Exists(unrelatedFilePath));
        Assert.True(File.Exists(unownedFilePath));
        Assert.True(Directory.Exists(nestedDirectoryPath));
        Assert.True(File.Exists(nestedOwnershipPath));

        Assert.Equal(
            unrelatedContent,
            await File.ReadAllTextAsync(
                unrelatedFilePath,
                TestContext.Current.CancellationToken));

        Assert.Equal(
            unownedContent,
            await File.ReadAllTextAsync(
                unownedFilePath,
                TestContext.Current.CancellationToken));

        Assert.Equal(
            nestedOwnershipContent,
            await File.ReadAllTextAsync(
                nestedOwnershipPath,
                TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Verifies that an artifact retained for an external consumer prevents
    /// a new artifact from reusing the same identity and extension.
    /// </summary>
    [Fact]
    public async Task Recover_RetainedArtifactPreventsExactIdentityAndExtensionCollision()
    {
        // Arrange
        using var temporaryRoot = new TemporaryRoot();

        Guid artifactId = Guid.NewGuid();

        var store = new TemporaryDocumentArtifactStore(
            temporaryRoot.DirectoryPath,
            () => artifactId);

        TemporaryDocumentArtifact artifact =
            await CreatePreparedArtifactAsync(store);

        artifact.MarkExternalConsumerMayNeedArtifact();

        string artifactPath = artifact.FilePath;
        string metadataPath = artifact.OwnershipMetadataPath;

        const string expectedContent =
            "Retained external-consumer content.";

        await File.WriteAllTextAsync(
            artifactPath,
            expectedContent,
            TestContext.Current.CancellationToken);

        artifact.ReleaseOwnershipLease();

        var recovery = CreateRecoveryService(store);

        // Act
        TemporaryDocumentArtifactRecoveryResult result =
            recovery.Recover();

        // Assert
        Assert.Equal(1, result.PreservedCount);

        Assert.Throws<IOException>(
            () => store.CreateArtifact("document.txt"));

        Assert.True(File.Exists(artifactPath));
        Assert.True(File.Exists(metadataPath));

        Assert.Equal(
            expectedContent,
            await File.ReadAllTextAsync(
                artifactPath,
                TestContext.Current.CancellationToken));

        // Remove isolated fixture files after verifying collision protection.
        File.Delete(artifactPath);
        File.Delete(metadataPath);
    }

    /// <summary>
    /// Grants the test process explicit cleanup rights on a fixture file.
    /// This does not change production storage or artifact ACLs.
    /// </summary>
    /// <param name="filePath">The test-created file to clean up.</param>
    private static void GrantFixtureCleanupAccess(
        string filePath)
    {
        using WindowsIdentity identity =
            WindowsIdentity.GetCurrent();

        SecurityIdentifier currentUserSid =
            identity.User ??
            throw new InvalidOperationException(
                "The current Windows user identity is unavailable.");

        var security = new FileSecurity();

        security.SetAccessRuleProtection(
            isProtected: true,
            preserveInheritance: false);

        security.AddAccessRule(
            new FileSystemAccessRule(
                currentUserSid,
                FileSystemRights.FullControl,
                AccessControlType.Allow));

        FileSystemAclExtensions.SetAccessControl(
            new FileInfo(filePath),
            security);
    }

    /// <summary>
    /// Grants the test process explicit cleanup rights on an isolated fixture directory.
    /// This is used only during test teardown and does not change production ACL policy.
    /// </summary>
    /// <param name="directoryPath">The test-created directory to clean up.</param>
    private static void GrantFixtureDirectoryCleanupAccess(
        string directoryPath)
    {
        using WindowsIdentity identity =
            WindowsIdentity.GetCurrent();

        SecurityIdentifier currentUserSid =
            identity.User ??
            throw new InvalidOperationException(
                "The current Windows user identity is unavailable.");

        var security = new DirectorySecurity();

        security.SetAccessRuleProtection(
            isProtected: true,
            preserveInheritance: false);

        security.AddAccessRule(
            new FileSystemAccessRule(
                currentUserSid,
                FileSystemRights.FullControl,
                AccessControlType.Allow));

        FileSystemAclExtensions.SetAccessControl(
            new DirectoryInfo(directoryPath),
            security);
    }

    private static TemporaryDocumentArtifactStore CreateStore(
        TemporaryRoot temporaryRoot)
    {
        return new TemporaryDocumentArtifactStore(
            temporaryRoot.DirectoryPath,
            Guid.NewGuid);
    }

    private static TemporaryDocumentArtifactRecoveryService CreateRecoveryService(
        TemporaryDocumentArtifactStore store)
    {
        return new TemporaryDocumentArtifactRecoveryService(
            store,
            NullLogger<TemporaryDocumentArtifactRecoveryService>.Instance);
    }

    private static async Task<TemporaryDocumentArtifact> CreatePreparedArtifactAsync(
        TemporaryDocumentArtifactStore store)
    {
        TemporaryDocumentArtifact artifact =
            store.CreateArtifact("document.txt");

        using var sourceStream = new MemoryStream(
            Encoding.UTF8.GetBytes("Temporary recovery test content."));

        await artifact.CopyFromAsync(
            sourceStream,
            TestContext.Current.CancellationToken);

        return artifact;
    }

    /// <summary>
    /// Provides an isolated filesystem root for recovery tests.
    /// </summary>
    private sealed class TemporaryRoot : IDisposable
    {
        public TemporaryRoot()
        {
            DirectoryPath =
                Path.Combine(
                    Path.GetTempPath(),
                    $"DeskVault.ArtifactRecoveryTests.{Guid.NewGuid():N}");

            Directory.CreateDirectory(DirectoryPath);
        }

        public string DirectoryPath { get; }

        public void Dispose()
        {
            if (!Directory.Exists(DirectoryPath))
            {
                return;
            }

            // Production storage deliberately uses restrictive ACLs.
            // Normalize the isolated test tree only during teardown so that
            // preserved unowned files and protected namespace directories can
            // be removed without changing production security behaviour.

            string[] filePaths =
            [
                .. Directory.GetFiles(
                DirectoryPath,
                "*",
                SearchOption.AllDirectories)
            ];

            string[] nestedDirectoryPaths =
            [
                .. Directory.GetDirectories(
                DirectoryPath,
                "*",
                SearchOption.AllDirectories)
                .OrderByDescending(path => path.Length)
            ];

            // Files first, so their individual DACLs grant cleanup rights.
            foreach (string filePath in filePaths)
            {
                GrantFixtureCleanupAccess(filePath);
            }

            // Normalize deepest directories first, then their parents.
            foreach (string directoryPath in nestedDirectoryPaths)
            {
                GrantFixtureDirectoryCleanupAccess(directoryPath);
            }

            GrantFixtureDirectoryCleanupAccess(DirectoryPath);

            Directory.Delete(
                DirectoryPath,
                recursive: true);
        }
    }
}
