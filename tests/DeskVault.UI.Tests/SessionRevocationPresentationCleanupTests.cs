using DeskVault.Application.Configurations;
using DeskVault.Application.Documents.Commands.RemoveDocument;
using DeskVault.Application.Documents.Queries.GetDocument;
using DeskVault.Application.Documents.Queries.ListDocuments;
using DeskVault.Application.Documents.Queries.OpenDocument;
using DeskVault.Application.Interfaces;
using DeskVault.Application.Security.Sessions;
using DeskVault.Application.Workspaces.Commands.AddDocumentToWorkspace;
using DeskVault.Application.Workspaces.Commands.RemoveDocumentFromWorkspace;
using DeskVault.Application.Workspaces.Commands.RenameWorkspace;
using DeskVault.Application.Workspaces.Commands.SaveTemporaryWorkspace;
using DeskVault.Application.Workspaces.Commands.UpdateWorkspaceDescription;
using DeskVault.Domain.Documents;
using DeskVault.Domain.Workspaces;
using DeskVault.UI.Presenters;
using DeskVault.UI.Services.Interfaces;
using DeskVault.UI.Services.Workspace;
using DeskVault.UI.Views;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Text;

namespace DeskVault.UI.Tests;

public sealed class SessionRevocationPresentationCleanupTests
{
    [Fact]
    public async Task DocumentPresenter_WhenOwningSessionIsRevoked_ClearsViewAndDisposesRetainedStreamOnUiDispatch()
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

        var workspaceRepository = new Mock<IWorkspaceRepository>();
        var storageService = new Mock<IStorageService>();
        var documentReader = new Mock<IDocumentReader>();
        var documentViewer = new Mock<IDocumentViewer>();
        var view = new Mock<IDocumentWorkspaceView>();

        var postedActions = new List<Action>();
        view
            .Setup(x => x.PostToUi(It.IsAny<Action>()))
            .Callback<Action>(postedActions.Add);

        view
            .Setup(x => x.ShowDocumentAsync(
                It.IsAny<Stream>(),
                document.FileName,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        ApplicationSecuritySessionSnapshot currentSnapshot =
            new(
                ApplicationSecuritySessionState.Authenticated,
                0);

        var securitySession =
            new Mock<IApplicationSecuritySession>();

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
            0L);

        var stream = new MemoryStream(
            Encoding.UTF8.GetBytes("Id,Name\r\n1,Alice\r\n"));

        await presenter.OpenAsync(
            documentId,
            stream,
            document.FileName,
            TestContext.Current.CancellationToken);

        Assert.True(stream.CanRead);

        // The notification invalidates operations immediately, but UI cleanup
        // remains queued until the owning view dispatcher is invoked.
        currentSnapshot =
            new ApplicationSecuritySessionSnapshot(
                ApplicationSecuritySessionState.Locked,
                1);

        securitySession.Raise(
            x => x.StateChanged += null,
            currentSnapshot);

        Assert.Single(postedActions);

        view.Verify(
            x => x.ClearPresentation(),
            Times.Never);

        Assert.True(stream.CanRead);

        // Apply the posted cleanup on the view's owning UI thread.
        postedActions[0]();

        view.Verify(
            x => x.ClearPresentation(),
            Times.Once);

        Assert.False(stream.CanRead);

        presenter.Dispose();
    }

    [Fact]
    public void WorkspacePresentation_WhenOwningSessionIsRevoked_DisposesProtectedWorkspaceOnUiDispatch()
    {
        var view = new Mock<IWorkspacePresentationView>();
        var postedActions = new List<Action>();

        view
            .Setup(x => x.PostToUi(It.IsAny<Action>()))
            .Callback<Action>(postedActions.Add);

        var documentRepository =
            new Mock<IDocumentRepository>();

        var workspaceRepository =
            new Mock<IWorkspaceRepository>();

        var activeWorkspaceRegistry =
            new Mock<IActiveWorkspaceRegistry>();

        var documentPresentationFactory =
            new Mock<IWorkspaceDocumentPresentationFactory>();

        var dialogService =
            new Mock<IWorkspaceDialogService>();

        var securitySession =
            new Mock<IApplicationSecuritySession>();

        ApplicationSecuritySessionSnapshot currentSnapshot =
            new(
                ApplicationSecuritySessionState.Authenticated,
                0);

        securitySession
            .SetupGet(x => x.CurrentSnapshot)
            .Returns(() => currentSnapshot);

        var presentation = new WorkspacePresentation(
            Guid.NewGuid(),
            WorkspaceType.Persistent,
            "Protected Workspace",
            "Sensitive workspace description",
            documentPresentationFactory.Object,
            new ListDocumentsHandler(
                documentRepository.Object,
                NullLogger<ListDocumentsHandler>.Instance),
            new AddDocumentToWorkspaceHandler(
                documentRepository.Object,
                workspaceRepository.Object,
                activeWorkspaceRegistry.Object,
                NullLogger<AddDocumentToWorkspaceHandler>.Instance),
            new RemoveDocumentFromWorkspaceHandler(
                workspaceRepository.Object,
                activeWorkspaceRegistry.Object,
                NullLogger<RemoveDocumentFromWorkspaceHandler>.Instance),
            new RenameWorkspaceHandler(
                workspaceRepository.Object,
                activeWorkspaceRegistry.Object,
                NullLogger<RenameWorkspaceHandler>.Instance),
            new SaveTemporaryWorkspaceHandler(
                workspaceRepository.Object,
                activeWorkspaceRegistry.Object,
                NullLogger<SaveTemporaryWorkspaceHandler>.Instance),
            new UpdateWorkspaceDescriptionHandler(
                workspaceRepository.Object,
                activeWorkspaceRegistry.Object,
                NullLogger<UpdateWorkspaceDescriptionHandler>.Instance),
            dialogService.Object,
            view.Object,
            NullLogger<WorkspacePresentation>.Instance,
            securitySession.Object,
            0L);

        Assert.Equal(
            "Protected Workspace",
            presentation.WorkspaceName);

        currentSnapshot =
            new ApplicationSecuritySessionSnapshot(
                ApplicationSecuritySessionState.Locked,
                1);

        securitySession.Raise(
            x => x.StateChanged += null,
            currentSnapshot);

        Assert.Single(postedActions);

        view.Verify(
            x => x.ClearPresentation(),
            Times.Never);

        // Workspace disposal occurs only when its owning UI dispatcher runs.
        postedActions[0]();

        Assert.Null(presentation.WorkspaceName);
        Assert.Null(presentation.WorkspaceDescription);

        view.Verify(
            x => x.ClearPresentation(),
            Times.Once);

        view.Verify(
            x => x.Dispose(),
            Times.Once);

        Assert.Throws<ObjectDisposedException>(
            () => presentation.Activate());
    }
}
