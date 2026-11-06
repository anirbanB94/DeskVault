using DeskVault.Application.Security.Sessions;
using DeskVault.UI.Views;
using Moq;
using static DeskVault.UI.Tests.PresentationSessionRevisionTestSupport;

namespace DeskVault.UI.Tests;

public sealed class DocumentWorkspacePresenterSessionRevisionTests
{
    [Fact]
    public void Activate_WhenOwningSessionRevisionIsSuperseded_DoesNotActivateDocument()
    {
        var session = new SecuritySessionHarness(
            ApplicationSecuritySessionState.Authenticated,
            revision: 40);
        var view = new Mock<IDocumentWorkspaceView>();
        var presenter = CreateDocumentWorkspacePresenter(
            view,
            session,
            securitySessionRevision: 40);

        session.Set(ApplicationSecuritySessionState.Locked, revision: 41);
        session.Set(ApplicationSecuritySessionState.Authenticated, revision: 42);

        presenter.Activate();

        view.Verify(
            x => x.ActivateWorkspace(),
            Times.Never);

        presenter.Dispose();
    }

    [Fact]
    public async Task OpenAsync_WhenOwningSessionRevisionIsSuperseded_RejectsAndDisposesIncomingStream()
    {
        Guid documentId = Guid.NewGuid();
        var session = new SecuritySessionHarness(
            ApplicationSecuritySessionState.Authenticated,
            revision: 50);
        var view = new Mock<IDocumentWorkspaceView>();
        var repository = new Mock<DeskVault.Application.Interfaces.IDocumentRepository>(MockBehavior.Strict);
        var presenter = CreateDocumentWorkspacePresenter(
            view,
            session,
            repository,
            securitySessionRevision: 50);
        var incomingStream = new MemoryStream([1, 2, 3, 4]);

        session.Set(ApplicationSecuritySessionState.Locked, revision: 51);
        session.Set(ApplicationSecuritySessionState.Authenticated, revision: 52);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => presenter.OpenAsync(
                documentId,
                incomingStream,
                "sensitive-document.txt",
                TestContext.Current.CancellationToken));

        Assert.False(incomingStream.CanRead);

        repository.Verify(
            x => x.GetByIdAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        view.Verify(
            x => x.ShowDocumentAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        presenter.Dispose();
    }
}
