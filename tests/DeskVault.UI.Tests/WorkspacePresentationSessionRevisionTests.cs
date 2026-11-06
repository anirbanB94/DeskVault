using DeskVault.Application.Security.Sessions;
using DeskVault.Domain.Workspaces;
using DeskVault.UI.Services.Interfaces;
using DeskVault.UI.Services.Workspace;
using DeskVault.UI.Views;
using Moq;
using static DeskVault.UI.Tests.PresentationSessionRevisionTestSupport;

namespace DeskVault.UI.Tests;

public sealed class WorkspacePresentationSessionRevisionTests
{
    [Fact]
    public void Activate_WhenSessionIsReauthenticatedAtNewRevision_DoesNotActivateOldWorkspace()
    {
        Guid workspaceId = Guid.NewGuid();
        var session = new SecuritySessionHarness(
            ApplicationSecuritySessionState.Authenticated,
            revision: 10);
        var view = new Mock<IWorkspacePresentationView>();
        WorkspacePresentation presentation = CreateWorkspacePresentation(
            workspaceId,
            session,
            revision: 10,
            view: view);

        session.Set(ApplicationSecuritySessionState.Locked, revision: 11);
        session.Set(ApplicationSecuritySessionState.Authenticated, revision: 12);

        bool activated = presentation.Activate();

        Assert.False(activated);
        view.Verify(
            x => x.ActivateWorkspace(),
            Times.Never);

        presentation.Dispose();
    }

    [Fact]
    public void OpenDocument_WhenWorkspaceBelongsToOlderRevision_RejectsBeforeCreatingDocumentPresentation()
    {
        Guid workspaceId = Guid.NewGuid();
        Guid documentId = Guid.NewGuid();
        var session = new SecuritySessionHarness(
            ApplicationSecuritySessionState.Authenticated,
            revision: 20);
        var documentFactory = new Mock<IWorkspaceDocumentPresentationFactory>(MockBehavior.Strict);
        WorkspacePresentation presentation = CreateWorkspacePresentation(
            workspaceId,
            session,
            revision: 20,
            documentPresentationFactory: documentFactory);

        session.Set(ApplicationSecuritySessionState.Locked, revision: 21);
        session.Set(ApplicationSecuritySessionState.Authenticated, revision: 22);

        Assert.Throws<OperationCanceledException>(
            () => presentation.OpenDocument(documentId));

        documentFactory.Verify(
            x => x.Create(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<long>()),
            Times.Never);

        presentation.Dispose();
    }

    [Fact]
    public async Task SetWorkspaceDocumentsAsync_WhenWorkspaceBelongsToOlderRevision_DoesNotPublishMetadata()
    {
        Guid workspaceId = Guid.NewGuid();
        Guid documentId = Guid.NewGuid();
        var session = new SecuritySessionHarness(
            ApplicationSecuritySessionState.Authenticated,
            revision: 30);
        var view = new Mock<IWorkspacePresentationView>();
        WorkspacePresentation presentation = CreateWorkspacePresentation(
            workspaceId,
            session,
            revision: 30,
            view: view);

        session.Set(ApplicationSecuritySessionState.Locked, revision: 31);
        session.Set(ApplicationSecuritySessionState.Authenticated, revision: 32);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => presentation.SetWorkspaceDocumentsAsync(
                [documentId],
                TestContext.Current.CancellationToken));

        view.Verify(
            x => x.SetWorkspaceDocuments(
                It.IsAny<IReadOnlyDictionary<Guid, string>>()),
            Times.Never);

        presentation.Dispose();
    }

    [Fact]
    public void SaveAsWorkspaceRequested_WhenOwningRevisionIsSuperseded_DoesNotShowDialog()
    {
        var session = new SecuritySessionHarness(
            ApplicationSecuritySessionState.Authenticated,
            revision: 80);

        var view = new Mock<IWorkspacePresentationView>();
        var dialogService =
            new Mock<IWorkspaceDialogService>(MockBehavior.Strict);

        WorkspacePresentation presentation =
            CreateWorkspacePresentation(
                Guid.NewGuid(),
                session,
                revision: 80,
                view: view,
                workspaceType: WorkspaceType.Temporary,
                dialogService: dialogService);

        // Simulate re-authentication without processing the old
        // presentation's queued UI-disposal notification.
        session.Set(
            ApplicationSecuritySessionState.Authenticated,
            revision: 82);

        view.Raise(
            x => x.SaveAsWorkspaceRequested += null,
            EventArgs.Empty);

        dialogService.Verify(
            x => x.ShowWorkspaceDetails(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<long>()),
            Times.Never);

        presentation.Dispose();
    }

    [Fact]
    public void UpdateWorkspaceDetailsRequested_WhenOwningRevisionIsSuperseded_DoesNotShowDialog()
    {
        var session = new SecuritySessionHarness(
            ApplicationSecuritySessionState.Authenticated,
            revision: 90);

        var view = new Mock<IWorkspacePresentationView>();
        var dialogService =
            new Mock<IWorkspaceDialogService>(MockBehavior.Strict);

        WorkspacePresentation presentation =
            CreateWorkspacePresentation(
                Guid.NewGuid(),
                session,
                revision: 90,
                view: view,
                dialogService: dialogService);

        session.Set(
            ApplicationSecuritySessionState.Authenticated,
            revision: 92);

        view.Raise(
            x => x.UpdateWorkspaceDetailsRequested += null,
            EventArgs.Empty);

        dialogService.Verify(
            x => x.ShowWorkspaceDetails(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<long>()),
            Times.Never);

        presentation.Dispose();
    }

    [Fact]
    public void AddDocumentsRequested_WhenOwningRevisionIsSuperseded_DoesNotShowPicker()
    {
        var session = new SecuritySessionHarness(
            ApplicationSecuritySessionState.Authenticated,
            revision: 100);

        var view = new Mock<IWorkspacePresentationView>();
        var dialogService =
            new Mock<IWorkspaceDialogService>(MockBehavior.Strict);

        WorkspacePresentation presentation =
            CreateWorkspacePresentation(
                Guid.NewGuid(),
                session,
                revision: 100,
                view: view,
                dialogService: dialogService);

        session.Set(
            ApplicationSecuritySessionState.Authenticated,
            revision: 102);

        view.Raise(
            x => x.AddDocumentsRequested += null,
            EventArgs.Empty);

        dialogService.Verify(
            x => x.ShowDocumentPicker(
                It.IsAny<IReadOnlyList<DeskVault.Domain.Documents.Document>>(),
                It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<long>()),
            Times.Never);

        presentation.Dispose();
    }

    [Fact]
    public void RemoveDocumentsRequested_WhenOwningRevisionIsSuperseded_DoesNotShowRemovalDialog()
    {
        var session = new SecuritySessionHarness(
            ApplicationSecuritySessionState.Authenticated,
            revision: 110);

        var view = new Mock<IWorkspacePresentationView>();
        var dialogService =
            new Mock<IWorkspaceDialogService>(MockBehavior.Strict);

        WorkspacePresentation presentation =
            CreateWorkspacePresentation(
                Guid.NewGuid(),
                session,
                revision: 110,
                view: view,
                dialogService: dialogService);

        session.Set(
            ApplicationSecuritySessionState.Authenticated,
            revision: 112);

        view.Raise(
            x => x.RemoveDocumentsRequested += null,
            EventArgs.Empty);

        dialogService.Verify(
            x => x.ShowDocumentRemoval(
                It.IsAny<IReadOnlyList<DeskVault.Domain.Documents.Document>>(),
                It.IsAny<long>()),
            Times.Never);

        presentation.Dispose();
    }

    [Fact]
    public void DeleteWorkspaceRequested_WhenOwningRevisionIsSuperseded_DoesNotForwardRequest()
    {
        var session = new SecuritySessionHarness(
            ApplicationSecuritySessionState.Authenticated,
            revision: 120);

        var view = new Mock<IWorkspacePresentationView>();

        WorkspacePresentation presentation =
            CreateWorkspacePresentation(
                Guid.NewGuid(),
                session,
                revision: 120,
                view: view);

        int requestCount = 0;

        presentation.WorkspaceDeleteRequested +=
            (_, _) => requestCount++;

        session.Set(
            ApplicationSecuritySessionState.Authenticated,
            revision: 122);

        view.Raise(
            x => x.DeleteWorkspaceRequested += null,
            EventArgs.Empty);

        Assert.Equal(0, requestCount);

        presentation.Dispose();
    }

    [Fact]
    public void CloseWorkspaceRequested_WhenOwningRevisionIsSuperseded_DoesNotForwardRequest()
    {
        var session = new SecuritySessionHarness(
            ApplicationSecuritySessionState.Authenticated,
            revision: 130);

        var view = new Mock<IWorkspacePresentationView>();

        WorkspacePresentation presentation =
            CreateWorkspacePresentation(
                Guid.NewGuid(),
                session,
                revision: 130,
                view: view);

        int requestCount = 0;

        presentation.WorkspaceCloseRequested +=
            (_, _) => requestCount++;

        session.Set(
            ApplicationSecuritySessionState.Authenticated,
            revision: 132);

        view.Raise(
            x => x.CloseWorkspaceRequested += null,
            EventArgs.Empty);

        Assert.Equal(0, requestCount);

        presentation.Dispose();
    }

    [Fact]
    public void CloseDocumentRequested_WhenOwningRevisionIsSuperseded_DoesNotForwardRequest()
    {
        var session = new SecuritySessionHarness(
            ApplicationSecuritySessionState.Authenticated,
            revision: 140);

        var view = new Mock<IWorkspacePresentationView>();

        WorkspacePresentation presentation =
            CreateWorkspacePresentation(
                Guid.NewGuid(),
                session,
                revision: 140,
                view: view,
                workspaceType: WorkspaceType.Temporary);

        int requestCount = 0;

        presentation.WorkspaceCloseRequested +=
            (_, _) => requestCount++;

        session.Set(
            ApplicationSecuritySessionState.Authenticated,
            revision: 142);

        view.Raise(
            x => x.CloseDocumentRequested += null,
            EventArgs.Empty);

        Assert.Equal(0, requestCount);

        presentation.Dispose();
    }
}
