using DeskVault.UI.Services.Document;
using DeskVault.UI.Services.Interfaces;

namespace DeskVault.UI.Tests;

public sealed class DocumentViewerTests
{
    [Fact]
    public async Task OpenAsync_WhenExternalLaunchRemainsActive_KeepsTemporaryFile()
    {
        // Arrange
        var launcher =
            new TestExternalDocumentLauncher();

        var viewer =
            new DocumentViewer(
                launcher);

        using var stream =
            CreateDocumentStream(
                "DeskVault document content.");

        // Act
        await viewer.OpenAsync(
            stream,
            "document.txt");

        // Assert
        Assert.NotNull(
            launcher.FilePath);

        Assert.True(
            File.Exists(
                launcher.FilePath));

        // Cleanup
        DeleteFileIfExists(
            launcher.FilePath);
    }

    [Fact]
    public async Task Dispose_WhenExternalLifecycleRemainsActive_DoesNotDeleteTemporaryFile()
    {
        // Arrange
        var launcher =
            new TestExternalDocumentLauncher();

        var viewer =
            new DocumentViewer(
                launcher);

        using var stream =
            CreateDocumentStream(
                "DeskVault document content.");

        // Act
        await viewer.OpenAsync(
            stream,
            "document.txt");

        string filePath =
            Assert.IsType<string>(
                launcher.FilePath);

        Assert.True(
            File.Exists(
                filePath));

        viewer.Dispose();

        // Assert
        Assert.True(
            File.Exists(
                filePath));

        // Complete the external lifecycle.
        launcher.Complete();

        await WaitForFileDeletionAsync(
            filePath);

        Assert.False(
            File.Exists(
                filePath));
    }

    [Fact]
    public async Task OpenAsync_WhenExternalLaunchCompletes_CleansTemporaryFile()
    {
        // Arrange
        var launcher =
            new TestExternalDocumentLauncher();

        var viewer =
            new DocumentViewer(
                launcher);

        using var stream =
            CreateDocumentStream(
                "DeskVault document content.");

        // Act
        await viewer.OpenAsync(
            stream,
            "document.txt");

        string filePath =
            Assert.IsType<string>(
                launcher.FilePath);

        Assert.True(
            File.Exists(
                filePath));

        launcher.Complete();

        // Assert
        await WaitForFileDeletionAsync(
            filePath);

        Assert.False(
            File.Exists(
                filePath));
    }

    [Fact]
    public async Task OpenAsync_WhenExternalLifecycleIsNotObservable_RetainsFileUntilViewerDisposed()
    {
        // Arrange
        var launcher =
            new TestExternalDocumentLauncher
            {
                ReturnUnobservableLaunch = true
            };

        using var viewer =
            new DocumentViewer(
                launcher);

        using var stream =
            CreateDocumentStream(
                "DeskVault document content.");

        // Act
        await viewer.OpenAsync(
            stream,
            "document.txt");

        // Assert
        string filePath =
            Assert.IsType<string>(
                launcher.FilePath);

        Assert.True(
            File.Exists(
                filePath));

        Assert.True(
            launcher.LaunchDisposed);

        // Act
        viewer.Dispose();

        // Assert
        await WaitForFileDeletionAsync(
            filePath);

        Assert.False(
            File.Exists(
                filePath));
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

        var launcher =
            new TestExternalDocumentLauncher();

        using var viewer =
            new DocumentViewer(
                launcher);

        using var stream =
            CreateDocumentStream(
                expectedContent);

        // Act
        await viewer.OpenAsync(
            stream,
            "document.txt");

        string filePath =
            Assert.IsType<string>(
                launcher.FilePath);

        // Assert
        string actualContent =
            await File.ReadAllTextAsync(
                filePath);

        Assert.Equal(
            expectedContent,
            actualContent);

        // Cleanup
        launcher.Complete();

        await WaitForFileDeletionAsync(
            filePath);
    }

    [Fact]
    public async Task OpenAsync_WhenCopyIsCancelled_CleansTemporaryFile()
    {
        // Arrange
        var launcher =
            new TestExternalDocumentLauncher();

        using var viewer =
            new DocumentViewer(
                launcher);

        using var stream =
            CreateDocumentStream(
                "DeskVault document content.");

        const string fileName =
            "document.deskvault-test";

        string tempPath =
            Path.GetTempPath();

        HashSet<string> filesBefore =
            Directory
                .GetFiles(
                    tempPath,
                    "*.deskvault-test")
                .ToHashSet(
                    StringComparer.OrdinalIgnoreCase);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                viewer.OpenAsync(
                    stream,
                    fileName,
                    cancellationTokenSource.Token));

        // Assert
        HashSet<string> filesAfter =
            Directory
                .GetFiles(
                    tempPath,
                    "*.deskvault-test")
                .ToHashSet(
                    StringComparer.OrdinalIgnoreCase);

        Assert.Empty(
            filesAfter.Except(
                filesBefore,
                StringComparer.OrdinalIgnoreCase));

        Assert.Null(
            launcher.FilePath);
    }

    [Fact]
    public async Task OpenAsync_WhenExternalLaunchFails_CleansTemporaryFile()
    {
        // Arrange
        var launcher =
            new TestExternalDocumentLauncher
            {
                LaunchException =
                    new InvalidOperationException(
                        "External launch failed.")
            };

        var viewer =
            new DocumentViewer(
                launcher);

        using var stream =
            CreateDocumentStream(
                "DeskVault document content.");

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                viewer.OpenAsync(
                    stream,
                    "document.txt"));

        // Assert
        Assert.NotNull(
            launcher.FilePath);

        Assert.False(
            File.Exists(
                launcher.FilePath));
    }

    [Fact]
    public async Task OpenAsync_DoesNotDisposeCallerOwnedStream()
    {
        // Arrange
        var launcher =
            new TestExternalDocumentLauncher();

        var viewer =
            new DocumentViewer(
                launcher);

        using var stream =
            CreateDocumentStream(
                "DeskVault document content.");

        // Act
        await viewer.OpenAsync(
            stream,
            "document.txt");

        // Assert
        stream.Position = 0;

        Assert.Equal(
            0,
            stream.Position);

        // Cleanup
        DeleteFileIfExists(
            launcher.FilePath);
    }

    private static MemoryStream CreateDocumentStream(
        string content)
    {
        return new MemoryStream(
            System.Text.Encoding.UTF8.GetBytes(
                content));
    }

    private static async Task WaitForFileDeletionAsync(
        string filePath)
    {
        DateTime deadline =
            DateTime.UtcNow.AddSeconds(2);

        while (
            File.Exists(filePath) &&
            DateTime.UtcNow < deadline)
        {
            await Task.Delay(
                25);
        }
    }

    private static void DeleteFileIfExists(
        string? filePath)
    {
        if (
            !string.IsNullOrWhiteSpace(filePath) &&
            File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }

    private sealed class TestExternalDocumentLauncher :
        IExternalDocumentLauncher
    {
        private readonly TaskCompletionSource<object?> _completionSource =
            new(
                TaskCreationOptions.RunContinuationsAsynchronously);

        public string? FilePath { get; private set; }

        public Exception? LaunchException { get; init; }

        public bool ReturnUnobservableLaunch { get; init; }

        public bool LaunchDisposed { get; private set; }

        public IExternalDocumentLaunch Launch(
            string filePath)
        {
            FilePath = filePath;

            if (LaunchException is not null)
            {
                throw LaunchException;
            }

            if (ReturnUnobservableLaunch)
            {
                return new TestExternalDocumentLaunch(
                    completion: null,
                    onDispose: () =>
                        LaunchDisposed = true);
            }

            return new TestExternalDocumentLaunch(
                _completionSource.Task,
                onDispose: () =>
                    LaunchDisposed = true);
        }

        public void Complete()
        {
            _completionSource.SetResult(
                null);
        }
    }

    private sealed class TestExternalDocumentLaunch :
        IExternalDocumentLaunch
    {
        private readonly Action _onDispose;

        public TestExternalDocumentLaunch(
            Task? completion,
            Action onDispose)
        {
            Completion = completion;
            _onDispose = onDispose;
        }

        public Task? Completion { get; }

        public void Dispose()
        {
            _onDispose();
        }
    }
}
