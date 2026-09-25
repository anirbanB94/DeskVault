using DeskVault.Application.Documents.Commands.RemoveDocument;
using DeskVault.Application.Documents.Queries.GetDocument;
using DeskVault.Application.Documents.Queries.OpenDocument;
using DeskVault.Application.Interfaces;
using DeskVault.UI.Presenters;
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
            new Mock<IDocumentWorkspaceView>();

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
            new Mock<IDocumentWorkspaceView>();

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
    public void Activate_DelegatesToPresenter()
    {
        var view =
            new Mock<IDocumentWorkspaceView>();

        DocumentWorkspacePresenter presenter =
            CreatePresenter(view);

        WorkspaceDocumentPresentation presentation =
            new(
                Guid.NewGuid(),
                presenter,
                view.Object);

        presentation.Activate();

        view.Verify(
            x => x.ActivateWorkspace(),
            Times.Once);
    }

    [Fact]
    public void Dispose_DisposesPresenterAndDisposableView()
    {
        var view =
            new DisposableDocumentWorkspaceView();

        DocumentWorkspacePresenter presenter =
            CreatePresenter(view);

        WorkspaceDocumentPresentation presentation =
            new(
                Guid.NewGuid(),
                presenter,
                view);

        presentation.Dispose();

        Assert.True(
            view.IsDisposed);

        Assert.Throws<ObjectDisposedException>(
            () => presentation.Activate());
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var view =
            new Mock<IDocumentWorkspaceView>();

        DocumentWorkspacePresenter presenter =
            CreatePresenter(view);

        WorkspaceDocumentPresentation presentation =
            new(
                Guid.NewGuid(),
                presenter,
                view.Object);

        presentation.Dispose();
        presentation.Dispose();

        Assert.Throws<ObjectDisposedException>(
            () => presentation.Activate());
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
                new Mock<IDocumentWorkspaceView>().Object);

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
                new Mock<IDocumentWorkspaceView>().Object);

        Assert.Null(
            presentation.FileName);
    }

    private sealed class DisposableDocumentWorkspaceView :
        IDocumentWorkspaceView,
        IDisposable
    {
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
            string sha256Hash)
        {
        }

        public bool ConfirmRemoval(string fileName)
        {
            return false;
        }

        public void ActivateWorkspace()
        {
        }

        public void CloseWorkspace()
        {
        }

        public void ShowError(
            string message,
            string title)
        {
        }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    private static DocumentWorkspacePresenter CreatePresenter(
        IDocumentWorkspaceView view)
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
            NullLogger<DocumentWorkspacePresenter>.Instance);
    }

    private static DocumentWorkspacePresenter CreatePresenter(
    Mock<IDocumentWorkspaceView>? view = null)
    {
        view ??= new Mock<IDocumentWorkspaceView>();

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
            NullLogger<DocumentWorkspacePresenter>.Instance);
    }
}
