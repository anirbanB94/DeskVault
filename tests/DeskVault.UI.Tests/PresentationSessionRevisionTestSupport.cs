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
using DeskVault.Application.Workspaces.Commands.UpdateWorkspaceLastActiveDocument;
using DeskVault.Domain.Workspaces;
using DeskVault.UI.Presenters;
using DeskVault.UI.Services.Interfaces;
using DeskVault.UI.Services.Workspace;
using DeskVault.UI.Tests.TestDoubles;
using DeskVault.UI.Views;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DeskVault.UI.Tests;

internal static class PresentationSessionRevisionTestSupport
{
    internal static IApplicationSecuritySession CreateAuthenticatedSession(
        long revision = 0)
    {
        var session = new Mock<IApplicationSecuritySession>();
        session
            .SetupGet(x => x.CurrentSnapshot)
            .Returns(new ApplicationSecuritySessionSnapshot(
                ApplicationSecuritySessionState.Authenticated,
                revision));

        return session.Object;
    }


    internal static IDocumentView CreateHostedDocumentView(
        IDocumentView innerView)
    {
        ArgumentNullException.ThrowIfNull(innerView);

        // WinForms Form construction can install a WindowsFormsSynchronizationContext
        // on the current thread. These tests do not run a WinForms message loop, so
        // prevent that context from leaking into asynchronous test continuations.
        SynchronizationContext? originalSynchronizationContext =
            SynchronizationContext.Current;

        try
        {
            return new HostedDocumentView(innerView);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(
                originalSynchronizationContext);
        }
    }


    internal sealed class SecuritySessionHarness
    {
        public SecuritySessionHarness(
            ApplicationSecuritySessionState state,
            long revision)
        {
            CurrentSnapshot = new ApplicationSecuritySessionSnapshot(
                state,
                revision);

            Session = new Mock<IApplicationSecuritySession>();
            Session
                .SetupGet(x => x.CurrentSnapshot)
                .Returns(() => CurrentSnapshot);
        }

        public Mock<IApplicationSecuritySession> Session { get; }

        public ApplicationSecuritySessionSnapshot CurrentSnapshot { get; private set; }

        public void Set(
            ApplicationSecuritySessionState state,
            long revision)
        {
            CurrentSnapshot = new ApplicationSecuritySessionSnapshot(
                state,
                revision);
        }
    }

    internal static WorkspacePresentation CreateWorkspacePresentation(
        Guid workspaceId,
        SecuritySessionHarness session,
        long revision,
        Mock<IWorkspacePresentationView>? view = null,
        Mock<IWorkspaceDocumentPresentationFactory>? documentPresentationFactory = null,
        WorkspaceType workspaceType = WorkspaceType.Persistent,
        Mock<IWorkspaceDialogService>? dialogService = null)
    {
        view ??= new Mock<IWorkspacePresentationView>();
        documentPresentationFactory ??=
            new Mock<IWorkspaceDocumentPresentationFactory>();
        dialogService ??=
            new Mock<IWorkspaceDialogService>();

        var documentRepository = new Mock<IDocumentRepository>();
        documentRepository
            .Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                []);

        var workspaceRepository = new Mock<IWorkspaceRepository>();
        var activeWorkspaceRegistry = new Mock<IActiveWorkspaceRegistry>();

        var listDocumentsHandler = new ListDocumentsHandler(
            documentRepository.Object,
            NullLogger<ListDocumentsHandler>.Instance);

        var addDocumentToWorkspaceHandler =
            new AddDocumentToWorkspaceHandler(
                documentRepository.Object,
                workspaceRepository.Object,
                activeWorkspaceRegistry.Object,
                NullLogger<AddDocumentToWorkspaceHandler>.Instance);

        var removeDocumentFromWorkspaceHandler =
            new RemoveDocumentFromWorkspaceHandler(
                workspaceRepository.Object,
                activeWorkspaceRegistry.Object,
                NullLogger<RemoveDocumentFromWorkspaceHandler>.Instance);

        var renameWorkspaceHandler = new RenameWorkspaceHandler(
            workspaceRepository.Object,
            activeWorkspaceRegistry.Object,
            NullLogger<RenameWorkspaceHandler>.Instance);

        var saveTemporaryWorkspaceHandler =
            new SaveTemporaryWorkspaceHandler(
                workspaceRepository.Object,
                activeWorkspaceRegistry.Object,
                NullLogger<SaveTemporaryWorkspaceHandler>.Instance);

        var updateWorkspaceDescriptionHandler =
            new UpdateWorkspaceDescriptionHandler(
                workspaceRepository.Object,
                activeWorkspaceRegistry.Object,
                NullLogger<UpdateWorkspaceDescriptionHandler>.Instance);

        var updateWorkspaceLastActiveDocumentHandler =
            new UpdateWorkspaceLastActiveDocumentHandler(
                workspaceRepository.Object,
                activeWorkspaceRegistry.Object,
                NullLogger<UpdateWorkspaceLastActiveDocumentHandler>.Instance);

        return new WorkspacePresentation(
            workspaceId,
            workspaceType,
            "Workspace",
            null,
            documentPresentationFactory.Object,
            listDocumentsHandler,
            addDocumentToWorkspaceHandler,
            removeDocumentFromWorkspaceHandler,
            renameWorkspaceHandler,
            saveTemporaryWorkspaceHandler,
            updateWorkspaceDescriptionHandler,
            updateWorkspaceLastActiveDocumentHandler,
            dialogService.Object,
            view.Object,
            NullLogger<WorkspacePresentation>.Instance,
            session.Session.Object,
            revision);
    }

    internal static DocumentWorkspacePresenter CreateDocumentWorkspacePresenter(
        Mock<IDocumentView> view,
        SecuritySessionHarness session,
        Mock<IDocumentRepository>? repository = null,
        long securitySessionRevision = 0)
    {
        repository ??= new Mock<IDocumentRepository>();
        var documentReader = new Mock<IDocumentReader>();
        var workspaceRepository = new Mock<IWorkspaceRepository>();
        var storageService = new Mock<IStorageService>();

        var getDocumentHandler = new GetDocumentHandler(
            repository.Object,
            NullLogger<GetDocumentHandler>.Instance);
        var openDocumentHandler = new OpenDocumentHandler(
            repository.Object,
            documentReader.Object,
            new DocumentProcessingOptions(),
            NullLogger<OpenDocumentHandler>.Instance);
        var removeDocumentHandler = new RemoveDocumentHandler(
            repository.Object,
            workspaceRepository.Object,
            storageService.Object,
            NoOpDocumentArtifactCoordinator.Instance,
            NullLogger<RemoveDocumentHandler>.Instance);

        return new DocumentWorkspacePresenter(
            view.Object,
            new Mock<IDocumentViewer>().Object,
            getDocumentHandler,
            openDocumentHandler,
            removeDocumentHandler,
            NullLogger<DocumentWorkspacePresenter>.Instance,
            session.Session.Object,
            securitySessionRevision);
    }

    internal static WorkspacePresentationFactory CreateWorkspacePresentationFactory(
        IApplicationSecuritySession securitySession,
        Mock<IWorkspaceDocumentPresentationFactory>? documentFactory = null)
    {
        var documentRepository = new Mock<IDocumentRepository>();
        documentRepository
            .Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var workspaceRepository = new Mock<IWorkspaceRepository>();
        var activeWorkspaceRegistry = new Mock<IActiveWorkspaceRegistry>();
        documentFactory ??= new Mock<IWorkspaceDocumentPresentationFactory>();

        return new WorkspacePresentationFactory(
            documentFactory.Object,
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
            new UpdateWorkspaceLastActiveDocumentHandler(
                workspaceRepository.Object,
                activeWorkspaceRegistry.Object,
                NullLogger<UpdateWorkspaceLastActiveDocumentHandler>.Instance),
            new Mock<IWorkspaceDialogService>().Object,
            securitySession);
    }

    internal static WorkspaceDocumentPresentationFactory CreateWorkspaceDocumentPresentationFactory(
        IApplicationSecuritySession securitySession,
        Mock<IDocumentRepository>? repository = null)
    {
        repository ??= new Mock<IDocumentRepository>(MockBehavior.Strict);
        var documentReader = new Mock<IDocumentReader>();
        var workspaceRepository = new Mock<IWorkspaceRepository>();
        var storageService = new Mock<IStorageService>();

        var getDocumentHandler = new GetDocumentHandler(
            repository.Object,
            NullLogger<GetDocumentHandler>.Instance);
        var openDocumentHandler = new OpenDocumentHandler(
            repository.Object,
            documentReader.Object,
            new DocumentProcessingOptions(),
            NullLogger<OpenDocumentHandler>.Instance);
        var removeDocumentHandler = new RemoveDocumentHandler(
            repository.Object,
            workspaceRepository.Object,
            storageService.Object,
            NoOpDocumentArtifactCoordinator.Instance,
            NullLogger<RemoveDocumentHandler>.Instance);

        return new WorkspaceDocumentPresentationFactory(
            new Mock<DeskVault.UI.Rendering.IDocumentContentRendererResolver>().Object,
            new Mock<IDocumentViewer>().Object,
            getDocumentHandler,
            openDocumentHandler,
            removeDocumentHandler,
            NullLogger<DocumentWorkspacePresenter>.Instance,
            securitySession,
            new Mock<ISessionBoundDialogCoordinator>().Object);
    }

    private sealed class HostedDocumentView :
        Form,
        IDocumentView
    {
        private readonly IDocumentView _innerView;
        private bool _innerViewDisposed;

        public HostedDocumentView(IDocumentView innerView)
        {
            ArgumentNullException.ThrowIfNull(innerView);

            _innerView = innerView;
        }

        public event EventHandler OpenExternallyRequested
        {
            add => _innerView.OpenExternallyRequested += value;
            remove => _innerView.OpenExternallyRequested -= value;
        }

        public event EventHandler DocumentInformationRequested
        {
            add => _innerView.DocumentInformationRequested += value;
            remove => _innerView.DocumentInformationRequested -= value;
        }

        public event EventHandler RemoveDocumentRequested
        {
            add => _innerView.RemoveDocumentRequested += value;
            remove => _innerView.RemoveDocumentRequested -= value;
        }

        public Task ShowDocumentAsync(
            Stream documentStream,
            string fileName,
            CancellationToken cancellationToken = default)
        {
            return _innerView.ShowDocumentAsync(
                documentStream,
                fileName,
                cancellationToken);
        }

        public void ShowUnsupportedPreview(string message)
        {
            _innerView.ShowUnsupportedPreview(message);
        }

        public void SetDocumentChromeVisible(bool visible)
        {
            _innerView.SetDocumentChromeVisible(visible);
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
            _innerView.ShowDocumentInformation(
                displayName,
                fileName,
                fileType,
                importedAt,
                status,
                sha256Hash,
                expectedSessionRevision);
        }

        public bool ConfirmRemoval(
            string fileName,
            long expectedSessionRevision = 0)
        {
            return _innerView.ConfirmRemoval(
                fileName,
                expectedSessionRevision);
        }

        public void ClearPresentation()
        {
            _innerView.ClearPresentation();
        }

        public void PostToUi(Action action)
        {
            _innerView.PostToUi(action);
        }

        public void ShowError(
            string message,
            string title,
            long expectedSessionRevision = 0)
        {
            _innerView.ShowError(
                message,
                title,
                expectedSessionRevision);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_innerViewDisposed)
            {
                _innerViewDisposed = true;

                try
                {
                    if (_innerView is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
                finally
                {
                    base.Dispose(disposing);
                }

                return;
            }

            base.Dispose(disposing);
        }
    }
}
