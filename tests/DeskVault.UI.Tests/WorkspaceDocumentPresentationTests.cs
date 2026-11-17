using DeskVault.Application.Configurations;
using DeskVault.Application.Documents.Commands.RemoveDocument;
using DeskVault.Application.Documents.Queries.GetDocument;
using DeskVault.Application.Documents.Queries.OpenDocument;
using DeskVault.Application.Interfaces;
using DeskVault.Application.Security.Sessions;
using DeskVault.UI.Forms;
using DeskVault.UI.Presenters;
using DeskVault.UI.Rendering;
using DeskVault.UI.Services.Interfaces;
using DeskVault.UI.Services.Workspace;
using DeskVault.UI.Views;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DeskVault.UI.Tests;

public sealed class WorkspaceDocumentPresentationTests
{
    [Fact]
    public void Constructor_StoresDocumentIdPresenterAndView()
    {
        Guid documentId = Guid.NewGuid();

        DocumentWorkspacePresenter presenter =
            CreatePresenter();

        var view =
            new Mock<IDocumentView>();

        WorkspaceDocumentPresentation presentation =
            new(
                documentId,
                presenter,
                view.Object);

        Assert.Equal(
            documentId,
            presentation.DocumentId);

        Assert.Same(
            presenter,
            presentation.Presenter);

        Assert.Same(
            view.Object,
            presentation.View);

        Assert.Null(
            presentation.DisplayName);

        Assert.Null(
            presentation.FileName);
    }

    [Fact]
    public void Constructor_ThrowsWhenPresenterIsNull()
    {
        var view =
            new Mock<IDocumentView>();

        Assert.Throws<ArgumentNullException>(
            () => new WorkspaceDocumentPresentation(
                Guid.NewGuid(),
                null!,
                view.Object));
    }

    [Fact]
    public void Constructor_ThrowsWhenViewIsNull()
    {
        DocumentWorkspacePresenter presenter =
            CreatePresenter();

        Assert.Throws<ArgumentNullException>(
            () => new WorkspaceDocumentPresentation(
                Guid.NewGuid(),
                presenter,
                null!));
    }

    [Fact]
    public void HostedForm_ReturnsSameInstanceAsDocumentView()
    {
        // Arrange
        var view = new DocumentViewForm(
            new Mock<IDocumentContentRendererResolver>().Object,
            new Mock<ISessionBoundDialogCoordinator>().Object);

        using WorkspaceDocumentPresentation presentation =
            new(
                Guid.NewGuid(),
                CreatePresenter(view),
                view);

        // Act
        Form hostedForm = presentation.HostedForm;

        // Assert
        Assert.Same(view, presentation.View);
        Assert.Same(view, hostedForm);

        Assert.Equal(
            typeof(Form),
            typeof(WorkspaceDocumentPresentation)
                .GetProperty(nameof(WorkspaceDocumentPresentation.HostedForm))!
                .PropertyType);
    }

    [Fact]
    public void HostedForm_ThrowsWhenDocumentViewDoesNotProvideFormHostingSurface()
    {
        // Arrange
        var view = new Mock<IDocumentView>();

        using WorkspaceDocumentPresentation presentation =
            new(
                Guid.NewGuid(),
                CreatePresenter(view.Object),
                view.Object);

        // Act
        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () => _ = presentation.HostedForm);

        // Assert
        Assert.Contains(
            "WinForms Form hosting surface",
            exception.Message);
    }

    [Fact]
    public async Task Dispose_DisposesPresenterAndDisposableView()
    {
        var view =
            new DisposableDocumentView();

        DocumentWorkspacePresenter presenter =
            CreatePresenter(view);

        WorkspaceDocumentPresentation presentation =
            new(
                Guid.NewGuid(),
                presenter,
                view);

        presentation.Dispose();

        Assert.True(view.IsDisposed);

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => presenter.OpenAsync(
                Guid.NewGuid(),
                TestContext.Current.CancellationToken));

        Assert.True(view.IsPresentationCleared);
        Assert.True(view.IsDisposed);
    }

    [Fact]
    public async Task Dispose_IsIdempotent()
    {
        var view =
            new Mock<IDocumentView>();

        DocumentWorkspacePresenter presenter =
            CreatePresenter(view);

        WorkspaceDocumentPresentation presentation =
            new(
                Guid.NewGuid(),
                presenter,
                view.Object);

        presentation.Dispose();
        presentation.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => presenter.OpenAsync(
                Guid.NewGuid(),
                TestContext.Current.CancellationToken));

        view.Verify(
            x => x.ClearPresentation(),
            Times.Once);
    }

    [Fact]
    public void DisplayName_DelegatesToPresenter()
    {
        DocumentWorkspacePresenter presenter =
            CreatePresenter();

        WorkspaceDocumentPresentation presentation =
            new(
                Guid.NewGuid(),
                presenter,
                new Mock<IDocumentView>().Object);

        Assert.Null(
            presentation.DisplayName);
    }

    [Fact]
    public void FileName_DelegatesToPresenter()
    {
        DocumentWorkspacePresenter presenter =
            CreatePresenter();

        WorkspaceDocumentPresentation presentation =
            new(
                Guid.NewGuid(),
                presenter,
                new Mock<IDocumentView>().Object);

        Assert.Null(
            presentation.FileName);
    }

    private sealed class DisposableDocumentView :
        IDocumentView,
        IDisposable
    {
        public bool IsPresentationCleared { get; private set; }

        public event EventHandler OpenExternallyRequested
        {
            add { }
            remove { }
        }

        public event EventHandler DocumentInformationRequested
        {
            add { }
            remove { }
        }

        public event EventHandler RemoveDocumentRequested
        {
            add { }
            remove { }
        }

        public event EventHandler CloseWorkspaceRequested
        {
            add { }
            remove { }
        }

        public bool IsDisposed { get; private set; }

        public Task ShowDocumentAsync(
            Stream documentStream,
            string fileName,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public void ShowUnsupportedPreview(string message)
        {
        }

        public void ShowDocumentInformation(
            string displayName,
            string fileName,
            string fileType,
            DateTime importedAt,
            string status,
            string sha256Hash,
            long expectedSessionRevision = 0)
        {
        }

        public bool ConfirmRemoval(
            string fileName,
            long expectedSessionRevision = 0)
        {
            return false;
        }

        public void ClearPresentation()
        {
            IsPresentationCleared = true;
        }

        public void ShowError(
            string message,
            string title,
            long expectedSessionRevision = 0)
        {
        }

        public void PostToUi(Action action)
        {
            ArgumentNullException.ThrowIfNull(action);
            action();
        }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    private static DocumentWorkspacePresenter CreatePresenter(
        IDocumentView view)
    {
        var documentRepository = new Mock<IDocumentRepository>();
        var documentReader = new Mock<IDocumentReader>();
        var workspaceRepository = new Mock<IWorkspaceRepository>();
        var storageService = new Mock<IStorageService>();

        var documentViewer = new Mock<IDocumentViewer>();

        var getDocumentHandler = new GetDocumentHandler(
            documentRepository.Object,
            NullLogger<GetDocumentHandler>.Instance);

        var openDocumentHandler = new OpenDocumentHandler(
            documentRepository.Object,
            documentReader.Object,
            new DocumentProcessingOptions(),
            NullLogger<OpenDocumentHandler>.Instance);

        var removeDocumentHandler = new RemoveDocumentHandler(
            documentRepository.Object,
            workspaceRepository.Object,
            storageService.Object,
            NullLogger<RemoveDocumentHandler>.Instance);

        return new DocumentWorkspacePresenter(
            Guid.NewGuid(),
            view,
            documentViewer.Object,
            getDocumentHandler,
            openDocumentHandler,
            removeDocumentHandler,
            NullLogger<DocumentWorkspacePresenter>.Instance,
            CreateAuthenticatedApplicationSecuritySession());
    }

    private static IApplicationSecuritySession CreateAuthenticatedApplicationSecuritySession()
    {
        var applicationSecuritySession =
            new Mock<IApplicationSecuritySession>();

        applicationSecuritySession
            .SetupGet(x => x.CurrentSnapshot)
            .Returns(
                new ApplicationSecuritySessionSnapshot(
                    ApplicationSecuritySessionState.Authenticated,
                    0));

        return applicationSecuritySession.Object;
    }

    private static DocumentWorkspacePresenter CreatePresenter(
    Mock<IDocumentView>? view = null)
    {
        view ??= new Mock<IDocumentView>();

        var documentRepository = new Mock<IDocumentRepository>();
        var documentReader = new Mock<IDocumentReader>();
        var workspaceRepository = new Mock<IWorkspaceRepository>();
        var storageService = new Mock<IStorageService>();

        var documentViewer = new Mock<IDocumentViewer>();

        var getDocumentHandler = new GetDocumentHandler(
            documentRepository.Object,
            NullLogger<GetDocumentHandler>.Instance);

        var openDocumentHandler = new OpenDocumentHandler(
            documentRepository.Object,
            documentReader.Object,
            new DocumentProcessingOptions(),
            NullLogger<OpenDocumentHandler>.Instance);

        var removeDocumentHandler = new RemoveDocumentHandler(
            documentRepository.Object,
            workspaceRepository.Object,
            storageService.Object,
            NullLogger<RemoveDocumentHandler>.Instance);

        return new DocumentWorkspacePresenter(
            Guid.NewGuid(),
            view.Object,
            documentViewer.Object,
            getDocumentHandler,
            openDocumentHandler,
            removeDocumentHandler,
            NullLogger<DocumentWorkspacePresenter>.Instance,
            CreateAuthenticatedApplicationSecuritySession());
    }
}
