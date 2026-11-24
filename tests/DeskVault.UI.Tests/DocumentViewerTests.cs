using DeskVault.UI.Services.Document;
using DeskVault.UI.Services.Interfaces;
using DeskVault.UI.Tests.TestDoubles;
using Microsoft.Extensions.Logging;
using System.Text;

namespace DeskVault.UI.Tests;

/// <summary>
/// Verifies protected artifact materialization and external-viewer lifecycle behavior.
/// </summary>
public sealed class DocumentViewerTests
{
    [Fact]
    public async Task OpenAsync_WhenExternalLaunchRemainsActive_KeepsTemporaryFile()
    {
        // Arrange
        var launcher = new TestExternalDocumentLauncher();

        using var fixture = new ViewerFixture(launcher);

        using var stream = CreateDocumentStream(
            "DeskVault document content.");

        // Act
        await fixture.Viewer.OpenAsync(
            stream,
            "document.txt",
            TestContext.Current.CancellationToken);

        // Assert
        string filePath = Assert.IsType<string>(launcher.FilePath);

        Assert.StartsWith(
            fixture.Store.DirectoryPath,
            filePath,
            StringComparison.OrdinalIgnoreCase);

        Assert.True(File.Exists(filePath));

        // Complete the external lifecycle before the fixture is disposed.
        launcher.Complete();

        await WaitForFileDeletionAsync(
            filePath,
            TestContext.Current.CancellationToken);

        Assert.False(File.Exists(filePath));
    }

    [Fact]
    public async Task Dispose_WhenExternalLifecycleRemainsActive_DoesNotDeleteTemporaryFile()
    {
        // Arrange
        var launcher = new TestExternalDocumentLauncher();

        using var fixture = new ViewerFixture(launcher);

        using var stream = CreateDocumentStream(
            "DeskVault document content.");

        // Act
        await fixture.Viewer.OpenAsync(
            stream,
            "document.txt",
            TestContext.Current.CancellationToken);

        string filePath = Assert.IsType<string>(launcher.FilePath);
        string ownershipMetadataPath = $"{filePath}.owner.json";

        Assert.True(File.Exists(filePath));
        Assert.True(File.Exists(ownershipMetadataPath));

        fixture.Viewer.Dispose();

        // Assert: an observable, still-active consumer retains both the artifact
        // and its exclusive process lease after viewer disposal.
        Assert.True(File.Exists(filePath));
        Assert.True(File.Exists(ownershipMetadataPath));

        Assert.Throws<IOException>(
            () =>
            {
                using var competingLease = new FileStream(
                    ownershipMetadataPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.None);
            });

        // Completing the external lifecycle makes cleanup eligible.
        launcher.Complete();

        await WaitForFileDeletionAsync(
            filePath,
            TestContext.Current.CancellationToken);

        Assert.False(File.Exists(filePath));
    }

    [Fact]
    public async Task OpenAsync_WhenViewerIsDisposedDuringFailedLaunch_CleansTemporaryFileAndOwnershipMetadata()
    {
        // Arrange
        DocumentViewer? viewer = null;

        var launcher = new TestExternalDocumentLauncher
        {
            LaunchException = new InvalidOperationException(
                "External launch failed before a launch handle was returned."),
            BeforeLaunch = _ => viewer!.Dispose()
        };

        using var fixture = new ViewerFixture(launcher);

        viewer = fixture.Viewer;

        using var stream = CreateDocumentStream(
            "DeskVault document content.");

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Viewer.OpenAsync(
                stream,
                "document.txt",
                TestContext.Current.CancellationToken));

        string filePath = Assert.IsType<string>(launcher.FilePath);
        string ownershipMetadataPath = $"{filePath}.owner.json";

        // Assert
        // Disposal occurred re-entrantly while Launch() was executing.
        // The unresolved launch state must have protected the ownership lease
        // until the failed-launch outcome could be persisted and cleaned up.
        Assert.False(File.Exists(filePath));
        Assert.False(File.Exists(ownershipMetadataPath));
    }

    [Fact]
    public async Task OpenAsync_WhenExternalLaunchCompletes_CleansTemporaryFile()
    {
        // Arrange
        var launcher = new TestExternalDocumentLauncher();

        using var fixture = new ViewerFixture(launcher);

        using var stream = CreateDocumentStream(
            "DeskVault document content.");

        // Act
        await fixture.Viewer.OpenAsync(
            stream,
            "document.txt",
            TestContext.Current.CancellationToken);

        string filePath = Assert.IsType<string>(launcher.FilePath);

        Assert.True(File.Exists(filePath));

        launcher.Complete();

        // Assert
        await WaitForFileDeletionAsync(
            filePath,
            TestContext.Current.CancellationToken);

        Assert.False(File.Exists(filePath));
    }

    [Fact]
    public async Task OpenAsync_WhenExternalLifecycleIsNotObservable_RetainsFileAfterViewerDisposed()
    {
        // Arrange
        var launcher = new TestExternalDocumentLauncher
        {
            ReturnUnobservableLaunch = true
        };

        using var fixture = new ViewerFixture(launcher);

        using var stream = CreateDocumentStream(
            "DeskVault document content.");

        // Act
        await fixture.Viewer.OpenAsync(
            stream,
            "document.txt",
            TestContext.Current.CancellationToken);

        string filePath = Assert.IsType<string>(launcher.FilePath);

        string ownershipMetadataPath = $"{filePath}.owner.json";

        // Act
        fixture.Viewer.Dispose();

        // Assert
        Assert.True(File.Exists(filePath));
        Assert.True(File.Exists(ownershipMetadataPath));

        TemporaryDocumentArtifactOwnershipRecord ownershipRecord =
            ReadRetainedOwnershipRecord(filePath);

        Assert.Equal(
            Path.GetFileName(filePath),
            ownershipRecord.ArtifactFileName);

        Assert.Equal(
            TemporaryDocumentArtifactLifecycleState.ExternalConsumerMayNeedArtifact,
            ownershipRecord.State);

        Assert.True(launcher.LaunchDisposed);

        // The test cleans up its isolated artifact and ownership record
        // after verifying retention and process-lease release.
        DeleteRetainedArtifactFiles(filePath);

        Assert.False(File.Exists(filePath));
        Assert.False(File.Exists(ownershipMetadataPath));
    }

    [Fact]
    public async Task OpenAsync_WhenExternalLifecycleFaults_RetainsFileAfterViewerDisposed()
    {
        // Arrange
        var launcher = new TestExternalDocumentLauncher();

        using var fixture = new ViewerFixture(launcher);

        using var stream = CreateDocumentStream(
            "DeskVault document content.");

        await fixture.Viewer.OpenAsync(
            stream,
            "document.txt",
            TestContext.Current.CancellationToken);

        string filePath = Assert.IsType<string>(launcher.FilePath);

        // Act
        launcher.Fail(
            new InvalidOperationException(
                "External lifecycle observation failed."));

        await WaitForConditionAsync(
            () => fixture.Logger.Entries.Any(
                entry =>
                    entry.Level == LogLevel.Warning &&
                    entry.Message.Contains(
                        "External document lifecycle observation failed",
                        StringComparison.Ordinal)),
            TestContext.Current.CancellationToken);

        string ownershipMetadataPath = $"{filePath}.owner.json";

        // Act
        fixture.Viewer.Dispose();

        // Assert
        Assert.True(File.Exists(filePath));
        Assert.True(File.Exists(ownershipMetadataPath));

        TemporaryDocumentArtifactOwnershipRecord ownershipRecord =
            ReadRetainedOwnershipRecord(filePath);

        Assert.Equal(
            Path.GetFileName(filePath),
            ownershipRecord.ArtifactFileName);

        Assert.Equal(
            TemporaryDocumentArtifactLifecycleState.ExternalConsumerMayNeedArtifact,
            ownershipRecord.State);

        Assert.True(launcher.LaunchDisposed);

        // The test cleans up its isolated artifact and ownership record
        // after verifying retention and process-lease release.
        DeleteRetainedArtifactFiles(filePath);

        Assert.False(File.Exists(filePath));
        Assert.False(File.Exists(ownershipMetadataPath));
    }

    [Fact]
    public async Task OpenAsync_WhenExternalLifecycleIsCancelled_RetainsFileAfterViewerDisposed()
    {
        // Arrange
        var launcher = new TestExternalDocumentLauncher();

        using var fixture = new ViewerFixture(launcher);

        using var stream = CreateDocumentStream(
            "DeskVault document content.");

        await fixture.Viewer.OpenAsync(
            stream,
            "document.txt",
            TestContext.Current.CancellationToken);

        string filePath = Assert.IsType<string>(launcher.FilePath);

        // Act
        launcher.Cancel();

        await WaitForConditionAsync(
            () => fixture.Logger.Entries.Any(
                entry =>
                    entry.Level == LogLevel.Warning &&
                    entry.Message.Contains(
                        "External document lifecycle observation failed",
                        StringComparison.Ordinal)),
            TestContext.Current.CancellationToken);

        string ownershipMetadataPath = $"{filePath}.owner.json";

        // Act
        fixture.Viewer.Dispose();

        // Assert
        Assert.True(File.Exists(filePath));
        Assert.True(File.Exists(ownershipMetadataPath));

        TemporaryDocumentArtifactOwnershipRecord ownershipRecord =
            ReadRetainedOwnershipRecord(filePath);

        Assert.Equal(
            Path.GetFileName(filePath),
            ownershipRecord.ArtifactFileName);

        Assert.Equal(
            TemporaryDocumentArtifactLifecycleState.ExternalConsumerMayNeedArtifact,
            ownershipRecord.State);

        Assert.True(launcher.LaunchDisposed);

        // The test cleans up its isolated artifact and ownership record
        // after verifying retention and process-lease release.
        DeleteRetainedArtifactFiles(filePath);

        Assert.False(File.Exists(filePath));
        Assert.False(File.Exists(ownershipMetadataPath));
    }

    [Fact]
    public async Task OpenAsync_WritesCompleteDocumentContentToTemporaryFile()
    {
        // Arrange
        const string expectedContent =
            """
            DeskVault document content.
            Multiple lines are preserved.
            """;

        var launcher = new TestExternalDocumentLauncher();

        using var fixture = new ViewerFixture(launcher);

        using var stream = CreateDocumentStream(expectedContent);

        // Act
        await fixture.Viewer.OpenAsync(
            stream,
            "document.txt",
            TestContext.Current.CancellationToken);

        string filePath = Assert.IsType<string>(launcher.FilePath);

        // Assert
        string actualContent = await File.ReadAllTextAsync(
            filePath,
            TestContext.Current.CancellationToken);

        Assert.Equal(expectedContent, actualContent);

        launcher.Complete();

        await WaitForFileDeletionAsync(
            filePath,
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task OpenAsync_ClosesWriteHandleBeforeExternalLaunch()
    {
        // Arrange
        const string expectedContent =
            """
            DeskVault document content.
            Multiple lines are preserved.
            """;

        string? contentAtLaunch = null;

        var launcher = new TestExternalDocumentLauncher
        {
            BeforeLaunch = path =>
            {
                // An exclusive read open cannot coexist with the artifact's
                // write handle. This callback runs immediately before launch.
                using var exclusiveReadHandle = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.None);

                using var reader = new StreamReader(
                    exclusiveReadHandle,
                    Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: true);

                contentAtLaunch = reader.ReadToEnd();
            }
        };

        using var fixture = new ViewerFixture(launcher);

        using var stream = CreateDocumentStream(expectedContent);

        // Act
        await fixture.Viewer.OpenAsync(
            stream,
            "document.txt",
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expectedContent, contentAtLaunch);

        string filePath = Assert.IsType<string>(launcher.FilePath);

        Assert.True(File.Exists(filePath));

        launcher.Complete();

        await WaitForFileDeletionAsync(
            filePath,
            TestContext.Current.CancellationToken);

        Assert.False(File.Exists(filePath));
    }

    [Fact]
    public async Task OpenAsync_WhenCopyIsCancelled_CleansTemporaryFile()
    {
        // Arrange
        var launcher = new TestExternalDocumentLauncher();

        using var fixture = new ViewerFixture(launcher);

        using var stream = new BlockingReadStream(
            "DeskVault document content.");

        using var cancellationTokenSource =
            new CancellationTokenSource();

        Task openTask = fixture.Viewer.OpenAsync(
            stream,
            "document.txt",
            cancellationTokenSource.Token);

        await stream.ReadStarted.WaitAsync(
            TimeSpan.FromSeconds(3),
            TestContext.Current.CancellationToken);

        string filePath = Assert.Single(
            Directory.GetFiles(
                fixture.Store.DirectoryPath,
                "*.txt"));

        Assert.True(File.Exists(filePath));

        // Act
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => openTask);

        // Assert
        Assert.False(File.Exists(filePath));
        Assert.Null(launcher.FilePath);
    }

    [Fact]
    public async Task OpenAsync_WhenArtifactIdentityCollidesAcrossExtensions_CleansRejectedArtifactWithoutReplacingActiveOwner()
    {
        // Arrange
        Guid artifactId = Guid.NewGuid();

        var launcher = new TestExternalDocumentLauncher();

        using var fixture = new ViewerFixture(
            launcher,
            () => artifactId);

        using var firstStream = CreateDocumentStream(
            "First active document.");

        await fixture.Viewer.OpenAsync(
            firstStream,
            "first.txt",
            TestContext.Current.CancellationToken);

        string firstPath = Assert.IsType<string>(launcher.FilePath);

        string rejectedArtifactPath = Path.Combine(
            fixture.Store.DirectoryPath,
            $"{artifactId:N}.md");

        using var secondStream = CreateDocumentStream(
            "Second rejected document.");

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Viewer.OpenAsync(
                secondStream,
                "second.md",
                TestContext.Current.CancellationToken));

        // Assert
        Assert.True(File.Exists(firstPath));
        Assert.False(File.Exists(rejectedArtifactPath));

        // Complete the original owner's lifecycle and verify its cleanup.
        launcher.Complete();

        await WaitForFileDeletionAsync(
            firstPath,
            TestContext.Current.CancellationToken);

        Assert.False(File.Exists(firstPath));
    }

    [Fact]
    public async Task OpenAsync_WhenExternalLaunchFails_CleansTemporaryFile()
    {
        // Arrange
        var launcher = new TestExternalDocumentLauncher
        {
            LaunchException = new InvalidOperationException(
                "External launch failed before a launch handle was returned.")
        };

        using var fixture = new ViewerFixture(launcher);

        using var stream = CreateDocumentStream(
            "DeskVault document content.");

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Viewer.OpenAsync(
                stream,
                "document.txt",
                TestContext.Current.CancellationToken));

        string filePath = Assert.IsType<string>(launcher.FilePath);
        string ownershipMetadataPath = $"{filePath}.owner.json";

        // Assert: a launch exception before a handle is returned is a known
        // failed launch, so both the plaintext artifact and ownership record
        // are cleaned up rather than retained indefinitely as an external use.
        Assert.False(File.Exists(filePath));
        Assert.False(File.Exists(ownershipMetadataPath));
    }

    [Fact]
    public async Task OpenAsync_WhenCompletionAccessFailsAfterLaunchHandleReturned_RetainsArtifactForRecovery()
    {
        // Arrange
        var launcher = new TestExternalDocumentLauncher
        {
            CompletionGetterException = new InvalidOperationException(
                "The returned launch handle could not expose its completion task.")
        };

        using var fixture = new ViewerFixture(launcher);

        using var stream = CreateDocumentStream(
            "DeskVault document content.");

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Viewer.OpenAsync(
                stream,
                "document.txt",
                TestContext.Current.CancellationToken));

        string filePath = Assert.IsType<string>(launcher.FilePath);
        string ownershipMetadataPath = $"{filePath}.owner.json";

        // The launcher returned a handle before completion inspection failed.
        // This is the ambiguous-handoff case and must remain conservative.
        Assert.True(launcher.LaunchDisposed);
        Assert.True(File.Exists(filePath));
        Assert.True(File.Exists(ownershipMetadataPath));

        // The viewer still holds the process lease while it owns the artifact.
        Assert.Throws<IOException>(
            () =>
            {
                using var competingLease = new FileStream(
                    ownershipMetadataPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.None);
            });

        // Disposal releases the process lease but must not delete the file or
        // downgrade the durable external-consumer state.
        fixture.Viewer.Dispose();

        Assert.True(File.Exists(filePath));
        Assert.True(File.Exists(ownershipMetadataPath));

        TemporaryDocumentArtifactOwnershipRecord ownershipRecord =
            ReadRetainedOwnershipRecord(filePath);

        Assert.Equal(
            Path.GetFileName(filePath),
            ownershipRecord.ArtifactFileName);

        Assert.Equal(
            TemporaryDocumentArtifactLifecycleState.ExternalConsumerMayNeedArtifact,
            ownershipRecord.State);

        // ReadRetainedOwnershipRecord opens the sidecar exclusively. Its
        // success proves that viewer disposal released the process lease.
        DeleteRetainedArtifactFiles(filePath);

        Assert.False(File.Exists(filePath));
        Assert.False(File.Exists(ownershipMetadataPath));
    }

    [Fact]
    public async Task OpenAsync_DoesNotDisposeCallerOwnedStream()
    {
        // Arrange
        var launcher = new TestExternalDocumentLauncher();

        using var fixture = new ViewerFixture(launcher);

        using var stream = CreateDocumentStream(
            "DeskVault document content.");

        // Act
        await fixture.Viewer.OpenAsync(
            stream,
            "document.txt",
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(stream.CanRead);

        stream.Position = 0;

        Assert.Equal(0, stream.Position);

        // Complete the external lifecycle so the test does not leave
        // an outstanding launch observer or active temporary artifact.
        string filePath = Assert.IsType<string>(launcher.FilePath);

        launcher.Complete();

        await WaitForFileDeletionAsync(
            filePath,
            TestContext.Current.CancellationToken);

        Assert.False(File.Exists(filePath));
    }

    [Fact]
    public async Task Dispose_WhenCleanupPreviouslyFailed_RetriesAndLogsFailure()
    {
        // Arrange
        var launcher = new TestExternalDocumentLauncher();

        using var fixture = new ViewerFixture(launcher);

        using var stream = CreateDocumentStream(
            "DeskVault document content.");

        await fixture.Viewer.OpenAsync(
            stream,
            "document.txt",
            TestContext.Current.CancellationToken);

        string filePath = Assert.IsType<string>(launcher.FilePath);

        using (var fileLock = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.None))
        {
            // Act: the completion callback attempts deletion while the file is locked.
            launcher.Complete();

            await WaitForConditionAsync(
                () => fixture.Logger.Entries.Any(
                    entry =>
                        entry.Level == LogLevel.Warning &&
                        entry.Message.Contains(
                            "Temporary document artifact cleanup failed",
                            StringComparison.Ordinal)),
                TestContext.Current.CancellationToken);

            // Assert: failed cleanup must not lose ownership or imply deletion.
            Assert.True(File.Exists(filePath));

            RecordedLogEntry cleanupEntry =
                Assert.Single(fixture.Logger.Entries, entry =>
                    entry.Message.Contains(
                        "Temporary document artifact cleanup failed",
                        StringComparison.Ordinal));

            Assert.Equal(LogLevel.Warning, cleanupEntry.Level);
            Assert.DoesNotContain(filePath, cleanupEntry.Message, StringComparison.OrdinalIgnoreCase);
        }

        // Act: disposal retries cleanup after the blocking handle is released.
        fixture.Viewer.Dispose();

        // Assert
        Assert.False(File.Exists(filePath));
    }

    [Fact]
    public async Task OpenAsync_WhenSourceReadFails_CleansTemporaryFile()
    {
        // Arrange
        var launcher = new TestExternalDocumentLauncher();

        using var fixture = new ViewerFixture(launcher);

        using var stream = new FaultingReadStream();

        // Act
        await Assert.ThrowsAsync<IOException>(
            () => fixture.Viewer.OpenAsync(
                stream,
                "document.txt",
                TestContext.Current.CancellationToken));

        // Assert
        Assert.Empty(
            Directory.GetFiles(
                fixture.Store.DirectoryPath,
                "*.txt"));

        Assert.Null(launcher.FilePath);
    }

    [Fact]
    public async Task Dispose_WhenOpenIsStillMaterializing_DefersCleanupUntilOperationStops()
    {
        // Arrange
        var launcher = new TestExternalDocumentLauncher();

        using var fixture = new ViewerFixture(launcher);

        using var stream = new BlockingReadStream(
            "DeskVault document content.");

        Task openTask = fixture.Viewer.OpenAsync(
            stream,
            "document.txt",
            TestContext.Current.CancellationToken);

        await stream.ReadStarted.WaitAsync(
            TimeSpan.FromSeconds(3),
            TestContext.Current.CancellationToken);

        string filePath = Assert.Single(
            Directory.GetFiles(
                fixture.Store.DirectoryPath,
                "*.txt"));

        // Act
        fixture.Viewer.Dispose();

        // Assert: an in-progress owner must not lose its artifact prematurely.
        Assert.True(File.Exists(filePath));

        // Allow materialization to finish. The disposed viewer must reject the launch
        // and the OpenAsync failure path must complete artifact cleanup.
        stream.ReleaseRead();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => openTask);

        Assert.False(File.Exists(filePath));
        Assert.Null(launcher.FilePath);
    }

    [Fact]
    public async Task OpenAsync_WhenAlreadyCancelled_DoesNotCreateArtifact()
    {
        // Arrange
        var launcher = new TestExternalDocumentLauncher();

        using var fixture = new ViewerFixture(launcher);

        using var stream = CreateDocumentStream(
            "DeskVault document content.");

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => fixture.Viewer.OpenAsync(
                stream,
                "document.txt",
                cancellationTokenSource.Token));

        // Assert
        Assert.False(
            Directory.Exists(fixture.Store.DirectoryPath));

        Assert.Null(launcher.FilePath);
    }

    [Fact]
    public async Task Dispose_WhenArtifactDeletionFails_RetainsOwnershipForRetry()
    {
        // Arrange
        var launcher = new TestExternalDocumentLauncher();

        using var fixture = new ViewerFixture(launcher);

        using var stream = CreateDocumentStream(
            "DeskVault document content.");

        await fixture.Viewer.OpenAsync(
            stream,
            "document.txt",
            TestContext.Current.CancellationToken);

        string filePath = Assert.IsType<string>(launcher.FilePath);

        using (var fileLock = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.None))
        {
            // Establish a completed, observable lifecycle while cleanup is blocked.
            launcher.Complete();

            await WaitForConditionAsync(
                () =>
                    launcher.LaunchDisposed &&
                    fixture.Logger.Entries.Any(
                        entry =>
                            entry.Level == LogLevel.Warning &&
                            entry.Message.Contains(
                                "Temporary document artifact cleanup failed",
                                StringComparison.Ordinal)),
                TestContext.Current.CancellationToken);

            // Act: disposal retries cleanup but must retain ownership on failure.
            fixture.Viewer.Dispose();

            // Assert: the locked artifact remains and the cleanup failure is observable.
            Assert.True(File.Exists(filePath));

            Assert.Contains(
                fixture.Logger.Entries,
                entry =>
                    entry.Level == LogLevel.Warning &&
                    entry.Message.Contains(
                        "Temporary document artifact cleanup failed",
                        StringComparison.Ordinal));
        }

        // Act: repeated disposal retries the retained artifact after the lock is released.
        fixture.Viewer.Dispose();

        // Assert
        Assert.False(File.Exists(filePath));
    }

    /// <summary>
    /// Reads a retained ownership record while requiring the process lease
    /// to have been released.
    /// </summary>
    /// <param name="artifactPath">The retained temporary artifact path.</param>
    /// <returns>The latest validated ownership record.</returns>
    private static TemporaryDocumentArtifactOwnershipRecord ReadRetainedOwnershipRecord(
        string artifactPath)
    {
        string ownershipMetadataPath =
            $"{artifactPath}.owner.json";

        using var ownershipStream = new FileStream(
            ownershipMetadataPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.None);

        return TemporaryDocumentArtifactOwnership.ReadLatestRecord(
            ownershipStream);
    }

    /// <summary>
    /// Removes an isolated retained artifact and its ownership metadata.
    /// </summary>
    /// <param name="artifactPath">The retained temporary artifact path.</param>
    private static void DeleteRetainedArtifactFiles(
        string artifactPath)
    {
        File.Delete(artifactPath);

        File.Delete(
            $"{artifactPath}.owner.json");
    }

    private static MemoryStream CreateDocumentStream(string content)
    {
        return new MemoryStream(
            Encoding.UTF8.GetBytes(content));
    }

    private static async Task WaitForFileDeletionAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        await WaitForConditionAsync(
            () => !File.Exists(filePath),
            cancellationToken);
    }

    private static async Task WaitForConditionAsync(
        Func<bool> condition,
        CancellationToken cancellationToken)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(3);

        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(
                TimeSpan.FromMilliseconds(20),
                cancellationToken);
        }

        Assert.True(
            condition(),
            "The expected asynchronous lifecycle condition was not reached.");
    }

    /// <summary>
    /// Supplies isolated storage and diagnostic services for a viewer test.
    /// </summary>
    private sealed class ViewerFixture : IDisposable
    {
        private readonly string _rootPath;

        public ViewerFixture(TestExternalDocumentLauncher launcher, Func<Guid>? identityFactory = null)
        {
            _rootPath = Path.Combine(
                Path.GetTempPath(),
                $"DeskVault.DocumentViewerTests.{Guid.NewGuid():N}");

            Directory.CreateDirectory(_rootPath);

            Store = new TemporaryDocumentArtifactStore(
                _rootPath,
                identityFactory ?? (() => Guid.NewGuid()));

            Logger = new RecordingLogger<DocumentViewer>();

            Viewer = new DocumentViewer(
                launcher,
                Store,
                Logger);
        }

        public TemporaryDocumentArtifactStore Store { get; }

        public RecordingLogger<DocumentViewer> Logger { get; }

        public DocumentViewer Viewer { get; }

        public void Dispose()
        {
            Viewer.Dispose();

            // Do not let recursive test-directory deletion hide a leaked artifact.
            bool hasRemainingFiles =
                Directory.Exists(_rootPath) &&
                Directory.EnumerateFiles(
                    _rootPath,
                    "*",
                    SearchOption.AllDirectories).Any();

            if (hasRemainingFiles)
            {
                throw new InvalidOperationException(
                    "The document viewer test fixture still owns temporary artifacts.");
            }

            if (Directory.Exists(_rootPath))
            {
                Directory.Delete(
                    _rootPath,
                    recursive: true);
            }
        }
    }

    private sealed class TestExternalDocumentLauncher :
        IExternalDocumentLauncher
    {
        private readonly TaskCompletionSource<object?> _completionSource =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string? FilePath { get; private set; }

        public Exception? LaunchException { get; init; }

        public Exception? CompletionGetterException { get; init; }

        public bool ReturnUnobservableLaunch { get; init; }

        public bool LaunchDisposed { get; private set; }

        public Action<string>? BeforeLaunch { get; init; }

        public IExternalDocumentLaunch Launch(string filePath)
        {
            FilePath = filePath;

            BeforeLaunch?.Invoke(filePath);

            if (LaunchException is not null)
            {
                throw LaunchException;
            }

            if (ReturnUnobservableLaunch)
            {
                return new TestExternalDocumentLaunch(
                    completion: null,
                    onDispose: () => LaunchDisposed = true);
            }

            return new TestExternalDocumentLaunch(
                _completionSource.Task,
                onDispose: () => LaunchDisposed = true,
                completionGetterException: CompletionGetterException);
        }

        public void Complete()
        {
            _completionSource.TrySetResult(null);
        }

        public void Fail(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);

            _completionSource.TrySetException(exception);
        }

        public void Cancel()
        {
            _completionSource.TrySetCanceled();
        }
    }

    private sealed class TestExternalDocumentLaunch :
        IExternalDocumentLaunch
    {
        private readonly Task? _completion;
        private readonly Action _onDispose;
        private readonly Exception? _completionGetterException;

        public TestExternalDocumentLaunch(
            Task? completion,
            Action onDispose,
            Exception? completionGetterException = null)
        {
            _completion = completion;
            _onDispose = onDispose;
            _completionGetterException = completionGetterException;
        }

        public Task? Completion
        {
            get
            {
                if (_completionGetterException is not null)
                {
                    throw _completionGetterException;
                }

                return _completion;
            }
        }

        public void Dispose()
        {
            _onDispose();
        }
    }

    /// <summary>
    /// Simulates a source stream that fails during asynchronous document copying.
    /// </summary>
    private sealed class FaultingReadStream : MemoryStream
    {
        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromException<int>(
                new IOException("Simulated source read failure."));
        }
    }

    /// <summary>
    /// Blocks the first asynchronous read to exercise disposal during materialization.
    /// </summary>
    private sealed class BlockingReadStream : MemoryStream
    {
        private readonly TaskCompletionSource<object?> _readStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<object?> _releaseRead =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public BlockingReadStream(string content)
            : base(Encoding.UTF8.GetBytes(content))
        {
        }

        public Task ReadStarted => _readStarted.Task;

        public void ReleaseRead()
        {
            _releaseRead.TrySetResult(null);
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            _readStarted.TrySetResult(null);

            await _releaseRead.Task.WaitAsync(
                cancellationToken);

            return await base.ReadAsync(
                buffer,
                cancellationToken);
        }
    }
}
