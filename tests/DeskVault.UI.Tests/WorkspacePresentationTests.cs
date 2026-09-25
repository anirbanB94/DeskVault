using DeskVault.Application.Documents.Commands.RemoveDocument;
using DeskVault.Application.Documents.Queries.GetDocument;
using DeskVault.Application.Documents.Queries.ListDocuments;
using DeskVault.Application.Documents.Queries.OpenDocument;
using DeskVault.Application.Interfaces;
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

namespace DeskVault.UI.Tests;

public sealed class WorkspacePresentationTests
{
    [Fact]
    public void PersistentWorkspace_DefaultsToTabs()
    {
        WorkspacePresentation presentation =
            CreatePresentation(
                WorkspaceType.Persistent,
                "Workspace",
                "Description");

        Assert.Equal(
            WorkspacePresentationNavigationMode.Tabs,
            presentation.NavigationMode);
    }

    [Fact]
    public void PersistentWorkspace_CanSwitchBetweenTabsAndSidebar()
    {
        WorkspacePresentationViewHarness view =
            CreateViewHarness();

        WorkspacePresentation presentation =
            CreatePresentation(
                WorkspaceType.Persistent,
                "Workspace",
                null,
                view);

        presentation.SetNavigationMode(
            WorkspacePresentationNavigationMode.Sidebar);

        Assert.Equal(
            WorkspacePresentationNavigationMode.Sidebar,
            presentation.NavigationMode);

        view.View.Verify(
            x => x.SetNavigationMode(
                WorkspacePresentationNavigationMode.Sidebar),
            Times.Once);

        presentation.SetNavigationMode(
            WorkspacePresentationNavigationMode.Tabs);

        Assert.Equal(
            WorkspacePresentationNavigationMode.Tabs,
            presentation.NavigationMode);

        view.View.Verify(
            x => x.SetNavigationMode(
                WorkspacePresentationNavigationMode.Tabs),
            Times.Once);
    }

    [Fact]
    public void TemporaryWorkspace_DoesNotChangeNavigationMode()
    {
        WorkspacePresentationViewHarness view =
            CreateViewHarness();

        WorkspacePresentation presentation =
            CreatePresentation(
                WorkspaceType.Temporary,
                null,
                null,
                view);

        presentation.SetNavigationMode(
            WorkspacePresentationNavigationMode.Sidebar);

        Assert.Equal(
            WorkspacePresentationNavigationMode.Tabs,
            presentation.NavigationMode);

        view.View.Verify(
            x => x.SetNavigationMode(
                It.IsAny<WorkspacePresentationNavigationMode>()),
            Times.Never);
    }

    [Fact]
    public void OpenDocument_CreatesAndActivatesDocumentPresentation()
    {
        Guid workspaceId = Guid.NewGuid();
        Guid documentId = Guid.NewGuid();

        WorkspacePresentationViewHarness view =
            CreateViewHarness();

        var documentPresentationFactory =
            new Mock<IWorkspaceDocumentPresentationFactory>();

        WorkspaceDocumentPresentation documentPresentation =
            CreateDocumentPresentation(
                workspaceId,
                documentId);

        documentPresentationFactory
            .Setup(x => x.Create(
                workspaceId,
                documentId))
            .Returns(documentPresentation);

        WorkspacePresentation presentation =
            CreatePresentation(
                WorkspaceType.Persistent,
                "Workspace",
                null,
                view,
                documentPresentationFactory:
                    documentPresentationFactory,
                workspaceId: workspaceId);

        WorkspaceDocumentPresentation result =
            presentation.OpenDocument(documentId);

        Assert.Same(
            documentPresentation,
            result);

        Assert.Contains(
            documentId,
            presentation.PresentedDocumentIds);

        Assert.Equal(
            documentId,
            presentation.ActiveDocumentId);

        view.View.Verify(
            x => x.AddDocumentPresentation(
                documentPresentation),
            Times.Once);

        view.View.Verify(
            x => x.ActivateDocumentPresentation(
                documentId),
            Times.Once);

        documentPresentationFactory.Verify(
            x => x.Create(
                workspaceId,
                documentId),
            Times.Once);
    }

    [Fact]
    public void OpenDocument_WhenDocumentAlreadyPresented_ReusesExistingPresentation()
    {
        Guid workspaceId = Guid.NewGuid();
        Guid documentId = Guid.NewGuid();

        WorkspacePresentationViewHarness view =
            CreateViewHarness();

        var documentPresentationFactory =
            new Mock<IWorkspaceDocumentPresentationFactory>();

        WorkspaceDocumentPresentation documentPresentation =
            CreateDocumentPresentation(
                workspaceId,
                documentId);

        documentPresentationFactory
            .Setup(x => x.Create(
                workspaceId,
                documentId))
            .Returns(documentPresentation);

        WorkspacePresentation presentation =
            CreatePresentation(
                WorkspaceType.Persistent,
                "Workspace",
                null,
                view,
                documentPresentationFactory:
                    documentPresentationFactory,
                workspaceId: workspaceId);

        WorkspaceDocumentPresentation first =
            presentation.OpenDocument(documentId);

        WorkspaceDocumentPresentation second =
            presentation.OpenDocument(documentId);

        Assert.Same(
            first,
            second);

        Assert.Single(
            presentation.PresentedDocumentIds);

        Assert.Equal(
            documentId,
            presentation.ActiveDocumentId);

        documentPresentationFactory.Verify(
            x => x.Create(
                workspaceId,
                documentId),
            Times.Once);

        view.View.Verify(
            x => x.AddDocumentPresentation(
                documentPresentation),
            Times.Exactly(2));

        view.View.Verify(
            x => x.ActivateDocumentPresentation(
                documentId),
            Times.Exactly(2));
    }

    [Fact]
    public void OpenMultipleDocuments_CanSwitchActiveDocument()
    {
        Guid workspaceId = Guid.NewGuid();
        Guid firstDocumentId = Guid.NewGuid();
        Guid secondDocumentId = Guid.NewGuid();

        WorkspacePresentationViewHarness view =
            CreateViewHarness();

        var documentPresentationFactory =
            new Mock<IWorkspaceDocumentPresentationFactory>();

        WorkspaceDocumentPresentation firstPresentation =
            CreateDocumentPresentation(
                workspaceId,
                firstDocumentId);

        WorkspaceDocumentPresentation secondPresentation =
            CreateDocumentPresentation(
                workspaceId,
                secondDocumentId);

        documentPresentationFactory
            .Setup(x => x.Create(
                workspaceId,
                firstDocumentId))
            .Returns(firstPresentation);

        documentPresentationFactory
            .Setup(x => x.Create(
                workspaceId,
                secondDocumentId))
            .Returns(secondPresentation);

        WorkspacePresentation presentation =
            CreatePresentation(
                WorkspaceType.Persistent,
                "Workspace",
                null,
                view,
                documentPresentationFactory:
                    documentPresentationFactory,
                workspaceId: workspaceId);

        presentation.OpenDocument(firstDocumentId);
        presentation.OpenDocument(secondDocumentId);

        Assert.Equal(
            secondDocumentId,
            presentation.ActiveDocumentId);

        Assert.Contains(
            firstDocumentId,
            presentation.PresentedDocumentIds);

        Assert.Contains(
            secondDocumentId,
            presentation.PresentedDocumentIds);

        Assert.Equal(
            2,
            presentation.PresentedDocumentIds.Count);

        Assert.True(
            presentation.ActivateDocument(
                firstDocumentId));

        Assert.Equal(
            firstDocumentId,
            presentation.ActiveDocumentId);

        view.View.Verify(
            x => x.ActivateDocumentPresentation(
                firstDocumentId),
            Times.Exactly(2));

        view.View.Verify(
            x => x.ActivateDocumentPresentation(
                secondDocumentId),
            Times.Once);
    }

    [Fact]
    public async Task CloseDocumentPresentation_RemovesPresentationButKeepsWorkspaceMembership()
    {
        Guid workspaceId = Guid.NewGuid();
        Guid documentId = Guid.NewGuid();

        WorkspacePresentationViewHarness view =
            CreateViewHarness();

        var documentPresentationFactory =
            new Mock<IWorkspaceDocumentPresentationFactory>();

        WorkspaceDocumentPresentation documentPresentation =
            CreateDocumentPresentation(
                workspaceId,
                documentId);

        documentPresentationFactory
            .Setup(x => x.Create(
                workspaceId,
                documentId))
            .Returns(documentPresentation);

        WorkspacePresentation presentation =
            CreatePresentation(
                WorkspaceType.Persistent,
                "Workspace",
                null,
                view,
                documentPresentationFactory:
                    documentPresentationFactory,
                workspaceId: workspaceId);

        await presentation.SetWorkspaceDocumentsAsync(
            [documentId]);

        presentation.OpenDocument(documentId);

        Assert.Contains(
            documentId,
            presentation.WorkspaceDocumentIds);

        Assert.Contains(
            documentId,
            presentation.PresentedDocumentIds);

        Assert.True(
            presentation.CloseDocumentPresentation(
                documentId));

        Assert.Contains(
            documentId,
            presentation.WorkspaceDocumentIds);

        Assert.DoesNotContain(
            documentId,
            presentation.PresentedDocumentIds);

        Assert.False(
            presentation.ContainsDocument(
                documentId));

        Assert.Null(
            presentation.ActiveDocumentId);

        view.View.Verify(
            x => x.RemoveDocumentPresentation(
                documentId),
            Times.Once);
    }

    [Fact]
    public async Task ClosedWorkspaceDocument_CanBePresentedAgain()
    {
        Guid workspaceId = Guid.NewGuid();
        Guid documentId = Guid.NewGuid();

        WorkspacePresentationViewHarness view =
            CreateViewHarness();

        var documentPresentationFactory =
            new Mock<IWorkspaceDocumentPresentationFactory>();

        WorkspaceDocumentPresentation firstPresentation =
            CreateDocumentPresentation(
                workspaceId,
                documentId);

        WorkspaceDocumentPresentation secondPresentation =
            CreateDocumentPresentation(
                workspaceId,
                documentId);

        documentPresentationFactory
            .SetupSequence(x => x.Create(
                workspaceId,
                documentId))
            .Returns(firstPresentation)
            .Returns(secondPresentation);

        WorkspacePresentation presentation =
            CreatePresentation(
                WorkspaceType.Persistent,
                "Workspace",
                null,
                view,
                documentPresentationFactory:
                    documentPresentationFactory,
                workspaceId: workspaceId);

        await presentation.SetWorkspaceDocumentsAsync(
            [documentId]);

        presentation.OpenDocument(documentId);

        Assert.Same(
            firstPresentation,
            presentation.GetDocumentPresentation(
                documentId));

        Assert.True(
            presentation.CloseDocumentPresentation(
                documentId));

        Assert.Contains(
            documentId,
            presentation.WorkspaceDocumentIds);

        Assert.DoesNotContain(
            documentId,
            presentation.PresentedDocumentIds);

        WorkspaceDocumentPresentation reopenedPresentation =
            presentation.OpenDocument(documentId);

        Assert.Same(
            secondPresentation,
            reopenedPresentation);

        Assert.Contains(
            documentId,
            presentation.WorkspaceDocumentIds);

        Assert.Contains(
            documentId,
            presentation.PresentedDocumentIds);

        Assert.Equal(
            documentId,
            presentation.ActiveDocumentId);

        documentPresentationFactory.Verify(
            x => x.Create(
                workspaceId,
                documentId),
            Times.Exactly(2));

        view.View.Verify(
            x => x.AddDocumentPresentation(
                firstPresentation),
            Times.Once);

        view.View.Verify(
            x => x.AddDocumentPresentation(
                secondPresentation),
            Times.Once);

        view.View.Verify(
            x => x.ActivateDocumentPresentation(
                documentId),
            Times.Exactly(2));
    }

    [Fact]
    public void CloseDocumentPresentation_WhenMultipleDocumentsArePresented_ActivatesAnotherDocument()
    {
        Guid workspaceId = Guid.NewGuid();
        Guid firstDocumentId = Guid.NewGuid();
        Guid secondDocumentId = Guid.NewGuid();

        WorkspacePresentationViewHarness view =
            CreateViewHarness();

        var documentPresentationFactory =
            new Mock<IWorkspaceDocumentPresentationFactory>();

        WorkspaceDocumentPresentation firstPresentation =
            CreateDocumentPresentation(
                workspaceId,
                firstDocumentId);

        WorkspaceDocumentPresentation secondPresentation =
            CreateDocumentPresentation(
                workspaceId,
                secondDocumentId);

        documentPresentationFactory
            .Setup(x => x.Create(
                workspaceId,
                firstDocumentId))
            .Returns(firstPresentation);

        documentPresentationFactory
            .Setup(x => x.Create(
                workspaceId,
                secondDocumentId))
            .Returns(secondPresentation);

        WorkspacePresentation presentation =
            CreatePresentation(
                WorkspaceType.Persistent,
                "Workspace",
                null,
                view,
                documentPresentationFactory:
                    documentPresentationFactory,
                workspaceId: workspaceId);

        presentation.OpenDocument(firstDocumentId);
        presentation.OpenDocument(secondDocumentId);

        Assert.True(
            presentation.CloseDocumentPresentation(
                secondDocumentId));

        Assert.DoesNotContain(
            secondDocumentId,
            presentation.PresentedDocumentIds);

        Assert.Contains(
            firstDocumentId,
            presentation.PresentedDocumentIds);

        Assert.Equal(
            firstDocumentId,
            presentation.ActiveDocumentId);

        view.View.Verify(
            x => x.RemoveDocumentPresentation(
                secondDocumentId),
            Times.Once);

        view.View.Verify(
            x => x.ActivateDocumentPresentation(
                firstDocumentId),
            Times.Exactly(2));
    }

    [Fact]
    public void DocumentActivated_ForNonMember_DoesNothing()
    {
        Guid documentId = Guid.NewGuid();

        WorkspacePresentationViewHarness view =
            CreateViewHarness();

        var documentPresentationFactory =
            new Mock<IWorkspaceDocumentPresentationFactory>();

        WorkspacePresentation presentation =
            CreatePresentation(
                WorkspaceType.Persistent,
                "Workspace",
                null,
                view,
                documentPresentationFactory:
                    documentPresentationFactory);

        view.View.Raise(
            x => x.DocumentActivated += null,
            new DocumentActivatedEventArgs(
                documentId));

        Assert.Empty(
            presentation.PresentedDocumentIds);

        Assert.Null(
            presentation.ActiveDocumentId);

        documentPresentationFactory.Verify(
            x => x.Create(
                It.IsAny<Guid>(),
                documentId),
            Times.Never);

        view.View.Verify(
            x => x.AddDocumentPresentation(
                It.IsAny<WorkspaceDocumentPresentation>()),
            Times.Never);
    }

    [Fact]
    public async Task RemoveDocuments_Successfully_RemovesMembershipAndPresentation()
    {
        Guid workspaceId = Guid.NewGuid();
        Guid documentId = Guid.NewGuid();

        Workspace workspace =
            Workspace.CreatePersistent(
                workspaceId,
                "Workspace");

        workspace.AddDocument(
            documentId);

        WorkspacePresentationViewHarness view =
            CreateViewHarness();

        var dialogService =
            new Mock<IWorkspaceDialogService>();

        dialogService
            .Setup(x => x.ShowDocumentRemoval(
                It.IsAny<IReadOnlyList<Document>>()))
            .Returns(
                new WorkspaceDocumentSelectionResult(
                    [documentId]));

        var workspaceRepository =
            new Mock<IWorkspaceRepository>();

        var activeWorkspaceRegistry =
            new Mock<IActiveWorkspaceRegistry>();

        activeWorkspaceRegistry
            .Setup(x => x.Get(workspaceId))
            .Returns(workspace);

        var documentPresentationFactory =
            new Mock<IWorkspaceDocumentPresentationFactory>();

        WorkspaceDocumentPresentation documentPresentation =
            CreateDocumentPresentation(
                workspaceId,
                documentId);

        documentPresentationFactory
            .Setup(x => x.Create(
                workspaceId,
                documentId))
            .Returns(documentPresentation);

        WorkspacePresentation presentation =
            CreatePresentation(
                WorkspaceType.Persistent,
                "Workspace",
                null,
                view,
                dialogService,
                workspaceRepository,
                activeWorkspaceRegistry,
                workspaceId,
                documentPresentationFactory);

        await presentation.SetWorkspaceDocumentsAsync(
            [documentId]);

        presentation.OpenDocument(
            documentId);

        TaskCompletionSource<bool> updateCompleted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        presentation.WorkspaceDocumentsUpdated +=
            (_, _) => updateCompleted.TrySetResult(true);

        view.View.Raise(
            x => x.RemoveDocumentsRequested += null,
            EventArgs.Empty);

        await updateCompleted.Task.WaitAsync(
            TimeSpan.FromSeconds(5));

        Assert.DoesNotContain(
            documentId,
            presentation.WorkspaceDocumentIds);

        Assert.DoesNotContain(
            documentId,
            presentation.PresentedDocumentIds);

        Assert.False(
            presentation.ContainsDocument(
                documentId));

        workspaceRepository.Verify(
            x => x.UpdateAsync(
                It.Is<Workspace>(
                    updatedWorkspace =>
                        !updatedWorkspace.Memberships.Any(
                            membership =>
                                membership.DocumentId == documentId)),
                It.IsAny<CancellationToken>()),
            Times.Once);

        activeWorkspaceRegistry.Verify(
            x => x.Replace(
                It.Is<Workspace>(
                    updatedWorkspace =>
                        !updatedWorkspace.Memberships.Any(
                            membership =>
                                membership.DocumentId == documentId))),
            Times.Once);

        view.View.Verify(
            x => x.RemoveDocumentPresentation(
                documentId),
            Times.Once);
    }

    [Fact]
    public void TemporaryWorkspace_CloseDocumentRequested_RaisesWorkspaceCloseRequested()
    {
        WorkspacePresentationViewHarness view =
            CreateViewHarness();

        WorkspacePresentation presentation =
            CreatePresentation(
                WorkspaceType.Temporary,
                "Document.txt",
                null,
                view);

        int closeRequestedCount = 0;

        presentation.WorkspaceCloseRequested +=
            (_, _) => closeRequestedCount++;

        view.View.Raise(
            x => x.CloseDocumentRequested += null,
            EventArgs.Empty);

        Assert.Equal(
            1,
            closeRequestedCount);
    }

    [Fact]
    public void TemporaryWorkspace_UsesTemporaryPresentation()
    {
        WorkspacePresentationViewHarness view =
            CreateViewHarness();

        WorkspacePresentation presentation =
            CreatePresentation(
                WorkspaceType.Temporary,
                null,
                null,
                view);

        Assert.True(
            presentation.IsTemporary);

        view.View.Verify(
            x => x.SetTemporaryDocumentPresentation(
                It.IsAny<string>()),
            Times.Once);

        view.View.Verify(
            x => x.SetWorkspaceIdentity(
                It.IsAny<string>(),
                It.IsAny<string?>()),
            Times.Never);
    }

    [Fact]
    public void SaveAsWorkspace_Cancelled_DoesNotChangeTemporaryWorkspace()
    {
        Guid workspaceId = Guid.NewGuid();

        WorkspacePresentationViewHarness view =
            CreateViewHarness();

        var dialogService =
            new Mock<IWorkspaceDialogService>();

        dialogService
            .Setup(x => x.ShowWorkspaceDetails(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                "Save as Workspace"))
            .Returns((WorkspaceDetailsDialogResult?)null);

        var workspaceRepository =
            new Mock<IWorkspaceRepository>();

        var activeWorkspaceRegistry =
            new Mock<IActiveWorkspaceRegistry>();

        WorkspacePresentation presentation =
            CreatePresentation(
                WorkspaceType.Temporary,
                null,
                null,
                view,
                dialogService,
                workspaceRepository,
                activeWorkspaceRegistry,
                workspaceId);

        view.View.Raise(
            x => x.SaveAsWorkspaceRequested += null,
            EventArgs.Empty);

        Assert.True(
            presentation.IsTemporary);

        Assert.Null(
            presentation.WorkspaceName);

        Assert.Null(
            presentation.WorkspaceDescription);

        dialogService.Verify(
            x => x.ShowWorkspaceDetails(
                string.Empty,
                null,
                "Save as Workspace"),
            Times.Once);
    }

    [Fact]
    public void UpdateWorkspaceDetails_Cancelled_DoesNotChangeIdentity()
    {
        WorkspacePresentationViewHarness view =
            CreateViewHarness();

        var dialogService =
            new Mock<IWorkspaceDialogService>();

        dialogService
            .Setup(x => x.ShowWorkspaceDetails(
                "Workspace",
                "Description",
                "Update Workspace Details"))
            .Returns((WorkspaceDetailsDialogResult?)null);

        WorkspacePresentation presentation =
            CreatePresentation(
                WorkspaceType.Persistent,
                "Workspace",
                "Description",
                view,
                dialogService);

        view.View.Raise(
            x => x.UpdateWorkspaceDetailsRequested += null,
            EventArgs.Empty);

        Assert.Equal(
            "Workspace",
            presentation.WorkspaceName);

        Assert.Equal(
            "Description",
            presentation.WorkspaceDescription);
    }

    [Fact]
    public void AddDocuments_Cancelled_DoesNotChangeMembership()
    {
        WorkspacePresentationViewHarness view =
            CreateViewHarness();

        var dialogService =
            new Mock<IWorkspaceDialogService>();

        dialogService
            .Setup(x => x.ShowDocumentPicker(
                It.IsAny<IReadOnlyList<Document>>(),
                It.IsAny<IEnumerable<Guid>>()))
            .Returns((WorkspaceDocumentSelectionResult?)null);

        WorkspacePresentation presentation =
            CreatePresentation(
                WorkspaceType.Persistent,
                "Workspace",
                null,
                view,
                dialogService);

        view.View.Raise(
            x => x.AddDocumentsRequested += null,
            EventArgs.Empty);

        Assert.Empty(
            presentation.WorkspaceDocumentIds);
    }

    [Fact]
    public async Task RemoveDocuments_Cancelled_DoesNotChangeMembership()
    {
        Guid documentId = Guid.NewGuid();

        WorkspacePresentationViewHarness view =
            CreateViewHarness();

        var dialogService =
            new Mock<IWorkspaceDialogService>();

        dialogService
            .Setup(x => x.ShowDocumentRemoval(
                It.IsAny<IReadOnlyList<Document>>()))
            .Returns((WorkspaceDocumentSelectionResult?)null);

        WorkspacePresentation presentation =
            CreatePresentation(
                WorkspaceType.Persistent,
                "Workspace",
                null,
                view,
                dialogService);

        await presentation.SetWorkspaceDocumentsAsync(
            [documentId]);

        view.View.Raise(
            x => x.RemoveDocumentsRequested += null,
            EventArgs.Empty);

        Assert.Contains(
            documentId,
            presentation.WorkspaceDocumentIds);
    }

    private static WorkspacePresentation CreatePresentation(
        WorkspaceType workspaceType,
        string? workspaceName,
        string? description,
        WorkspacePresentationViewHarness? view = null,
        Mock<IWorkspaceDialogService>? dialogService = null,
        Mock<IWorkspaceRepository>? workspaceRepository = null,
        Mock<IActiveWorkspaceRegistry>? activeWorkspaceRegistry = null,
        Guid? workspaceId = null,
        Mock<IWorkspaceDocumentPresentationFactory>? documentPresentationFactory = null)
    {
        Guid id =
            workspaceId ?? Guid.NewGuid();

        view ??=
            CreateViewHarness();

        dialogService ??=
            new Mock<IWorkspaceDialogService>();

        workspaceRepository ??=
            new Mock<IWorkspaceRepository>();

        activeWorkspaceRegistry ??=
            new Mock<IActiveWorkspaceRegistry>();

        documentPresentationFactory ??=
            new Mock<IWorkspaceDocumentPresentationFactory>();

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetAllAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                []);

        var listDocumentsHandler =
            new ListDocumentsHandler(
                repository.Object,
                NullLogger<ListDocumentsHandler>.Instance);

        var addDocumentToWorkspaceHandler =
            new AddDocumentToWorkspaceHandler(
                repository.Object,
                workspaceRepository.Object,
                activeWorkspaceRegistry.Object,
                NullLogger<AddDocumentToWorkspaceHandler>.Instance);

        var removeDocumentFromWorkspaceHandler =
            new RemoveDocumentFromWorkspaceHandler(
                workspaceRepository.Object,
                activeWorkspaceRegistry.Object,
                NullLogger<RemoveDocumentFromWorkspaceHandler>.Instance);

        var renameWorkspaceHandler =
            new RenameWorkspaceHandler(
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

        return new WorkspacePresentation(
            id,
            workspaceType,
            workspaceName,
            description,
            documentPresentationFactory.Object,
            listDocumentsHandler,
            addDocumentToWorkspaceHandler,
            removeDocumentFromWorkspaceHandler,
            renameWorkspaceHandler,
            saveTemporaryWorkspaceHandler,
            updateWorkspaceDescriptionHandler,
            dialogService.Object,
            view.View.Object,
            NullLogger<WorkspacePresentation>.Instance);
    }

    private static WorkspaceDocumentPresentation CreateDocumentPresentation(
        Guid workspaceId,
        Guid documentId)
    {
        var view =
            new Mock<IDocumentWorkspaceView>();

        var documentRepository =
            new Mock<IDocumentRepository>();

        var documentReader =
            new Mock<IDocumentReader>();

        var workspaceRepository =
            new Mock<IWorkspaceRepository>();

        var storageService =
            new Mock<IStorageService>();

        var documentViewer =
            new Mock<IDocumentViewer>();

        var getDocumentHandler =
            new GetDocumentHandler(
                documentRepository.Object,
                NullLogger<GetDocumentHandler>.Instance);

        var openDocumentHandler =
            new OpenDocumentHandler(
                documentRepository.Object,
                documentReader.Object,
                NullLogger<OpenDocumentHandler>.Instance);

        var removeDocumentHandler =
            new RemoveDocumentHandler(
                documentRepository.Object,
                workspaceRepository.Object,
                storageService.Object,
                NullLogger<RemoveDocumentHandler>.Instance);

        var presenter =
            new DocumentWorkspacePresenter(
                workspaceId,
                view.Object,
                documentViewer.Object,
                getDocumentHandler,
                openDocumentHandler,
                removeDocumentHandler,
                NullLogger<DocumentWorkspacePresenter>.Instance);

        return new WorkspaceDocumentPresentation(
            documentId,
            presenter,
            view.Object);
    }

    private static WorkspacePresentationViewHarness CreateViewHarness()
    {
        return new WorkspacePresentationViewHarness();
    }

    private sealed class WorkspacePresentationViewHarness
    {
        public WorkspacePresentationViewHarness()
        {
            View =
                new Mock<IWorkspacePresentationView>();
        }

        public Mock<IWorkspacePresentationView> View { get; }
    }
}
