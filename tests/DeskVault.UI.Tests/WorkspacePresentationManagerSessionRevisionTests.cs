using DeskVault.Application.Security.Sessions;
using DeskVault.Domain.Workspaces;
using DeskVault.UI.Services.Interfaces;
using DeskVault.UI.Services.Workspace;
using DeskVault.UI.Views;
using Moq;
using static DeskVault.UI.Tests.PresentationSessionRevisionTestSupport;

namespace DeskVault.UI.Tests;

public sealed class WorkspacePresentationManagerSessionRevisionTests
{
    [Fact]
    public void GetOrCreate_WhenExpectedRevisionIsStale_RejectsBeforeFactoryCreation()
    {
        Guid workspaceId = Guid.NewGuid();
        var session = new SecuritySessionHarness(
            ApplicationSecuritySessionState.Authenticated,
            revision: 7);
        var factory = new Mock<IWorkspacePresentationFactory>(MockBehavior.Strict);
        var manager = new WorkspacePresentationManager(
            factory.Object,
            session.Session.Object);

        Assert.Throws<OperationCanceledException>(
            () => manager.GetOrCreate(
                workspaceId,
                WorkspaceType.Persistent,
                "Workspace",
                null,
                expectedSessionRevision: 6));

        factory.Verify(
            x => x.Create(
                It.IsAny<Guid>(),
                It.IsAny<WorkspaceType>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<long>()),
            Times.Never);
    }

    [Fact]
    public void GetOrCreate_WhenExistingPresentationBelongsToOlderRevision_DisposesItAndCreatesFreshPresentation()
    {
        Guid workspaceId = Guid.NewGuid();
        var session = new SecuritySessionHarness(
            ApplicationSecuritySessionState.Authenticated,
            revision: 1);

        var oldView = new Mock<IWorkspacePresentationView>();
        WorkspacePresentation oldPresentation = CreateWorkspacePresentation(
            workspaceId,
            session,
            revision: 1,
            view: oldView);

        var newView = new Mock<IWorkspacePresentationView>();
        WorkspacePresentation newPresentation = CreateWorkspacePresentation(
            workspaceId,
            session,
            revision: 3,
            view: newView);

        var factory = new Mock<IWorkspacePresentationFactory>(MockBehavior.Strict);
        factory
            .Setup(x => x.Create(
                workspaceId,
                WorkspaceType.Persistent,
                "Workspace",
                null,
                1))
            .Returns(oldPresentation);
        factory
            .Setup(x => x.Create(
                workspaceId,
                WorkspaceType.Persistent,
                "Workspace",
                null,
                3))
            .Returns(newPresentation);

        var manager = new WorkspacePresentationManager(
            factory.Object,
            session.Session.Object);

        WorkspacePresentation first = manager.GetOrCreate(
            workspaceId,
            WorkspaceType.Persistent,
            "Workspace",
            null,
            expectedSessionRevision: 1);

        Assert.Same(oldPresentation, first);

        // Act: successful re-authentication creates a new authorized revision.
        session.Set(ApplicationSecuritySessionState.Locked, revision: 2);
        session.Set(ApplicationSecuritySessionState.Authenticated, revision: 3);

        WorkspacePresentation replacement = manager.GetOrCreate(
            workspaceId,
            WorkspaceType.Persistent,
            "Workspace",
            null,
            expectedSessionRevision: 3);

        Assert.Same(newPresentation, replacement);
        Assert.NotSame(oldPresentation, replacement);
        Assert.True(manager.Contains(workspaceId));

        Assert.Throws<ObjectDisposedException>(
            () => oldPresentation.Activate());

        oldView.Verify(
            x => x.ClearPresentation(),
            Times.Once);

        factory.Verify(
            x => x.Create(workspaceId, WorkspaceType.Persistent, "Workspace", null, 1),
            Times.Once);
        factory.Verify(
            x => x.Create(workspaceId, WorkspaceType.Persistent, "Workspace", null, 3),
            Times.Once);

        manager.Dispose();
    }

    [Fact]
    public void Activate_WhenManagedPresentationBelongsToOlderRevision_ReturnsFalse()
    {
        Guid workspaceId = Guid.NewGuid();
        var session = new SecuritySessionHarness(
            ApplicationSecuritySessionState.Authenticated,
            revision: 4);
        var view = new Mock<IWorkspacePresentationView>();
        WorkspacePresentation presentation = CreateWorkspacePresentation(
            workspaceId,
            session,
            revision: 4,
            view: view);

        var factory = new Mock<IWorkspacePresentationFactory>();
        factory
            .Setup(x => x.Create(
                workspaceId,
                WorkspaceType.Persistent,
                "Workspace",
                null,
                4))
            .Returns(presentation);

        var manager = new WorkspacePresentationManager(
            factory.Object,
            session.Session.Object);

        manager.GetOrCreate(
            workspaceId,
            WorkspaceType.Persistent,
            "Workspace",
            null,
            expectedSessionRevision: 4);

        view.Invocations.Clear();

        session.Set(ApplicationSecuritySessionState.Locked, revision: 5);
        session.Set(ApplicationSecuritySessionState.Authenticated, revision: 6);

        bool activated = manager.Activate(workspaceId);

        Assert.False(activated);
        view.Verify(
            x => x.ActivateWorkspace(),
            Times.Never);

        manager.Dispose();
    }
}
