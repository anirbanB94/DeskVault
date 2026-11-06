using DeskVault.Application.Configurations;
using DeskVault.Application.Documents.Commands.RemoveDocument;
using DeskVault.Application.Documents.Queries.GetDocument;
using DeskVault.Application.Documents.Queries.OpenDocument;
using DeskVault.Application.Interfaces;
using DeskVault.Application.Security.Sessions;
using DeskVault.Domain.Documents;
using DeskVault.UI.Presenters;
using DeskVault.UI.Services.Interfaces;
using DeskVault.UI.Views;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DeskVault.UI.Tests;

public sealed class DocumentWorkspacePresenterRemovalRevocationTests
{
    [Fact]
    public async Task RemoveDocument_WhenSessionIsRevokedDuringHandler_DoesNotPublishRemovalCompletion()
    {
        Guid documentId = Guid.NewGuid();
        Document document = Document.Create(
            documentId,
            "protected.csv",
            "Protected",
            "test-sha256",
            $"Data\\{documentId}\\protected.csv");

        var repository = new Mock<IDocumentRepository>();
        repository
            .Setup(x => x.GetByIdAsync(
                documentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var deleteStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var deleteCompletion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        repository
            .Setup(x => x.DeleteAsync(
                documentId,
                It.IsAny<CancellationToken>()))
            .Callback<Guid, CancellationToken>(
                (_, _) => deleteStarted.TrySetResult())
            .Returns(deleteCompletion.Task);

        var storageService = new Mock<IStorageService>();
        storageService
            .Setup(x => x.DeleteAsync(
                documentId,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var workspaceRepository = new Mock<IWorkspaceRepository>();
        var documentReader = new Mock<IDocumentReader>();
        var documentViewer = new Mock<IDocumentViewer>();
        var view = new Mock<IDocumentWorkspaceView>();

        var postedActions = new List<Action>();
        view
            .Setup(x => x.PostToUi(It.IsAny<Action>()))
            .Callback<Action>(postedActions.Add);

        view
            .Setup(x => x.ConfirmRemoval(document.FileName))
            .Returns(true);

        view
            .Setup(x => x.ShowDocumentAsync(
                It.IsAny<Stream>(),
                document.FileName,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        ApplicationSecuritySessionSnapshot currentSnapshot = new(
            ApplicationSecuritySessionState.Authenticated,
            0);

        var securitySession = new Mock<IApplicationSecuritySession>();
        securitySession
            .SetupGet(x => x.CurrentSnapshot)
            .Returns(() => currentSnapshot);

        var presenter = new DocumentWorkspacePresenter(
            Guid.NewGuid(),
            view.Object,
            documentViewer.Object,
            new GetDocumentHandler(
                repository.Object,
                NullLogger<GetDocumentHandler>.Instance),
            new OpenDocumentHandler(
                repository.Object,
                documentReader.Object,
                new DocumentProcessingOptions(),
                NullLogger<OpenDocumentHandler>.Instance),
            new RemoveDocumentHandler(
                repository.Object,
                workspaceRepository.Object,
                storageService.Object,
                NullLogger<RemoveDocumentHandler>.Instance),
            NullLogger<DocumentWorkspacePresenter>.Instance,
            securitySession.Object,
            securitySessionRevision: 0);

        int documentRemovedCount = 0;
        presenter.DocumentRemoved += (_, _) => documentRemovedCount++;

        var stream = new MemoryStream([1, 2, 3, 4]);

        await presenter.OpenAsync(
            documentId,
            stream,
            document.FileName,
            TestContext.Current.CancellationToken);

        view.Raise(
            x => x.RemoveDocumentRequested += null,
            EventArgs.Empty);

        await deleteStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        // The asynchronous removal is already in progress. Lock the session
        // while keeping the UI cleanup callback queued.
        currentSnapshot = new ApplicationSecuritySessionSnapshot(
            ApplicationSecuritySessionState.Locked,
            1);

        securitySession.Raise(
            x => x.StateChanged += null,
            currentSnapshot);

        Action cleanup = Assert.Single(postedActions);

        // Apply lock-time cleanup first. The presenter clears its retained
        // document but remains an object until its owning workspace is disposed.
        cleanup();

        view.Verify(
            x => x.ClearPresentation(),
            Times.Once);

        Assert.False(stream.CanRead);

        // Complete the removal only after the in-app presentation is cleared.
        deleteCompletion.TrySetResult();

        await Task.Delay(
            100,
            TestContext.Current.CancellationToken);

        Assert.Equal(0, documentRemovedCount);

        view.Verify(
            x => x.ClearPresentation(),
            Times.Once);

        view.Verify(
            x => x.ShowError(
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Never);

        presenter.Dispose();
    }
}
