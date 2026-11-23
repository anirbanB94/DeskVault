using DeskVault.UI.Services.Document;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;

namespace DeskVault.UI.Tests;

/// <summary>
/// Verifies namespace isolation, access restrictions, artifact ownership and cleanup behavior.
/// </summary>
public sealed class TemporaryDocumentArtifactStoreTests
{
    /// <summary>
    /// Verifies that artifacts use the dedicated namespace and retain their assigned identity.
    /// </summary>
    [Fact]
    public void CreateArtifact_UsesDedicatedNamespaceAndStableIdentity()
    {
        // Arrange
        using var temporaryRoot = new TemporaryRoot();

        Guid expectedId = Guid.NewGuid();

        var store = new TemporaryDocumentArtifactStore(
            temporaryRoot.DirectoryPath,
            () => expectedId);

        // Act
        TemporaryDocumentArtifact artifact =
            store.CreateArtifact("report.html");

        // Assert
        Assert.Equal(
            Path.Combine(
                temporaryRoot.DirectoryPath,
                "DeskVault",
                "TemporaryDocuments"),
            store.DirectoryPath);

        Assert.Equal(
            expectedId,
            artifact.Id);

        Assert.Equal(
            $"{expectedId:N}.html",
            Path.GetFileName(artifact.FilePath));

        Assert.Equal(
            store.DirectoryPath,
            Path.GetDirectoryName(artifact.FilePath));

        Assert.True(
            File.Exists(artifact.FilePath));

        Assert.True(
            artifact.TryCleanup().Succeeded);
    }

    /// <summary>
    /// Verifies that the artifact directory grants access only to the current Windows user.
    /// </summary>
    [Fact]
    public void CreateArtifact_DirectoryAclAllowsOnlyCurrentUser()
    {
        // Arrange
        using var temporaryRoot = new TemporaryRoot();

        var store = new TemporaryDocumentArtifactStore(
            temporaryRoot.DirectoryPath,
            Guid.NewGuid);

        // Act
        TemporaryDocumentArtifact artifact =
            store.CreateArtifact("document.txt");

        DirectorySecurity security =
            new DirectoryInfo(store.DirectoryPath)
                .GetAccessControl();

        // Assert
        AssertAccessRules(
            security,
            FileSystemRights.ReadAndExecute |
            FileSystemRights.CreateFiles |
            FileSystemRights.ChangePermissions |
            FileSystemRights.Synchronize,
            InheritanceFlags.ContainerInherit |
            InheritanceFlags.ObjectInherit);

        Assert.True(
            artifact.TryCleanup().Succeeded);
    }

    /// <summary>
    /// Verifies that each artifact file receives an explicit current-user-only access policy.
    /// </summary>
    [Fact]
    public void CreateArtifact_FileAclAllowsOnlyCurrentUser()
    {
        // Arrange
        using var temporaryRoot = new TemporaryRoot();

        var store = new TemporaryDocumentArtifactStore(
            temporaryRoot.DirectoryPath,
            Guid.NewGuid);

        // Act
        TemporaryDocumentArtifact artifact =
            store.CreateArtifact("document.txt");

        FileSecurity security =
            new FileInfo(artifact.FilePath)
                .GetAccessControl();

        // Assert
        AssertAccessRules(
            security,
            FileSystemRights.Read |
            FileSystemRights.Write |
            FileSystemRights.Delete |
            FileSystemRights.Synchronize,
            InheritanceFlags.None);

        Assert.True(
            artifact.TryCleanup().Succeeded);
    }

    /// <summary>
    /// Verifies that exclusive creation does not overwrite an existing artifact.
    /// </summary>
    [Fact]
    public void CreateArtifact_WhenIdentityCollides_DoesNotOverwriteExistingFile()
    {
        // Arrange
        using var temporaryRoot = new TemporaryRoot();

        Guid existingId = Guid.NewGuid();

        var store = new TemporaryDocumentArtifactStore(
            temporaryRoot.DirectoryPath,
            () => existingId);

        TemporaryDocumentArtifact initialArtifact =
            store.CreateArtifact("initial.txt");

        Assert.True(
            initialArtifact.TryCleanup().Succeeded);

        string existingPath =
            Path.Combine(
                store.DirectoryPath,
                $"{existingId:N}.txt");

        const string existingContent =
            "Existing unrelated content.";

        File.WriteAllText(
            existingPath,
            existingContent);
        // Keep this unrelated collision file independently removable during fixture cleanup.
        // It must not inherit the artifact ACL.
        System.Security.Principal.WindowsIdentity currentIdentity =
            System.Security.Principal.WindowsIdentity.GetCurrent();

        try
        {
            System.Security.Principal.SecurityIdentifier currentUserSid =
                currentIdentity.User ??
                throw new InvalidOperationException(
                    "The current Windows user SID is unavailable.");

            var existingFileSecurity =
                new System.Security.AccessControl.FileSecurity();

            existingFileSecurity.SetAccessRuleProtection(
                isProtected: true,
                preserveInheritance: false);

            existingFileSecurity.AddAccessRule(
                new System.Security.AccessControl.FileSystemAccessRule(
                    currentUserSid,
                    System.Security.AccessControl.FileSystemRights.Read |
                    System.Security.AccessControl.FileSystemRights.Write |
                    System.Security.AccessControl.FileSystemRights.Delete |
                    System.Security.AccessControl.FileSystemRights.Synchronize,
                    System.Security.AccessControl.AccessControlType.Allow));

            System.IO.FileSystemAclExtensions.SetAccessControl(
                new System.IO.FileInfo(existingPath),
                existingFileSecurity);
        }
        finally
        {
            currentIdentity.Dispose();
        }


        // Act
        Assert.Throws<IOException>(
            () => store.CreateArtifact("document.txt"));

        // Assert
        Assert.Equal(
            existingContent,
            File.ReadAllText(existingPath));
    }

    /// <summary>
    /// Verifies complete content persistence and preservation of the caller-owned source stream.
    /// </summary>
    [Fact]
    public async Task CopyFromAsync_WritesCompleteContentAndPreservesCallerStream()
    {
        // Arrange
        using var temporaryRoot = new TemporaryRoot();

        var store = new TemporaryDocumentArtifactStore(
            temporaryRoot.DirectoryPath,
            Guid.NewGuid);

        const string expectedContent =
            """
            DeskVault temporary document.
            Multiple lines are preserved.
            """;

        using var sourceStream = new MemoryStream(
            Encoding.UTF8.GetBytes(expectedContent));

        TemporaryDocumentArtifact artifact =
            store.CreateArtifact("document.txt");

        // Act
        await artifact.CopyFromAsync(
            sourceStream,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(sourceStream.CanRead);

        Assert.Equal(
            expectedContent,
            await File.ReadAllTextAsync(
                artifact.FilePath,
                TestContext.Current.CancellationToken));

        Assert.True(
            artifact.TryCleanup().Succeeded);

        Assert.True(
            artifact.TryCleanup().Succeeded);

        Assert.False(
            File.Exists(artifact.FilePath));
    }

    /// <summary>
    /// Verifies that cancellation leaves the artifact in a state that permits cleanup.
    /// </summary>
    [Fact]
    public async Task CopyFromAsync_WhenCancelled_AllowsOwnedArtifactCleanup()
    {
        // Arrange
        using var temporaryRoot = new TemporaryRoot();

        var store = new TemporaryDocumentArtifactStore(
            temporaryRoot.DirectoryPath,
            Guid.NewGuid);

        TemporaryDocumentArtifact artifact =
            store.CreateArtifact("document.txt");

        using var sourceStream = new MemoryStream(
            Encoding.UTF8.GetBytes("Sensitive content."));

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => artifact.CopyFromAsync(
                sourceStream,
                cancellationTokenSource.Token));

        // Assert
        Assert.True(sourceStream.CanRead);

        Assert.True(
            artifact.TryCleanup().Succeeded);

        Assert.False(
            File.Exists(artifact.FilePath));
    }

    /// <summary>
    /// Verifies that a failed deletion is reported and can be retried after the file becomes available.
    /// </summary>
    [Fact]
    public async Task TryCleanup_WhenDeletionFails_ReportsFailureAndAllowsRetry()
    {
        // Arrange
        using var temporaryRoot = new TemporaryRoot();

        var store = new TemporaryDocumentArtifactStore(
            temporaryRoot.DirectoryPath,
            Guid.NewGuid);

        TemporaryDocumentArtifact artifact =
            store.CreateArtifact("document.txt");

        using var sourceStream = new MemoryStream(
            Encoding.UTF8.GetBytes("Sensitive content."));

        await artifact.CopyFromAsync(
            sourceStream,
            TestContext.Current.CancellationToken);

        // Prevent deletion using a Windows sharing restriction.
        using (var fileLock = new FileStream(
            artifact.FilePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.None))
        {
            // Act
            TemporaryDocumentArtifactCleanupResult result =
                artifact.TryCleanup();

            // Assert
            Assert.False(result.Succeeded);
            Assert.Equal("IOException", result.FailureCategory);
            Assert.True(File.Exists(artifact.FilePath));
        }

        // Act: retry after the blocking handle has been released.
        TemporaryDocumentArtifactCleanupResult retryResult =
            artifact.TryCleanup();

        // Assert
        Assert.True(retryResult.Succeeded);
        Assert.Null(retryResult.FailureCategory);
        Assert.False(File.Exists(artifact.FilePath));
    }

    /// <summary>
    /// Verifies that unsupported extension syntax is rejected before the namespace is created.
    /// </summary>
    [Fact]
    public void CreateArtifact_RejectsUnsafeExtension()
    {
        // Arrange
        using var temporaryRoot = new TemporaryRoot();

        var store = new TemporaryDocumentArtifactStore(
            temporaryRoot.DirectoryPath,
            Guid.NewGuid);

        // Act
        ArgumentException exception =
            Assert.Throws<ArgumentException>(
                () => store.CreateArtifact(
                    "document.txt:alternate"));

        // Assert
        Assert.Equal(
            "fileName",
            exception.ParamName);

        Assert.False(
            Directory.Exists(store.DirectoryPath));
    }

    /// <summary>
    /// Verifies the exact protected DACL required by the artifact store.
    /// </summary>
    private static void AssertAccessRules(
        FileSystemSecurity security,
        FileSystemRights expectedRights,
        InheritanceFlags expectedInheritanceFlags)
    {
        using WindowsIdentity identity =
            WindowsIdentity.GetCurrent();

        SecurityIdentifier currentUserSid =
            identity.User ??
            throw new InvalidOperationException(
                "The current Windows user identity is unavailable.");

        Assert.True(
            security.AreAccessRulesProtected);

        FileSystemAccessRule rule =
            Assert.Single(
                security
                    .GetAccessRules(
                        includeExplicit: true,
                        includeInherited: true,
                        targetType: typeof(SecurityIdentifier))
                    .Cast<FileSystemAccessRule>());

        Assert.Equal(
            currentUserSid,
            rule.IdentityReference);

        Assert.Equal(
            AccessControlType.Allow,
            rule.AccessControlType);

        Assert.Equal(
            expectedRights,
            rule.FileSystemRights);

        Assert.Equal(
            expectedInheritanceFlags,
            rule.InheritanceFlags);

        Assert.Equal(
            PropagationFlags.None,
            rule.PropagationFlags);
    }

    /// <summary>
    /// Provides an isolated filesystem root for artifact-store tests.
    /// </summary>
    private sealed class TemporaryRoot : IDisposable
    {
        public TemporaryRoot()
        {
            DirectoryPath =
                Path.Combine(
                    Path.GetTempPath(),
                    $"DeskVault.ArtifactStoreTests.{Guid.NewGuid():N}");

            Directory.CreateDirectory(DirectoryPath);
        }

        /// <summary>
        /// Gets the isolated directory path.
        /// </summary>
        public string DirectoryPath { get; }

        /// <summary>
        /// Removes the isolated test directory and its contents.
        /// </summary>
        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(
                    DirectoryPath,
                    recursive: true);
            }
        }
    }
}
