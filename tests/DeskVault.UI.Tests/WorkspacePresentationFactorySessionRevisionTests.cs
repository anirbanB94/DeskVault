using DeskVault.Application.Security.Sessions;
using DeskVault.Domain.Workspaces;
using DeskVault.UI.Services.Interfaces;
using DeskVault.UI.Services.Workspace;
using Moq;
using static DeskVault.UI.Tests.PresentationSessionRevisionTestSupport;

namespace DeskVault.UI.Tests;

public sealed class WorkspacePresentationFactorySessionRevisionTests
{
    [Fact]
    public void Create_WhenSuppliedRevisionIsOlderThanCurrentSession_PreservesSuppliedRevision()
    {
        var session = new SecuritySessionHarness(
            ApplicationSecuritySessionState.Authenticated,
            revision: 60);
        var documentFactory = new Mock<IWorkspaceDocumentPresentationFactory>(MockBehavior.Strict);
        var factory = CreateWorkspacePresentationFactory(
            session.Session.Object,
            documentFactory);
        WorkspacePresentation presentation = factory.Create(
            Guid.NewGuid(),
            WorkspaceType.Persistent,
            "Workspace",
            null,
            securitySessionRevision: 59);

        // The presentation remains bound to revision 59. Step 2 additionally
        // disposes it immediately because the current session is already at 60.
        // Therefore its public operation now reports disposal rather than the
        // older operation-level authorization cancellation.
        Assert.Throws<ObjectDisposedException>(
            () => presentation.OpenDocument(Guid.NewGuid()));

        documentFactory.Verify(
            x => x.Create(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<long>()),
            Times.Never);

        presentation.Dispose();
    }

    [Fact]
    public async Task Create_WhenSuppliedRevisionIsOlderThanCurrentSession_ChildDocumentPresenterRetainsThatRevision()
    {
        var session = new SecuritySessionHarness(
            ApplicationSecuritySessionState.Authenticated,
            revision: 70);
        var repository = new Mock<DeskVault.Application.Interfaces.IDocumentRepository>(MockBehavior.Strict);
        WorkspaceDocumentPresentationFactory factory =
            CreateWorkspaceDocumentPresentationFactory(
                session.Session.Object,
                repository);

        WorkspaceDocumentPresentation presentation = factory.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            securitySessionRevision: 69);

        var incomingStream = new MemoryStream([9, 8, 7]);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => presentation.Presenter.OpenAsync(
                presentation.DocumentId,
                incomingStream,
                "revision-bound.txt",
                TestContext.Current.CancellationToken));

        Assert.False(incomingStream.CanRead);
        repository.Verify(
            x => x.GetByIdAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        presentation.Dispose();
    }
}
