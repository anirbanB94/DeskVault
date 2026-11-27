using DeskVault.Application.Interfaces;
using DeskVault.Application.Security.Sessions;
using DeskVault.Domain.Documents;
using DeskVault.UI.Presenters;
using DeskVault.UI.Services.Interfaces;
using DeskVault.UI.Views;
using Moq;

namespace DeskVault.UI.Tests;

/// <summary>
/// Verifies ownership, borrowing, cancellation and disposal behaviour
/// of decrypted document source streams managed by the document presenter.
/// </summary>
public sealed partial class DocumentWorkspacePresenterTests
{
    /// <summary>
    /// Verifies that replacing a document defers disposal of its source stream
    /// until the outstanding external-viewing operation has completed.
    /// </summary>
    [Fact]
    public async Task OpenExternally_WhenDocumentIsReplaced_DefersOldStreamDisposalUntilViewerOperationCompletes()
    {
        // Arrange
        Guid firstDocumentId = Guid.NewGuid();
        Guid secondDocumentId = Guid.NewGuid();

        Document firstDocument =
            CreateDocument(
                firstDocumentId,
                "first-document.csv");

        Document secondDocument =
            CreateDocument(
                secondDocumentId,
                "second-document.csv");

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetByIdAsync(
                firstDocumentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(firstDocument);

        repository
            .Setup(x => x.GetByIdAsync(
                secondDocumentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(secondDocument);

        var view =
            new Mock<IDocumentView>();

        view
            .Setup(x => x.ShowDocumentAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var documentViewer =
            new Mock<IDocumentViewer>();

        var externalOpenStarted =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var externalOpenCompletion =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var firstStream =
            new TrackingDocumentStream();

        var secondStream =
            new TrackingDocumentStream();

        CancellationToken externalOpenToken = default;

        documentViewer
            .Setup(x => x.OpenAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns((
                Stream documentStream,
                string fileName,
                CancellationToken cancellationToken) =>
            {
                Assert.Same(
                    firstStream,
                    documentStream);

                Assert.Equal(
                    firstDocument.FileName,
                    fileName);

                externalOpenToken =
                    cancellationToken;

                externalOpenStarted.TrySetResult();

                return externalOpenCompletion.Task;
            });

        var storageService =
            new Mock<IStorageService>();

        DocumentWorkspacePresenter presenter =
            CreatePresenter(
                view,
                documentViewer,
                repository,
                storageService);

        try
        {
            // Act
            // Establish the first active document presentation.
            await presenter.OpenAsync(
                firstDocumentId,
                firstStream,
                firstDocument.FileName,
                TestContext.Current.CancellationToken);

            // Start an external-viewing operation that remains pending.
            view.Raise(
                x => x.OpenExternallyRequested += null,
                EventArgs.Empty);

            await externalOpenStarted.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);

            // The source stream must remain available while it is borrowed.
            Assert.True(firstStream.CanRead);
            Assert.Equal(0, firstStream.DisposeCount);

            // Replace the document while the external operation is pending.
            await presenter.OpenAsync(
                secondDocumentId,
                secondStream,
                secondDocument.FileName,
                TestContext.Current.CancellationToken);

            // The replacement invalidates and cancels the old external operation.
            Assert.True(
                externalOpenToken.IsCancellationRequested);

            // The old stream must not be disposed while the viewer still borrows it.
            Assert.True(firstStream.CanRead);
            Assert.Equal(0, firstStream.DisposeCount);

            // The replacement stream becomes the current presentation stream.
            Assert.Equal(
                secondDocumentId,
                presenter.CurrentDocumentId);

            Assert.True(secondStream.CanRead);
            Assert.Equal(0, secondStream.DisposeCount);

            // Complete the viewer operation after the original presentation
            // has been replaced. Its failure must not affect the new presentation.
            externalOpenCompletion.TrySetException(
                new IOException(
                    "External viewing failed after document replacement."));

            await firstStream.Disposed.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);

            // Assert
            Assert.False(firstStream.CanRead);
            Assert.Equal(1, firstStream.DisposeCount);

            Assert.Equal(
                secondDocumentId,
                presenter.CurrentDocumentId);

            Assert.True(secondStream.CanRead);

            view.Verify(
                x => x.ShowError(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<long>()),
                Times.Never);

            documentViewer.Verify(
                x => x.OpenAsync(
                    firstStream,
                    firstDocument.FileName,
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }
        finally
        {
            // Release the pending operation if an earlier assertion fails.
            externalOpenCompletion.TrySetCanceled(TestContext.Current.CancellationToken);

            presenter.Dispose();
        }

        await secondStream.Disposed.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, secondStream.DisposeCount);
    }

    /// <summary>
    /// Verifies that repeated external-viewing requests cannot concurrently
    /// consume the same current document source stream.
    /// </summary>
    [Fact]
    public async Task OpenExternally_ConcurrentRequestsForSameDocument_DoNotReadSourceStreamConcurrently()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();

        Document document =
            CreateDocument(
                documentId,
                "document.csv");

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetByIdAsync(
                documentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var view =
            new Mock<IDocumentView>();

        view
            .Setup(x => x.ShowDocumentAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var documentViewer =
            new Mock<IDocumentViewer>();

        var externalOpenStarted =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var externalOpenCompletion =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var stream =
            new TrackingDocumentStream();

        int externalOpenInvocationCount = 0;

        documentViewer
            .Setup(x => x.OpenAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns((
                Stream documentStream,
                string fileName,
                CancellationToken cancellationToken) =>
            {
                Assert.Same(
                    stream,
                    documentStream);

                Interlocked.Increment(
                    ref externalOpenInvocationCount);

                externalOpenStarted.TrySetResult();

                return externalOpenCompletion.Task;
            });

        var storageService =
            new Mock<IStorageService>();

        DocumentWorkspacePresenter presenter =
            CreatePresenter(
                view,
                documentViewer,
                repository,
                storageService);

        try
        {
            // Arrange the active document presentation.
            await presenter.OpenAsync(
                documentId,
                stream,
                document.FileName,
                TestContext.Current.CancellationToken);

            // Act
            // Start the first external-viewing operation.
            view.Raise(
                x => x.OpenExternallyRequested += null,
                EventArgs.Empty);

            await externalOpenStarted.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);

            // Request another external open while the first operation
            // is still borrowing the same source stream.
            view.Raise(
                x => x.OpenExternallyRequested += null,
                EventArgs.Empty);

            // Assert
            // Only one operation may consume the stream at a time.
            Assert.Equal(
                1,
                Volatile.Read(
                    ref externalOpenInvocationCount));

            Assert.True(stream.CanRead);
            Assert.Equal(0, stream.DisposeCount);

            documentViewer.Verify(
                x => x.OpenAsync(
                    stream,
                    document.FileName,
                    It.IsAny<CancellationToken>()),
                Times.Once);

            // Disposing the presenter requests cancellation of the borrower.
            presenter.Dispose();

            externalOpenCompletion.TrySetCanceled(TestContext.Current.CancellationToken);

            await stream.Disposed.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);

            Assert.False(stream.CanRead);
            Assert.Equal(1, stream.DisposeCount);
        }
        finally
        {
            externalOpenCompletion.TrySetCanceled(TestContext.Current.CancellationToken);

            presenter.Dispose();
        }
    }

    /// <summary>
    /// Verifies that disposing the presenter requests cancellation of an active
    /// external-viewing operation without prematurely disposing its source stream.
    /// </summary>
    [Fact]
    public async Task OpenExternally_WhenPresenterIsDisposed_DefersSourceStreamDisposalUntilViewerOperationCompletes()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();

        Document document =
            CreateDocument(
                documentId,
                "document.csv");

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetByIdAsync(
                documentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var view =
            new Mock<IDocumentView>();

        view
            .Setup(x => x.ShowDocumentAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var documentViewer =
            new Mock<IDocumentViewer>();

        var externalOpenStarted =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var externalOpenCompletion =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var stream =
            new TrackingDocumentStream();

        CancellationToken externalOpenToken = default;

        documentViewer
            .Setup(x => x.OpenAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns((
                Stream documentStream,
                string fileName,
                CancellationToken cancellationToken) =>
            {
                Assert.Same(
                    stream,
                    documentStream);

                externalOpenToken =
                    cancellationToken;

                externalOpenStarted.TrySetResult();

                // Keep the external operation pending until the test completes it.
                return externalOpenCompletion.Task;
            });

        var storageService =
            new Mock<IStorageService>();

        DocumentWorkspacePresenter presenter =
            CreatePresenter(
                view,
                documentViewer,
                repository,
                storageService);

        // Act
        await presenter.OpenAsync(
            documentId,
            stream,
            document.FileName,
            TestContext.Current.CancellationToken);

        view.Raise(
            x => x.OpenExternallyRequested += null,
            EventArgs.Empty);

        await externalOpenStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        Assert.True(stream.CanRead);
        Assert.Equal(0, stream.DisposeCount);

        presenter.Dispose();

        // Cancellation is requested, but the source remains available
        // until the external operation actually completes.
        Assert.True(externalOpenToken.IsCancellationRequested);
        Assert.True(stream.CanRead);
        Assert.Equal(0, stream.DisposeCount);

        externalOpenCompletion.TrySetCanceled(
            externalOpenToken);

        await stream.Disposed.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(stream.CanRead);
        Assert.Equal(1, stream.DisposeCount);

        // Repeated disposal must not dispose the source a second time.
        presenter.Dispose();

        Assert.Equal(1, stream.DisposeCount);
    }

    /// <summary>
    /// Verifies that security-session revocation cancels an external-viewing
    /// operation and defers source-stream disposal until that operation completes.
    /// </summary>
    [Fact]
    public async Task OpenExternally_WhenSessionIsRevoked_DefersStreamDisposalUntilViewerOperationCompletes()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();

        Document document =
            CreateDocument(
                documentId,
                "protected-document.csv");

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetByIdAsync(
                documentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var view =
            new Mock<IDocumentView>();

        view
            .Setup(x => x.ShowDocumentAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var postedActions = new List<Action>();

        view
            .Setup(x => x.PostToUi(
                It.IsAny<Action>()))
            .Callback<Action>(postedActions.Add);

        var documentViewer =
            new Mock<IDocumentViewer>();

        var externalOpenStarted =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var externalOpenCompletion =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var stream =
            new TrackingDocumentStream();

        CancellationToken externalOpenToken = default;

        documentViewer
            .Setup(x => x.OpenAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns((
                Stream documentStream,
                string fileName,
                CancellationToken cancellationToken) =>
            {
                Assert.Same(
                    stream,
                    documentStream);

                externalOpenToken =
                    cancellationToken;

                externalOpenStarted.TrySetResult();

                return externalOpenCompletion.Task;
            });

        ApplicationSecuritySessionSnapshot currentSnapshot =
            new(
                ApplicationSecuritySessionState.Authenticated,
                0);

        var securitySession =
            new Mock<IApplicationSecuritySession>();

        securitySession
            .SetupGet(x => x.CurrentSnapshot)
            .Returns(() => currentSnapshot);

        var storageService =
            new Mock<IStorageService>();

        DocumentWorkspacePresenter presenter =
            CreatePresenter(
                view,
                documentViewer,
                repository,
                storageService,
                applicationSecuritySession: securitySession);

        try
        {
            await presenter.OpenAsync(
                documentId,
                stream,
                document.FileName,
                TestContext.Current.CancellationToken);

            view.Raise(
                x => x.OpenExternallyRequested += null,
                EventArgs.Empty);

            await externalOpenStarted.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);

            Assert.True(stream.CanRead);
            Assert.Equal(0, stream.DisposeCount);

            // Act
            // Revoke the owning security session.
            currentSnapshot =
                new ApplicationSecuritySessionSnapshot(
                    ApplicationSecuritySessionState.Locked,
                    1);

            securitySession.Raise(
                x => x.StateChanged += null,
                currentSnapshot);

            Assert.True(externalOpenToken.IsCancellationRequested);

            Action postedCleanup = Assert.Single(postedActions);

            // Apply presentation cleanup through the owning UI dispatcher.
            postedCleanup();

            // The view must be cleared even though the external operation
            // has not yet completed its borrow of the source stream.
            view.Verify(
                x => x.ClearPresentation(),
                Times.Once);

            Assert.True(stream.CanRead);
            Assert.Equal(0, stream.DisposeCount);

            externalOpenCompletion.TrySetCanceled(
                externalOpenToken);

            await stream.Disposed.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);

            // Assert
            Assert.False(stream.CanRead);
            Assert.Equal(1, stream.DisposeCount);
        }
        finally
        {
            externalOpenCompletion.TrySetCanceled(TestContext.Current.CancellationToken);

            presenter.Dispose();
        }
    }

    /// <summary>
    /// Verifies that removing a document cancels an active external-viewing
    /// operation and defers source-stream disposal until that operation completes.
    /// </summary>
    [Fact]
    public async Task OpenExternally_WhenDocumentIsRemoved_DefersSourceStreamDisposalUntilViewerOperationCompletes()
    {
        // Arrange
        Guid documentId = Guid.NewGuid();

        Document document =
            CreateDocument(
                documentId,
                "document.csv");

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetByIdAsync(
                documentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        repository
            .Setup(x => x.DeleteAsync(
                documentId,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var storageService =
            new Mock<IStorageService>();

        storageService
            .Setup(x => x.DeleteAsync(
                document.Id,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var view =
            new Mock<IDocumentView>();

        view
            .Setup(x => x.ShowDocumentAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        view
            .Setup(x => x.ConfirmRemoval(
                document.FileName,
                It.IsAny<long>()))
            .Returns(true);

        var documentViewer =
            new Mock<IDocumentViewer>();

        var externalOpenStarted =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var externalOpenCompletion =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var documentRemoved =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var stream =
            new TrackingDocumentStream();

        CancellationToken externalOpenToken = default;

        documentViewer
            .Setup(x => x.OpenAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns((
                Stream documentStream,
                string fileName,
                CancellationToken cancellationToken) =>
            {
                Assert.Same(
                    stream,
                    documentStream);

                Assert.Equal(
                    document.FileName,
                    fileName);

                externalOpenToken =
                    cancellationToken;

                externalOpenStarted.TrySetResult();

                return externalOpenCompletion.Task;
            });

        DocumentWorkspacePresenter presenter =
            CreatePresenter(
                view,
                documentViewer,
                repository,
                storageService);

        presenter.DocumentRemoved +=
            (_, _) => documentRemoved.TrySetResult();

        try
        {
            // Act
            // Establish the current document presentation.
            await presenter.OpenAsync(
                documentId,
                stream,
                document.FileName,
                TestContext.Current.CancellationToken);

            // Start an external-viewing operation that remains pending.
            view.Raise(
                x => x.OpenExternallyRequested += null,
                EventArgs.Empty);

            await externalOpenStarted.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);

            // The external operation currently borrows the source stream.
            Assert.True(stream.CanRead);
            Assert.Equal(0, stream.DisposeCount);

            // Remove the document while the external operation is pending.
            view.Raise(
                x => x.RemoveDocumentRequested += null,
                EventArgs.Empty);

            await documentRemoved.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);

            // Removal must cancel the external operation.
            Assert.True(
                externalOpenToken.IsCancellationRequested);

            // The document presentation is cleared, but the borrowed
            // source stream must remain usable until the viewer completes.
            Assert.Null(presenter.CurrentDocumentId);
            Assert.True(stream.CanRead);
            Assert.Equal(0, stream.DisposeCount);

            repository.Verify(
                x => x.DeleteAsync(
                    documentId,
                    It.IsAny<CancellationToken>()),
                Times.Once);

            storageService.Verify(
                x => x.DeleteAsync(
                    document.Id,
                    It.IsAny<CancellationToken>()),
                Times.Once);

            view.Verify(
                x => x.ConfirmRemoval(
                    document.FileName,
                    It.IsAny<long>()),
                Times.Once);

            // Complete the cancelled external-viewing operation.
            externalOpenCompletion.TrySetCanceled(
                externalOpenToken);

            await stream.Disposed.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);

            // Assert
            Assert.False(stream.CanRead);
            Assert.Equal(1, stream.DisposeCount);

            view.Verify(
                x => x.ShowError(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<long>()),
                Times.Never);

            documentViewer.Verify(
                x => x.OpenAsync(
                    stream,
                    document.FileName,
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }
        finally
        {
            // Ensure the pending borrower can complete if an earlier
            // assertion fails, then release any remaining presenter state.
            presenter.Dispose();

            externalOpenCompletion.TrySetCanceled(
                TestContext.Current.CancellationToken);
        }
    }

    /// <summary>
    /// Verifies that presenter disposal waits for both the renderer and
    /// external viewer to release their borrows of the same source stream.
    /// </summary>
    [Fact]
    public async Task Dispose_WhenRendererAndExternalViewerBothBorrowSameStream_DefersDisposalUntilBothComplete()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        Guid documentId = Guid.NewGuid();

        Document document =
            CreateDocument(
                documentId,
                "document.csv");

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetByIdAsync(
                documentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var renderingStarted =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var renderingCompletion =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var externalOpenStarted =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var externalOpenCompletion =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var stream =
            new TrackingDocumentStream();

        CancellationToken renderingToken = default;
        CancellationToken externalOpenToken = default;

        var view =
            new Mock<IDocumentView>();

        view
            .Setup(x => x.ShowDocumentAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns((
                Stream documentStream,
                string fileName,
                CancellationToken token) =>
            {
                Assert.Same(stream, documentStream);
                Assert.Equal(document.FileName, fileName);

                renderingToken = token;
                renderingStarted.TrySetResult();

                // Keep the renderer borrow pending independently.
                return renderingCompletion.Task;
            });

        var documentViewer =
            new Mock<IDocumentViewer>();

        documentViewer
            .Setup(x => x.OpenAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns((
                Stream documentStream,
                string fileName,
                CancellationToken token) =>
            {
                Assert.Same(stream, documentStream);
                Assert.Equal(document.FileName, fileName);

                externalOpenToken = token;
                externalOpenStarted.TrySetResult();

                // Keep the external-open borrow pending independently.
                return externalOpenCompletion.Task;
            });

        var storageService =
            new Mock<IStorageService>();

        DocumentWorkspacePresenter presenter =
            CreatePresenter(
                view,
                documentViewer,
                repository,
                storageService);

        Task? openTask = null;

        try
        {
            // Start rendering and wait until the stream has been promoted.
            openTask =
                presenter.OpenAsync(
                    documentId,
                    stream,
                    document.FileName,
                    cancellationToken);

            await renderingStarted.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                cancellationToken);

            Assert.Equal(documentId, presenter.CurrentDocumentId);
            Assert.True(stream.CanRead);
            Assert.Equal(0, stream.DisposeCount);

            // Start external opening while rendering still borrows the same stream.
            view.Raise(
                x => x.OpenExternallyRequested += null,
                EventArgs.Empty);

            await externalOpenStarted.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                cancellationToken);

            Assert.True(renderingToken.CanBeCanceled);
            Assert.True(externalOpenToken.CanBeCanceled);
            Assert.True(stream.CanRead);
            Assert.Equal(0, stream.DisposeCount);

            // Dispose while both independent operations still borrow the stream.
            presenter.Dispose();

            Assert.True(renderingToken.IsCancellationRequested);
            Assert.True(externalOpenToken.IsCancellationRequested);
            Assert.True(stream.CanRead);
            Assert.Equal(0, stream.DisposeCount);

            // Complete rendering first. External opening still holds a borrow.
            renderingCompletion.TrySetResult();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => openTask!);

            Assert.True(stream.CanRead);
            Assert.Equal(0, stream.DisposeCount);

            // Complete external opening. This is the final outstanding borrow.
            externalOpenCompletion.TrySetCanceled(externalOpenToken);

            await stream.Disposed.WaitAsync(
                TimeSpan.FromSeconds(5),
                cancellationToken);

            Assert.False(stream.CanRead);
            Assert.Equal(1, stream.DisposeCount);

            presenter.Dispose();
            Assert.Equal(1, stream.DisposeCount);
        }
        finally
        {
            // Release both controlled operations if an earlier assertion fails.
            presenter.Dispose();
            renderingCompletion.TrySetResult();

            externalOpenCompletion.TrySetCanceled(
                externalOpenToken.CanBeCanceled
                    ? externalOpenToken
                    : cancellationToken);

            if (openTask is not null)
            {
                try
                {
                    await openTask;
                }
                catch (OperationCanceledException)
                {
                    // Expected after presenter disposal.
                }

                await stream.Disposed.WaitAsync(
                    TimeSpan.FromSeconds(5),
                    cancellationToken);
            }
        }
    }
    /// <summary>
    /// Provides a document source stream that records disposal calls
    /// and exposes a completion signal for deterministic lifecycle assertions.
    /// </summary>
    private sealed class TrackingDocumentStream : MemoryStream
    {
        private readonly TaskCompletionSource _disposed =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private int _disposeCount;

        /// <summary>
        /// Gets the number of times disposal has been requested.
        /// </summary>
        public int DisposeCount =>
            Volatile.Read(ref _disposeCount);

        /// <summary>
        /// Gets a task that completes when the stream is disposed.
        /// </summary>
        public Task Disposed =>
            _disposed.Task;

        /// <summary>
        /// Records disposal and releases the underlying memory stream.
        /// </summary>
        /// <param name="disposing">
        /// Indicates whether the method is called from explicit disposal.
        /// </param>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                Interlocked.Increment(
                    ref _disposeCount);

                _disposed.TrySetResult();
            }
        }
    }
}
