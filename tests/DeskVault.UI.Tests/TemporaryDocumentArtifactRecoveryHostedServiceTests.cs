using DeskVault.UI.Hosting;
using DeskVault.UI.Services.Document;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text;

namespace DeskVault.UI.Tests;

/// <summary>
/// Verifies startup execution and ordering behaviour of temporary document artifact recovery.
/// </summary>
public sealed class TemporaryDocumentArtifactRecoveryHostedServiceTests
{
    /// <summary>
    /// Verifies that startup recovery completes before the hosted service
    /// reports startup completion.
    /// </summary>
    [Fact]
    public async Task StartAsync_ReconcilesAbandonedArtifactBeforeReturning()
    {
        // Arrange
        using var temporaryRoot = new TemporaryRoot();

        var store = new TemporaryDocumentArtifactStore(
            temporaryRoot.DirectoryPath,
            Guid.NewGuid);

        TemporaryDocumentArtifact artifact =
            store.CreateArtifact("document.txt");

        using var sourceStream = new MemoryStream(
            Encoding.UTF8.GetBytes("Abandoned temporary document."));

        await artifact.CopyFromAsync(
            sourceStream,
            TestContext.Current.CancellationToken);

        string artifactPath = artifact.FilePath;
        string ownershipMetadataPath = artifact.OwnershipMetadataPath;

        Assert.True(File.Exists(artifactPath));
        Assert.True(File.Exists(ownershipMetadataPath));

        // Simulate the previous process terminating after preparation.
        artifact.ReleaseOwnershipLease();

        var recoveryService =
            new TemporaryDocumentArtifactRecoveryService(
                store,
                NullLogger<TemporaryDocumentArtifactRecoveryService>.Instance);

        var hostedService =
            new TemporaryDocumentArtifactRecoveryHostedService(
                recoveryService,
                NullLogger<TemporaryDocumentArtifactRecoveryHostedService>.Instance);

        // Act
        await hostedService.StartAsync(
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(File.Exists(artifactPath));
        Assert.False(File.Exists(ownershipMetadataPath));

        await hostedService.StopAsync(
            TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Verifies that a repeated start call does not execute another recovery pass.
    /// </summary>
    [Fact]
    public async Task StartAsync_WhenCalledRepeatedly_RunsRecoveryOnlyOnce()
    {
        // Arrange
        using var temporaryRoot = new TemporaryRoot();

        var store = new TemporaryDocumentArtifactStore(
            temporaryRoot.DirectoryPath,
            Guid.NewGuid);

        TemporaryDocumentArtifact firstArtifact =
            store.CreateArtifact("first.txt");

        await PrepareArtifactAndReleaseLeaseAsync(
            firstArtifact,
            "First abandoned artifact.");

        var recoveryService =
            new TemporaryDocumentArtifactRecoveryService(
                store,
                NullLogger<TemporaryDocumentArtifactRecoveryService>.Instance);

        var hostedService =
            new TemporaryDocumentArtifactRecoveryHostedService(
                recoveryService,
                NullLogger<TemporaryDocumentArtifactRecoveryHostedService>.Instance);

        // Act
        await hostedService.StartAsync(
            TestContext.Current.CancellationToken);

        // Create another abandoned artifact after startup has completed.
        // A repeated StartAsync must not initiate another recovery pass.
        TemporaryDocumentArtifact secondArtifact =
            store.CreateArtifact("second.txt");

        await PrepareArtifactAndReleaseLeaseAsync(
            secondArtifact,
            "Second abandoned artifact.");

        await hostedService.StartAsync(
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(File.Exists(firstArtifact.FilePath));
        Assert.False(File.Exists(firstArtifact.OwnershipMetadataPath));

        Assert.True(File.Exists(secondArtifact.FilePath));
        Assert.True(File.Exists(secondArtifact.OwnershipMetadataPath));

        // A subsequent process performs its own recovery pass.
        var nextProcessRecoveryService =
            new TemporaryDocumentArtifactRecoveryService(
                store,
                NullLogger<TemporaryDocumentArtifactRecoveryService>.Instance);

        TemporaryDocumentArtifactRecoveryResult result =
            nextProcessRecoveryService.Recover();

        Assert.Equal(1, result.CleanedCount);
        Assert.False(File.Exists(secondArtifact.FilePath));
        Assert.False(File.Exists(secondArtifact.OwnershipMetadataPath));

        await hostedService.StopAsync(
            TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Verifies that startup cancellation prevents recovery from being started.
    /// </summary>
    [Fact]
    public async Task StartAsync_WhenAlreadyCancelled_DoesNotRunRecovery()
    {
        // Arrange
        using var temporaryRoot = new TemporaryRoot();

        var store = new TemporaryDocumentArtifactStore(
            temporaryRoot.DirectoryPath,
            Guid.NewGuid);

        TemporaryDocumentArtifact artifact =
            store.CreateArtifact("document.txt");

        await PrepareArtifactAndReleaseLeaseAsync(
            artifact,
            "Abandoned temporary document.");

        var recoveryService =
            new TemporaryDocumentArtifactRecoveryService(
                store,
                NullLogger<TemporaryDocumentArtifactRecoveryService>.Instance);

        var hostedService =
            new TemporaryDocumentArtifactRecoveryHostedService(
                recoveryService,
                NullLogger<TemporaryDocumentArtifactRecoveryHostedService>.Instance);

        using var cancellationSource =
            new CancellationTokenSource();

        cancellationSource.Cancel();

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => hostedService.StartAsync(cancellationSource.Token));

        // Assert
        Assert.True(File.Exists(artifact.FilePath));
        Assert.True(File.Exists(artifact.OwnershipMetadataPath));

        // A separate recovery pass cleans up the isolated test artifact.
        TemporaryDocumentArtifactRecoveryResult result =
            recoveryService.Recover();

        Assert.Equal(1, result.CleanedCount);
        Assert.False(File.Exists(artifact.FilePath));
        Assert.False(File.Exists(artifact.OwnershipMetadataPath));
    }

    /// <summary>
    /// Completes artifact materialization and releases the ownership lease
    /// to simulate an artifact left behind by a terminated process.
    /// </summary>
    /// <param name="artifact">The artifact to prepare.</param>
    /// <param name="content">The content to write before simulating termination.</param>
    private static async Task PrepareArtifactAndReleaseLeaseAsync(
        TemporaryDocumentArtifact artifact,
        string content)
    {
        using var sourceStream = new MemoryStream(
            Encoding.UTF8.GetBytes(content));

        await artifact.CopyFromAsync(
            sourceStream,
            TestContext.Current.CancellationToken);

        // CopyFromAsync closes the artifact write stream.
        // Releasing the ownership lease now accurately simulates process termination.
        artifact.ReleaseOwnershipLease();
    }

    /// <summary>
    /// Provides an isolated filesystem root for startup recovery tests.
    /// </summary>
    private sealed class TemporaryRoot : IDisposable
    {
        public TemporaryRoot()
        {
            DirectoryPath =
                Path.Combine(
                    Path.GetTempPath(),
                    $"DeskVault.ArtifactRecoveryStartupTests.{Guid.NewGuid():N}");

            Directory.CreateDirectory(DirectoryPath);
        }

        public string DirectoryPath { get; }

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
