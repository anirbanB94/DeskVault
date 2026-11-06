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
using DeskVault.UI.Resources;
using DeskVault.UI.Services.Interfaces;
using DeskVault.UI.Services.Workspace;
using DeskVault.UI.Views;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using DiagnosticOperationCodes = DeskVault.Shared.Resources.DiagnosticOperationCodes;

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
    public async Task OpenDocumentAsync_WhenPresenterFails_LogsSafeDiagnosticAndRethrows()
    {
        Guid workspaceId = Guid.NewGuid();
        Guid documentId = Guid.NewGuid();

        string[] sensitiveMarkers =
        [
            "DOCUMENT-CONTENT-SENTINEL-232",
            "ENCRYPTION-KEY-SENTINEL-232",
            @"C:\Sensitive\Vault\CustomerReport-232.pdf",
            "SEARCH-QUERY-SENTINEL-232",
            "EXCEPTION-DETAIL-SENTINEL-232"
        ];

        var originalException =
            new InvalidOperationException(
                string.Join("|", sensitiveMarkers));

        var logger =
            new RecordingLogger<WorkspacePresentation>();

        var documentPresentationFactory =
            new Mock<IWorkspaceDocumentPresentationFactory>();

        WorkspaceDocumentPresentation documentPresentation =
            CreateDocumentPresentation(
                workspaceId,
                documentId,
                originalException);

        documentPresentationFactory
            .Setup(x => x.Create(
                workspaceId,
                documentId, 0))
            .Returns(documentPresentation);

        WorkspacePresentation presentation =
            CreatePresentation(
                WorkspaceType.Persistent,
                "Workspace",
                null,
                workspaceId: workspaceId,
                documentPresentationFactory: documentPresentationFactory,
                logger: logger);

        // Act
        InvalidOperationException thrownException =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => presentation.OpenDocumentAsync(
                    documentId,
                    TestContext.Current.CancellationToken));

        // Assert: preserve the original exception and failed-presentation cleanup.
        Assert.Same(originalException, thrownException);
        Assert.Empty(presentation.PresentedDocumentIds);

        RecordedLogEntry logEntry =
            Assert.Single(logger.Entries);

        Assert.Equal(LogLevel.Error, logEntry.Level);
        Assert.Contains(
            LogMessages.WorkspacePresentationDocumentOpenSafeFailure,
            logEntry.Message);
        Assert.Contains(
            $"OperationCode={DiagnosticOperationCodes.WorkspacePresentationDocumentOpenFailure}",
            logEntry.Message);
        Assert.Contains(
            $"FailureType={nameof(InvalidOperationException)}",
            logEntry.Message);

        Assert.DoesNotContain("{WorkspaceId}", logEntry.Message);
        Assert.DoesNotContain("{DocumentId}", logEntry.Message);
        Assert.DoesNotContain("{Description}", logEntry.Message);
        // The logger receives no exception object or runtime workspace/document IDs.
        Assert.Null(logEntry.Exception);
        Assert.DoesNotContain(workspaceId.ToString(), logEntry.Message);
        Assert.DoesNotContain(documentId.ToString(), logEntry.Message);
        Assert.DoesNotContain("WorkspaceId", logEntry.Properties.Keys);
        Assert.DoesNotContain("DocumentId", logEntry.Properties.Keys);
        Assert.DoesNotContain("Description", logEntry.Properties.Keys);

        foreach (string marker in sensitiveMarkers)
        {
            Assert.DoesNotContain(marker, logEntry.Message);

            foreach (object? value in logEntry.Properties.Values)
            {
                Assert.DoesNotContain(
                    marker,
                    value?.ToString() ?? string.Empty);
            }
        }
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
                documentId, 0))
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
                documentId, 0),
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
                documentId, 0))
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
                documentId, 0),
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
                firstDocumentId, 0))
            .Returns(firstPresentation);

        documentPresentationFactory
            .Setup(x => x.Create(
                workspaceId,
                secondDocumentId, 0))
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
                documentId, 0))
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

        await presentation.SetWorkspaceDocumentsAsync([documentId], TestContext.Current.CancellationToken);

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
                documentId, 0))
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

        await presentation.SetWorkspaceDocumentsAsync([documentId], TestContext.Current.CancellationToken);

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
                documentId, 0),
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
                firstDocumentId, 0))
            .Returns(firstPresentation);

        documentPresentationFactory
            .Setup(x => x.Create(
                workspaceId,
                secondDocumentId, 0))
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
                documentId, 0),
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
                documentId, 0))
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

        await presentation.SetWorkspaceDocumentsAsync([documentId], TestContext.Current.CancellationToken);

        presentation.OpenDocument(
            documentId);

        TaskCompletionSource<bool> updateCompleted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        presentation.WorkspaceDocumentsUpdated +=
            (_, _) => updateCompleted.TrySetResult(true);

        view.View.Raise(
            x => x.RemoveDocumentsRequested += null,
            EventArgs.Empty);

        await updateCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

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
    public async Task Dispose_ClearsRetainedWorkspaceStateAndDisposesView()
    {
        Guid workspaceId = Guid.NewGuid();
        Guid documentId = Guid.NewGuid();

        WorkspacePresentationViewHarness view =
            CreateViewHarness();

        WorkspaceDocumentPresentation documentPresentation =
            CreateDocumentPresentation(
                workspaceId,
                documentId);

        var documentPresentationFactory =
            new Mock<IWorkspaceDocumentPresentationFactory>();

        documentPresentationFactory
            .Setup(x => x.Create(
                workspaceId,
                documentId, 0))
            .Returns(documentPresentation);

        WorkspacePresentation presentation =
            CreatePresentation(
                WorkspaceType.Persistent,
                "Sensitive workspace name",
                "Sensitive workspace description",
                view,
                workspaceId: workspaceId,
                documentPresentationFactory:
                    documentPresentationFactory);

        // Arrange: retain workspace membership and an opened document presentation.
        await presentation.SetWorkspaceDocumentsAsync(
            [documentId],
            TestContext.Current.CancellationToken);

        WorkspaceDocumentPresentation openedPresentation =
            presentation.OpenDocument(documentId);

        Assert.Contains(
            documentId,
            presentation.WorkspaceDocumentIds);

        Assert.Contains(
            documentId,
            presentation.PresentedDocumentIds);

        // Act: dispose the workspace presentation.
        presentation.Dispose();
        presentation.Dispose();

        // Assert: sensitive workspace metadata and retained state are cleared.
        Assert.Null(presentation.WorkspaceName);
        Assert.Null(presentation.WorkspaceDescription);
        Assert.Null(presentation.ActiveDocumentId);

        Assert.Empty(presentation.WorkspaceDocumentIds);
        Assert.Empty(presentation.PresentedDocumentIds);

        Assert.Throws<ObjectDisposedException>(
            () => openedPresentation.Activate());

        view.View.Verify(
            x => x.ClearPresentation(),
            Times.Once);

        view.View.Verify(
            x => x.Dispose(),
            Times.Once);
    }

    [Fact]
    public async Task Dispose_WhenDocumentViewDisposalThrows_StillDisposesRemainingPresentationsAndClearsWorkspace()
    {
        Guid workspaceId = Guid.NewGuid();
        Guid firstDocumentId = Guid.NewGuid();
        Guid secondDocumentId = Guid.NewGuid();

        var firstDocumentView =
            new Mock<IDocumentWorkspaceView>();

        var firstDisposableView =
            firstDocumentView.As<IDisposable>();

        firstDisposableView
            .Setup(x => x.Dispose())
            .Throws(new InvalidOperationException(
                "First document view disposal failed."));

        var secondDocumentView =
            new Mock<IDocumentWorkspaceView>();

        var secondDisposableView =
            secondDocumentView.As<IDisposable>();

        WorkspaceDocumentPresentation firstDocumentPresentation =
            CreateDocumentPresentation(
                workspaceId,
                firstDocumentId,
                documentView: firstDocumentView);

        WorkspaceDocumentPresentation secondDocumentPresentation =
            CreateDocumentPresentation(
                workspaceId,
                secondDocumentId,
                documentView: secondDocumentView);

        var documentPresentationFactory =
            new Mock<IWorkspaceDocumentPresentationFactory>();

        documentPresentationFactory
            .Setup(x => x.Create(
                workspaceId,
                firstDocumentId, 0))
            .Returns(firstDocumentPresentation);

        documentPresentationFactory
            .Setup(x => x.Create(
                workspaceId,
                secondDocumentId, 0))
            .Returns(secondDocumentPresentation);

        WorkspacePresentationViewHarness workspaceView =
            CreateViewHarness();

        var disposableWorkspaceView =
            workspaceView.View.As<IDisposable>();

        WorkspacePresentation presentation =
            CreatePresentation(
                WorkspaceType.Persistent,
                "Sensitive workspace name",
                "Sensitive workspace description",
                workspaceView,
                workspaceId: workspaceId,
                documentPresentationFactory: documentPresentationFactory);

        await presentation.SetWorkspaceDocumentsAsync(
            [firstDocumentId, secondDocumentId],
            TestContext.Current.CancellationToken);

        presentation.OpenDocument(firstDocumentId);
        presentation.OpenDocument(secondDocumentId);

        // The first document view throws during disposal.
        Assert.Throws<InvalidOperationException>(
            () => presentation.Dispose());

        // Remaining document presentation must still be disposed.
        secondDisposableView.Verify(
            x => x.Dispose(),
            Times.Once);

        // Workspace-level cleanup must still run.
        workspaceView.View.Verify(
            x => x.ClearPresentation(),
            Times.Once);

        disposableWorkspaceView.Verify(
            x => x.Dispose(),
            Times.Once);

        // Retained workspace and document state must be cleared.
        Assert.Null(presentation.WorkspaceName);
        Assert.Null(presentation.WorkspaceDescription);
        Assert.Null(presentation.ActiveDocumentId);

        Assert.Empty(presentation.WorkspaceDocumentIds);
        Assert.Empty(presentation.PresentedDocumentIds);
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

        await presentation.SetWorkspaceDocumentsAsync([documentId], TestContext.Current.CancellationToken);

        view.View.Raise(
            x => x.RemoveDocumentsRequested += null,
            EventArgs.Empty);

        Assert.Contains(
            documentId,
            presentation.WorkspaceDocumentIds);
    }

    [Fact]
    public async Task SetWorkspaceDocumentsAsync_WhenDisposedBeforeLookupCompletes_DoesNotUpdateView()
    {
        Guid documentId = Guid.NewGuid();

        Document document =
            Document.Create(
                documentId,
                "delayed.csv",
                "delayed",
                "test-sha256",
                $"Data\\{documentId}\\delayed.csv");

        var repository =
            new Mock<IDocumentRepository>();

        var lookupCompletion =
            new TaskCompletionSource<IReadOnlyList<Document>>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        WorkspacePresentationViewHarness view =
            CreateViewHarness();

        WorkspacePresentation presentation =
            CreatePresentation(
                WorkspaceType.Persistent,
                "Workspace",
                null,
                view,
                documentRepository: repository);

        repository
            .Setup(x => x.GetAllAsync(
                It.IsAny<CancellationToken>()))
            .Returns(() => lookupCompletion.Task);

        Task updateTask =
            presentation.SetWorkspaceDocumentsAsync(
                [documentId],
                TestContext.Current.CancellationToken);

        // Simulate the workspace being closed by the security lock lifecycle.
        presentation.Dispose();

        Assert.Empty(presentation.WorkspaceDocumentIds);

        // The previously started query completes after disposal.
        lookupCompletion.SetResult([document]);

        await updateTask;

        view.View.Verify(
            x => x.SetWorkspaceDocuments(
                It.IsAny<IReadOnlyDictionary<Guid, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task SetWorkspaceDocumentsAsync_WhenSessionLocksDuringLookup_DoesNotPublishStaleWorkspaceDocuments()
    {
        Guid documentId = Guid.NewGuid();

        Document document =
            Document.Create(
                documentId,
                "security-policy.md",
                "Security Policy",
                "test-sha256",
                $"Data\\{documentId}\\security-policy.md");

        var lookupStarted =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var lookupCompletion =
            new TaskCompletionSource<IReadOnlyList<Document>>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetAllAsync(
                It.IsAny<CancellationToken>()))
            .Callback<CancellationToken>(
                _ => lookupStarted.TrySetResult())
            .Returns(lookupCompletion.Task);

        var securitySession =
            new Mock<IApplicationSecuritySession>();

        ApplicationSecuritySessionSnapshot currentSnapshot =
            new(
                ApplicationSecuritySessionState.Authenticated,
                0);

        securitySession
            .SetupGet(x => x.CurrentSnapshot)
            .Returns(() => currentSnapshot);

        WorkspacePresentationViewHarness view =
            CreateViewHarness();

        WorkspacePresentation presentation =
            CreatePresentation(
                WorkspaceType.Persistent,
                "Workspace",
                null,
                view,
                documentRepository: repository,
                applicationSecuritySession: securitySession.Object);

        // Arrange: begin loading workspace document metadata.
        Task updateTask =
            presentation.SetWorkspaceDocumentsAsync(
                [documentId],
                TestContext.Current.CancellationToken);

        await lookupStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        // Act: lock the session before the metadata lookup completes.
        currentSnapshot =
            new ApplicationSecuritySessionSnapshot(
                ApplicationSecuritySessionState.Locked,
                1);

        securitySession.Raise(
            x => x.StateChanged += null,
            currentSnapshot);

        // Complete the lookup with authenticated-era metadata.
        lookupCompletion.SetResult([document]);

        await updateTask;

        // Assert: stale metadata must not reach the workspace view.
        view.View.Verify(
            x => x.SetWorkspaceDocuments(
                It.IsAny<IReadOnlyDictionary<Guid, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task AddDocumentsRequested_WhenDisposedDuringDocumentLookup_DoesNotShowPicker()
    {
        Guid documentId = Guid.NewGuid();

        Document document =
            Document.Create(
                documentId,
                "delayed.csv",
                "delayed",
                "test-sha256",
                $"Data\\{documentId}\\delayed.csv");

        var repository =
            new Mock<IDocumentRepository>();

        var lookupStarted =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var lookupCompletion =
            new TaskCompletionSource<IReadOnlyList<Document>>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        WorkspacePresentationViewHarness view =
            CreateViewHarness();

        var dialogService =
            new Mock<IWorkspaceDialogService>();

        WorkspacePresentation presentation =
            CreatePresentation(
                WorkspaceType.Persistent,
                "Workspace",
                null,
                view,
                dialogService,
                documentRepository: repository);

        repository
            .Setup(x => x.GetAllAsync(
                It.IsAny<CancellationToken>()))
            .Returns((CancellationToken _) =>
            {
                lookupStarted.TrySetResult();
                return lookupCompletion.Task;
            });

        view.View.Raise(
            x => x.AddDocumentsRequested += null,
            EventArgs.Empty);

        await lookupStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        presentation.Dispose();

        lookupCompletion.SetResult([document]);

        await Task.Delay(
            100,
            TestContext.Current.CancellationToken);

        dialogService.Verify(
            x => x.ShowDocumentPicker(
                It.IsAny<IReadOnlyList<Document>>(),
                It.IsAny<IReadOnlyCollection<Guid>>()),
            Times.Never);
    }

    [Fact]
    public async Task AddDocumentsRequested_WhenSessionLocksDuringDocumentLookup_DoesNotShowPicker()
    {
        Guid documentId = Guid.NewGuid();

        Document document =
            Document.Create(
                documentId,
                "security-policy.md",
                "Security Policy",
                "test-sha256",
                $"Data\\{documentId}\\security-policy.md");

        var lookupStarted =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var lookupCompletion =
            new TaskCompletionSource<IReadOnlyList<Document>>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var documentRepository =
            new Mock<IDocumentRepository>();

        documentRepository
            .Setup(x => x.GetAllAsync(
                It.IsAny<CancellationToken>()))
            .Callback<CancellationToken>(
                _ => lookupStarted.TrySetResult())
            .Returns(lookupCompletion.Task);

        var securitySession =
            new Mock<IApplicationSecuritySession>();

        ApplicationSecuritySessionSnapshot currentSnapshot =
            new(
                ApplicationSecuritySessionState.Authenticated,
                0);

        securitySession
            .SetupGet(x => x.CurrentSnapshot)
            .Returns(() => currentSnapshot);

        var dialogService =
            new Mock<IWorkspaceDialogService>();

        WorkspacePresentationViewHarness view =
            CreateViewHarness();

        WorkspacePresentation presentation =
            CreatePresentation(
                WorkspaceType.Persistent,
                "Workspace",
                null,
                view,
                dialogService,
                documentRepository: documentRepository,
                applicationSecuritySession: securitySession.Object);

        // Arrange: begin retrieving documents for the selection dialog.
        view.View.Raise(
            x => x.AddDocumentsRequested += null,
            EventArgs.Empty);

        await lookupStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        // Act: lock the authoritative session before the lookup completes.
        currentSnapshot =
            new ApplicationSecuritySessionSnapshot(
                ApplicationSecuritySessionState.Locked,
                1);

        securitySession.Raise(
            x => x.StateChanged += null,
            currentSnapshot);

        // Complete the lookup with authenticated-era document metadata.
        lookupCompletion.SetResult([document]);

        await Task.Delay(
            100,
            TestContext.Current.CancellationToken);

        // Assert: the stale result must not be passed to the picker.
        dialogService.Verify(
            x => x.ShowDocumentPicker(
                It.IsAny<IReadOnlyList<Document>>(),
                It.IsAny<IEnumerable<Guid>>()),
            Times.Never);

        Assert.Empty(presentation.WorkspaceDocumentIds);

        Assert.Empty(presentation.PresentedDocumentIds);

        presentation.Dispose();
    }

    [Fact]
    public async Task AddDocumentsRequested_WhenDisposedByDocumentPicker_DoesNotMutateWorkspace()
    {
        Guid workspaceId = Guid.NewGuid();
        Guid documentId = Guid.NewGuid();

        Document document =
            Document.Create(
                documentId,
                "selected.csv",
                "selected",
                "test-sha256",
                $"Data\\{documentId}\\selected.csv");

        Workspace workspace =
            Workspace.CreatePersistent(
                workspaceId,
                "Workspace");

        var repository =
            new Mock<IDocumentRepository>();

        var workspaceRepository =
            new Mock<IWorkspaceRepository>();

        workspaceRepository
            .Setup(x => x.UpdateAsync(
                It.IsAny<Workspace>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var activeWorkspaceRegistry =
            new Mock<IActiveWorkspaceRegistry>();

        activeWorkspaceRegistry
            .Setup(x => x.Get(workspaceId))
            .Returns(workspace);

        WorkspacePresentationViewHarness view =
            CreateViewHarness();

        var dialogService =
            new Mock<IWorkspaceDialogService>();

        WorkspacePresentation? presentation = null;

        presentation =
            CreatePresentation(
                WorkspaceType.Persistent,
                "Workspace",
                null,
                view,
                dialogService,
                workspaceRepository,
                activeWorkspaceRegistry,
                workspaceId,
                documentRepository: repository);

        // Configure after CreatePresentation because the helper supplies
        // a default empty result for GetAllAsync.
        repository
            .Setup(x => x.GetAllAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([document]);

        repository
            .Setup(x => x.GetByIdAsync(
                documentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        dialogService
            .Setup(x => x.ShowDocumentPicker(
                It.IsAny<IReadOnlyList<Document>>(),
                It.IsAny<IEnumerable<Guid>>()))
            .Callback(() => presentation.Dispose())
            .Returns(
                new WorkspaceDocumentSelectionResult(
                    [documentId]));

        view.View.Raise(
            x => x.AddDocumentsRequested += null,
            EventArgs.Empty);

        await Task.Delay(
            150,
            TestContext.Current.CancellationToken);

        dialogService.Verify(
            x => x.ShowDocumentPicker(
                It.IsAny<IReadOnlyList<Document>>(),
                It.IsAny<IEnumerable<Guid>>()),
            Times.Once);

        workspaceRepository.Verify(
            x => x.UpdateAsync(
                It.IsAny<Workspace>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        activeWorkspaceRegistry.Verify(
            x => x.Replace(It.IsAny<Workspace>()),
            Times.Never);

        Assert.Empty(presentation.WorkspaceDocumentIds);
    }

    [Fact]
    public async Task AddDocumentsRequested_WhenDisposedDuringWorkspaceUpdate_DoesNotRestoreMembership()
    {
        Guid workspaceId = Guid.NewGuid();
        Guid documentId = Guid.NewGuid();

        Document document = Document.Create(
            documentId,
            "document.csv",
            "Document",
            "test-sha256",
            $"Data\\{documentId}\\document.csv");

        Workspace workspace = Workspace.CreatePersistent(
            workspaceId,
            "Workspace");

        var updateStarted =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var updateRelease =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var documentRepository =
            new Mock<IDocumentRepository>();

        documentRepository
            .Setup(x => x.GetAllAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([document]);

        documentRepository
            .Setup(x => x.GetByIdAsync(
                documentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var workspaceRepository =
            new Mock<IWorkspaceRepository>();

        workspaceRepository
            .Setup(x => x.UpdateAsync(
                It.IsAny<Workspace>(),
                It.IsAny<CancellationToken>()))
            .Callback<Workspace, CancellationToken>(
                (_, _) => updateStarted.TrySetResult(true))
            .Returns(updateRelease.Task);

        var activeWorkspaceRegistry =
            new Mock<IActiveWorkspaceRegistry>();

        activeWorkspaceRegistry
            .Setup(x => x.Get(workspaceId))
            .Returns(workspace);

        var dialogService =
            new Mock<IWorkspaceDialogService>();

        dialogService
            .Setup(x => x.ShowDocumentPicker(
                It.IsAny<IReadOnlyList<Document>>(),
                It.IsAny<IEnumerable<Guid>>()))
            .Returns(
                new WorkspaceDocumentSelectionResult([documentId]));

        var documentPresentationFactory =
            new Mock<IWorkspaceDocumentPresentationFactory>();

        WorkspacePresentationViewHarness view =
            CreateViewHarness();

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
                documentPresentationFactory,
                documentRepository: documentRepository);

        int updatedEventCount = 0;

        presentation.WorkspaceDocumentsUpdated +=
            (_, _) => updatedEventCount++;

        view.View.Raise(
            x => x.AddDocumentsRequested += null,
            EventArgs.Empty);

        await updateStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        presentation.Dispose();

        updateRelease.TrySetResult();

        await Task.Delay(
            100,
            TestContext.Current.CancellationToken);

        Assert.Empty(presentation.WorkspaceDocumentIds);
        Assert.Empty(presentation.PresentedDocumentIds);
        Assert.Equal(0, updatedEventCount);

        view.View.Verify(
            x => x.SetWorkspaceDocuments(
                It.IsAny<IReadOnlyDictionary<Guid, string>>()),
            Times.Never);

        documentPresentationFactory.Verify(
            x => x.Create(
                workspaceId,
                documentId, 0),
            Times.Never);
    }

    [Fact]
    public async Task RemoveDocumentsRequested_WhenDisposedByRemovalDialog_DoesNotMutateWorkspace()
    {
        Guid workspaceId = Guid.NewGuid();
        Guid documentId = Guid.NewGuid();

        Document document =
            Document.Create(
                documentId,
                "selected.csv",
                "selected",
                "test-sha256",
                $"Data\\{documentId}\\selected.csv");

        Workspace workspace =
            Workspace.CreatePersistent(
                workspaceId,
                "Workspace");

        workspace.AddDocument(documentId);

        var repository =
            new Mock<IDocumentRepository>();

        var workspaceRepository =
            new Mock<IWorkspaceRepository>();

        workspaceRepository
            .Setup(x => x.UpdateAsync(
                It.IsAny<Workspace>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var activeWorkspaceRegistry =
            new Mock<IActiveWorkspaceRegistry>();

        activeWorkspaceRegistry
            .Setup(x => x.Get(workspaceId))
            .Returns(workspace);

        WorkspacePresentationViewHarness view =
            CreateViewHarness();

        var dialogService =
            new Mock<IWorkspaceDialogService>();

        WorkspacePresentation? presentation = null;

        presentation =
            CreatePresentation(
                WorkspaceType.Persistent,
                "Workspace",
                null,
                view,
                dialogService,
                workspaceRepository,
                activeWorkspaceRegistry,
                workspaceId,
                documentRepository: repository);

        repository
            .Setup(x => x.GetAllAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([document]);

        await presentation.SetWorkspaceDocumentsAsync(
            [documentId],
            TestContext.Current.CancellationToken);

        dialogService
            .Setup(x => x.ShowDocumentRemoval(
                It.IsAny<IReadOnlyList<Document>>()))
            .Callback(() => presentation.Dispose())
            .Returns(
                new WorkspaceDocumentSelectionResult(
                    [documentId]));

        view.View.Raise(
            x => x.RemoveDocumentsRequested += null,
            EventArgs.Empty);

        await Task.Delay(
            150,
            TestContext.Current.CancellationToken);

        dialogService.Verify(
            x => x.ShowDocumentRemoval(
                It.IsAny<IReadOnlyList<Document>>()),
            Times.Once);

        workspaceRepository.Verify(
            x => x.UpdateAsync(
                It.IsAny<Workspace>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        activeWorkspaceRegistry.Verify(
            x => x.Replace(It.IsAny<Workspace>()),
            Times.Never);

        Assert.Contains(
            documentId,
            workspace.Memberships.Select(
                membership => membership.DocumentId));
    }

    [Fact]
    public async Task RemoveDocumentsRequested_WhenDisposedDuringDocumentLookup_DoesNotShowRemovalDialog()
    {
        Guid documentId = Guid.NewGuid();

        Document document =
            Document.Create(
                documentId,
                "delayed.csv",
                "delayed",
                "test-sha256",
                $"Data\\{documentId}\\delayed.csv");

        var repository =
            new Mock<IDocumentRepository>();

        var lookupStarted =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var lookupCompletion =
            new TaskCompletionSource<IReadOnlyList<Document>>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        WorkspacePresentationViewHarness view =
            CreateViewHarness();

        var dialogService =
            new Mock<IWorkspaceDialogService>();

        WorkspacePresentation presentation =
            CreatePresentation(
                WorkspaceType.Persistent,
                "Workspace",
                null,
                view,
                dialogService,
                documentRepository: repository);

        repository
            .Setup(x => x.GetAllAsync(
                It.IsAny<CancellationToken>()))
            .Returns((CancellationToken _) =>
            {
                lookupStarted.TrySetResult();
                return lookupCompletion.Task;
            });

        view.View.Raise(
            x => x.RemoveDocumentsRequested += null,
            EventArgs.Empty);

        await lookupStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        presentation.Dispose();

        lookupCompletion.SetResult([document]);

        await Task.Delay(
            100,
            TestContext.Current.CancellationToken);

        dialogService.Verify(
            x => x.ShowDocumentRemoval(
                It.IsAny<IReadOnlyList<Document>>()),
            Times.Never);
    }

    [Fact]
    public async Task RemoveDocumentsRequested_WhenSessionLocksDuringDocumentLookup_DoesNotShowRemovalDialog()
    {
        Guid documentId = Guid.NewGuid();

        Document document =
            Document.Create(
                documentId,
                "security-policy.md",
                "Security Policy",
                "test-sha256",
                $"Data\\{documentId}\\security-policy.md");

        var lookupStarted =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var lookupCompletion =
            new TaskCompletionSource<IReadOnlyList<Document>>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var documentRepository =
            new Mock<IDocumentRepository>();

        documentRepository
            .Setup(x => x.GetAllAsync(
                It.IsAny<CancellationToken>()))
            .Callback<CancellationToken>(
                _ => lookupStarted.TrySetResult())
            .Returns(lookupCompletion.Task);

        var securitySession =
            new Mock<IApplicationSecuritySession>();

        ApplicationSecuritySessionSnapshot currentSnapshot =
            new(
                ApplicationSecuritySessionState.Authenticated,
                0);

        securitySession
            .SetupGet(x => x.CurrentSnapshot)
            .Returns(() => currentSnapshot);

        var dialogService =
            new Mock<IWorkspaceDialogService>();

        WorkspacePresentationViewHarness view =
            CreateViewHarness();

        WorkspacePresentation presentation =
            CreatePresentation(
                WorkspaceType.Persistent,
                "Workspace",
                null,
                view,
                dialogService,
                documentRepository: documentRepository,
                applicationSecuritySession: securitySession.Object);

        view.View.Raise(
            x => x.RemoveDocumentsRequested += null,
            EventArgs.Empty);

        await lookupStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        currentSnapshot =
            new ApplicationSecuritySessionSnapshot(
                ApplicationSecuritySessionState.Locked,
                1);

        securitySession.Raise(
            x => x.StateChanged += null,
            currentSnapshot);

        lookupCompletion.SetResult([document]);

        await Task.Delay(
            100,
            TestContext.Current.CancellationToken);

        dialogService.Verify(
            x => x.ShowDocumentRemoval(
                It.IsAny<IReadOnlyList<Document>>()),
            Times.Never);

        presentation.Dispose();
    }

    [Fact]
    public async Task RemoveDocumentsRequested_WhenDisposedDuringWorkspaceUpdate_DoesNotContinueRemoval()
    {
        Guid workspaceId = Guid.NewGuid();
        Guid documentId = Guid.NewGuid();

        Document document = Document.Create(
            documentId,
            "document.csv",
            "Document",
            "test-sha256",
            $"Data\\{documentId}\\document.csv");

        Workspace workspace = Workspace.CreatePersistent(
            workspaceId,
            "Workspace");

        workspace.AddDocument(documentId);

        var updateStarted =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var updateRelease =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var documentRepository =
            new Mock<IDocumentRepository>();

        documentRepository
            .Setup(x => x.GetAllAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([document]);

        var workspaceRepository =
            new Mock<IWorkspaceRepository>();

        workspaceRepository
            .Setup(x => x.UpdateAsync(
                It.IsAny<Workspace>(),
                It.IsAny<CancellationToken>()))
            .Callback<Workspace, CancellationToken>(
                (_, _) => updateStarted.TrySetResult(true))
            .Returns(updateRelease.Task);

        var activeWorkspaceRegistry =
            new Mock<IActiveWorkspaceRegistry>();

        activeWorkspaceRegistry
            .Setup(x => x.Get(workspaceId))
            .Returns(workspace);

        var dialogService =
            new Mock<IWorkspaceDialogService>();

        dialogService
            .Setup(x => x.ShowDocumentRemoval(
                It.IsAny<IReadOnlyList<Document>>()))
            .Returns(
                new WorkspaceDocumentSelectionResult([documentId]));

        WorkspacePresentationViewHarness view =
            CreateViewHarness();

        var logger =
            new RecordingLogger<WorkspacePresentation>();

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
                logger: logger,
                documentRepository: documentRepository);

        await presentation.SetWorkspaceDocumentsAsync(
            [documentId],
            TestContext.Current.CancellationToken);

        int updatedEventCount = 0;

        presentation.WorkspaceDocumentsUpdated +=
            (_, _) => updatedEventCount++;

        view.View.Raise(
            x => x.RemoveDocumentsRequested += null,
            EventArgs.Empty);

        await updateStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        presentation.Dispose();

        updateRelease.TrySetResult();

        await Task.Delay(
            100,
            TestContext.Current.CancellationToken);

        Assert.Empty(presentation.WorkspaceDocumentIds);
        Assert.Empty(presentation.PresentedDocumentIds);
        Assert.Equal(0, updatedEventCount);

        view.View.Verify(
            x => x.SetWorkspaceDocuments(
                It.IsAny<IReadOnlyDictionary<Guid, string>>()),
            Times.Once);

        view.View.Verify(
            x => x.RemoveDocumentPresentation(documentId),
            Times.Never);

        Assert.DoesNotContain(
            logger.Entries,
            entry => entry.Level == LogLevel.Error);
    }

    [Fact]
    public async Task UpdateWorkspaceDetails_WhenDisposedByDialog_DoesNotUpdateWorkspace()
    {
        Guid workspaceId = Guid.NewGuid();

        Workspace workspace =
            Workspace.CreatePersistent(
                workspaceId,
                "Workspace");

        var workspaceRepository =
            new Mock<IWorkspaceRepository>();

        workspaceRepository
            .Setup(x => x.UpdateAsync(
                It.IsAny<Workspace>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var activeWorkspaceRegistry =
            new Mock<IActiveWorkspaceRegistry>();

        activeWorkspaceRegistry
            .Setup(x => x.Get(workspaceId))
            .Returns(workspace);

        WorkspacePresentationViewHarness view =
            CreateViewHarness();

        var dialogService =
            new Mock<IWorkspaceDialogService>();

        WorkspacePresentation? presentation = null;

        presentation =
            CreatePresentation(
                WorkspaceType.Persistent,
                "Workspace",
                null,
                view,
                dialogService,
                workspaceRepository,
                activeWorkspaceRegistry,
                workspaceId);

        dialogService
            .Setup(x => x.ShowWorkspaceDetails(
                "Workspace",
                null,
                UiMessages.UpdateWorkspaceDetailsTitle))
            .Callback(() => presentation.Dispose())
            .Returns(
                new WorkspaceDetailsDialogResult(
                    "Updated Workspace",
                    "Updated description"));

        view.View.Raise(
            x => x.UpdateWorkspaceDetailsRequested += null,
            EventArgs.Empty);

        await Task.Delay(
            150,
            TestContext.Current.CancellationToken);

        dialogService.Verify(
            x => x.ShowWorkspaceDetails(
                "Workspace",
                null,
                UiMessages.UpdateWorkspaceDetailsTitle),
            Times.Once);

        workspaceRepository.Verify(
            x => x.UpdateAsync(
                It.IsAny<Workspace>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        activeWorkspaceRegistry.Verify(
            x => x.Replace(It.IsAny<Workspace>()),
            Times.Never);

        view.View.Verify(
            x => x.SetWorkspaceIdentity(
                "Updated Workspace",
                "Updated description"),
            Times.Never);
    }

    [Fact]
    public async Task UpdateWorkspaceDetails_WhenSessionLocksDuringDialog_DoesNotUpdateWorkspace()
    {
        Guid workspaceId = Guid.NewGuid();

        Workspace workspace =
            Workspace.CreatePersistent(
                workspaceId,
                "Workspace");

        var workspaceRepository =
            new Mock<IWorkspaceRepository>();

        workspaceRepository
            .Setup(x => x.UpdateAsync(
                It.IsAny<Workspace>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var activeWorkspaceRegistry =
            new Mock<IActiveWorkspaceRegistry>();

        activeWorkspaceRegistry
            .Setup(x => x.Get(workspaceId))
            .Returns(workspace);

        var securitySession =
            new Mock<IApplicationSecuritySession>();

        ApplicationSecuritySessionSnapshot currentSnapshot =
            new(
                ApplicationSecuritySessionState.Authenticated,
                0);

        securitySession
            .SetupGet(x => x.CurrentSnapshot)
            .Returns(() => currentSnapshot);

        WorkspacePresentationViewHarness view =
            CreateViewHarness();

        var dialogService =
            new Mock<IWorkspaceDialogService>();

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
                applicationSecuritySession: securitySession.Object);

        dialogService
            .Setup(x => x.ShowWorkspaceDetails(
                "Workspace",
                null,
                UiMessages.UpdateWorkspaceDetailsTitle))
            .Callback(() =>
            {
                currentSnapshot =
                    new ApplicationSecuritySessionSnapshot(
                        ApplicationSecuritySessionState.Locked,
                        1);

                securitySession.Raise(
                    x => x.StateChanged += null,
                    currentSnapshot);
            })
            .Returns(
                new WorkspaceDetailsDialogResult(
                    "Updated Workspace",
                    "Updated description"));

        view.View.Raise(
            x => x.UpdateWorkspaceDetailsRequested += null,
            EventArgs.Empty);

        await Task.Delay(
            100,
            TestContext.Current.CancellationToken);

        dialogService.Verify(
            x => x.ShowWorkspaceDetails(
                "Workspace",
                null,
                UiMessages.UpdateWorkspaceDetailsTitle),
            Times.Once);

        workspaceRepository.Verify(
            x => x.UpdateAsync(
                It.IsAny<Workspace>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        activeWorkspaceRegistry.Verify(
            x => x.Replace(It.IsAny<Workspace>()),
            Times.Never);

        view.View.Verify(
            x => x.SetWorkspaceIdentity(
                "Updated Workspace",
                "Updated description"),
            Times.Never);

        Assert.Equal("Workspace", presentation.WorkspaceName);
        Assert.Null(presentation.WorkspaceDescription);

        presentation.Dispose();
    }

    [Fact]
    public async Task SaveAsWorkspace_WhenDisposedByDialog_DoesNotSaveWorkspace()
    {
        Guid workspaceId = Guid.NewGuid();

        Workspace workspace =
            Workspace.CreateTemporary(workspaceId);

        var workspaceRepository =
            new Mock<IWorkspaceRepository>();

        workspaceRepository
            .Setup(x => x.AddAsync(
                It.IsAny<Workspace>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var activeWorkspaceRegistry =
            new Mock<IActiveWorkspaceRegistry>();

        activeWorkspaceRegistry
            .Setup(x => x.Get(workspaceId))
            .Returns(workspace);

        WorkspacePresentationViewHarness view =
            CreateViewHarness();

        var dialogService =
            new Mock<IWorkspaceDialogService>();

        WorkspacePresentation? presentation = null;

        presentation =
            CreatePresentation(
                WorkspaceType.Temporary,
                null,
                null,
                view,
                dialogService,
                workspaceRepository,
                activeWorkspaceRegistry,
                workspaceId);

        dialogService
            .Setup(x => x.ShowWorkspaceDetails(
                string.Empty,
                null,
                UiMessages.SaveAsWorkspace))
            .Callback(() => presentation.Dispose())
            .Returns(
                new WorkspaceDetailsDialogResult(
                    "Saved Workspace",
                    "Description"));

        view.View.Raise(
            x => x.SaveAsWorkspaceRequested += null,
            EventArgs.Empty);

        await Task.Delay(
            150,
            TestContext.Current.CancellationToken);

        dialogService.Verify(
            x => x.ShowWorkspaceDetails(
                string.Empty,
                null,
                UiMessages.SaveAsWorkspace),
            Times.Once);

        workspaceRepository.Verify(
            x => x.AddAsync(
                It.IsAny<Workspace>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        activeWorkspaceRegistry.Verify(
            x => x.Replace(It.IsAny<Workspace>()),
            Times.Never);

        view.View.Verify(
            x => x.SetWorkspaceIdentity(
                "Saved Workspace",
                "Description"),
            Times.Never);
    }

    [Fact]
    public async Task SaveAsWorkspace_WhenSessionLocksDuringDialog_DoesNotSaveWorkspace()
    {
        Guid workspaceId = Guid.NewGuid();

        Workspace workspace =
            Workspace.CreateTemporary(workspaceId);

        var workspaceRepository =
            new Mock<IWorkspaceRepository>();

        workspaceRepository
            .Setup(x => x.AddAsync(
                It.IsAny<Workspace>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var activeWorkspaceRegistry =
            new Mock<IActiveWorkspaceRegistry>();

        activeWorkspaceRegistry
            .Setup(x => x.Get(workspaceId))
            .Returns(workspace);

        var securitySession =
            new Mock<IApplicationSecuritySession>();

        ApplicationSecuritySessionSnapshot currentSnapshot =
            new(
                ApplicationSecuritySessionState.Authenticated,
                0);

        securitySession
            .SetupGet(x => x.CurrentSnapshot)
            .Returns(() => currentSnapshot);

        WorkspacePresentationViewHarness view =
            CreateViewHarness();

        var dialogService =
            new Mock<IWorkspaceDialogService>();

        WorkspacePresentation presentation =
            CreatePresentation(
                WorkspaceType.Temporary,
                null,
                null,
                view,
                dialogService,
                workspaceRepository,
                activeWorkspaceRegistry,
                workspaceId,
                applicationSecuritySession: securitySession.Object);

        dialogService
            .Setup(x => x.ShowWorkspaceDetails(
                string.Empty,
                null,
                UiMessages.SaveAsWorkspace))
            .Callback(() =>
            {
                currentSnapshot =
                    new ApplicationSecuritySessionSnapshot(
                        ApplicationSecuritySessionState.Locked,
                        1);

                securitySession.Raise(
                    x => x.StateChanged += null,
                    currentSnapshot);
            })
            .Returns(
                new WorkspaceDetailsDialogResult(
                    "Saved Workspace",
                    "Description"));

        view.View.Raise(
            x => x.SaveAsWorkspaceRequested += null,
            EventArgs.Empty);

        await Task.Delay(
            100,
            TestContext.Current.CancellationToken);

        dialogService.Verify(
            x => x.ShowWorkspaceDetails(
                string.Empty,
                null,
                UiMessages.SaveAsWorkspace),
            Times.Once);

        workspaceRepository.Verify(
            x => x.AddAsync(
                It.IsAny<Workspace>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        activeWorkspaceRegistry.Verify(
            x => x.Replace(It.IsAny<Workspace>()),
            Times.Never);

        view.View.Verify(
            x => x.SetWorkspaceIdentity(
                "Saved Workspace",
                "Description"),
            Times.Never);

        Assert.True(presentation.IsTemporary);

        presentation.Dispose();
    }

    [Fact]
    public async Task OpenDocumentAsync_WhenDisposedDuringRender_DoesNotReattachDocumentPresentation()
    {
        Guid workspaceId = Guid.NewGuid();
        Guid documentId = Guid.NewGuid();

        Document document = Document.Create(
            documentId,
            "document.csv",
            "Document",
            "test-sha256",
            $"Data\\{documentId}\\document.csv");

        var renderStarted =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var renderRelease =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var documentView =
            new Mock<IDocumentWorkspaceView>();

        documentView
            .Setup(x => x.ShowDocumentAsync(
                It.IsAny<Stream>(),
                document.FileName,
                It.IsAny<CancellationToken>()))
            .Callback<Stream, string, CancellationToken>(
                (_, _, _) => renderStarted.TrySetResult(true))
            .Returns(renderRelease.Task);

        var documentRepository =
            new Mock<IDocumentRepository>();

        documentRepository
            .Setup(x => x.GetByIdAsync(
                documentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var documentReader =
            new Mock<IDocumentReader>();

        documentReader
            .Setup(x => x.OpenReadAsync(
                documentId,
                document.Sha256Hash,
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new MemoryStream([1, 2, 3]));

        WorkspaceDocumentPresentation documentPresentation =
            CreateDocumentPresentation(
                workspaceId,
                documentId,
                documentView: documentView,
                documentRepository: documentRepository,
                documentReader: documentReader);

        var documentPresentationFactory =
            new Mock<IWorkspaceDocumentPresentationFactory>();

        documentPresentationFactory
            .Setup(x => x.Create(workspaceId, documentId, 0))
            .Returns(documentPresentation);

        WorkspacePresentationViewHarness workspaceView =
            CreateViewHarness();

        WorkspacePresentation presentation =
            CreatePresentation(
                WorkspaceType.Persistent,
                "Workspace",
                null,
                workspaceView,
                workspaceId: workspaceId,
                documentPresentationFactory: documentPresentationFactory);

        Task openTask =
            presentation.OpenDocumentAsync(
                documentId,
                TestContext.Current.CancellationToken);

        await renderStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        presentation.Dispose();

        renderRelease.TrySetResult();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => openTask);

        Assert.Empty(presentation.PresentedDocumentIds);

        workspaceView.View.Verify(
            x => x.AddDocumentPresentation(
                It.IsAny<WorkspaceDocumentPresentation>()),
            Times.Never);

        workspaceView.View.Verify(
            x => x.ActivateDocumentPresentation(documentId),
            Times.Never);
    }

    [Fact]
    public async Task OpenDocumentAsync_WhenWorkspaceSessionLocksDuringRender_DoesNotAttachDocumentPresentation()
    {
        Guid workspaceId = Guid.NewGuid();
        Guid documentId = Guid.NewGuid();

        Document document =
            Document.Create(
                documentId,
                "security-policy.csv",
                "Security Policy",
                "test-sha256",
                $"Data\\{documentId}\\security-policy.csv");

        var renderStarted =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var renderRelease =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var documentView =
            new Mock<IDocumentWorkspaceView>();

        documentView
            .Setup(x => x.ShowDocumentAsync(
                It.IsAny<Stream>(),
                document.FileName,
                It.IsAny<CancellationToken>()))
            .Callback<Stream, string, CancellationToken>(
                (_, _, _) => renderStarted.TrySetResult(true))
            .Returns(renderRelease.Task);

        var documentRepository =
            new Mock<IDocumentRepository>();

        documentRepository
            .Setup(x => x.GetByIdAsync(
                documentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var documentReader =
            new Mock<IDocumentReader>();

        documentReader
            .Setup(x => x.OpenReadAsync(
                documentId,
                document.Sha256Hash,
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream([1, 2, 3]));

        // Keep the child presenter authenticated so this test isolates
        // the workspace presentation's own session-revision responsibility.
        WorkspaceDocumentPresentation documentPresentation =
            CreateDocumentPresentation(
                workspaceId,
                documentId,
                documentView: documentView,
                documentRepository: documentRepository,
                documentReader: documentReader);

        var documentPresentationFactory =
            new Mock<IWorkspaceDocumentPresentationFactory>();

        documentPresentationFactory
            .Setup(x => x.Create(
                workspaceId,
                documentId, 0))
            .Returns(documentPresentation);

        var workspaceSecuritySession =
            new Mock<IApplicationSecuritySession>();

        ApplicationSecuritySessionSnapshot currentSnapshot =
            new(
                ApplicationSecuritySessionState.Authenticated,
                0);

        workspaceSecuritySession
            .SetupGet(x => x.CurrentSnapshot)
            .Returns(() => currentSnapshot);

        WorkspacePresentationViewHarness workspaceView =
            CreateViewHarness();

        WorkspacePresentation presentation =
            CreatePresentation(
                WorkspaceType.Persistent,
                "Workspace",
                null,
                workspaceView,
                workspaceId: workspaceId,
                documentPresentationFactory: documentPresentationFactory,
                applicationSecuritySession: workspaceSecuritySession.Object);

        // Arrange: begin opening a document and pause during rendering.
        Task openTask =
            presentation.OpenDocumentAsync(
                documentId,
                TestContext.Current.CancellationToken);

        await renderStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        // Act: lock the workspace's authoritative session before rendering completes.
        currentSnapshot =
            new ApplicationSecuritySessionSnapshot(
                ApplicationSecuritySessionState.Locked,
                1);

        workspaceSecuritySession.Raise(
            x => x.StateChanged += null,
            currentSnapshot);

        renderRelease.TrySetResult();

        try
        {
            await openTask;
        }
        catch (OperationCanceledException)
        {
            // Cancellation is an acceptable outcome for an invalidated open.
        }

        // Assert: the stale child presentation must never be attached or activated.
        Assert.Empty(presentation.PresentedDocumentIds);
        Assert.Null(presentation.ActiveDocumentId);

        workspaceView.View.Verify(
            x => x.AddDocumentPresentation(
                It.IsAny<WorkspaceDocumentPresentation>()),
            Times.Never);

        workspaceView.View.Verify(
            x => x.ActivateDocumentPresentation(documentId),
            Times.Never);
    }

    [Fact]
    public void Dispose_ClearsRetainedWorkspaceMetadata()
    {
        WorkspacePresentationViewHarness view =
            CreateViewHarness();

        WorkspacePresentation presentation =
            CreatePresentation(
                WorkspaceType.Persistent,
                "Confidential Workspace",
                "Confidential workspace description",
                view);

        presentation.Dispose();

        Assert.Null(presentation.WorkspaceName);
        Assert.Null(presentation.WorkspaceDescription);

        Assert.Empty(presentation.WorkspaceDocumentIds);
        Assert.Empty(presentation.PresentedDocumentIds);
        Assert.Null(presentation.ActiveDocumentId);

        view.View.Verify(
            x => x.ClearPresentation(),
            Times.Once);

        view.View.Verify(
            x => x.Dispose(),
            Times.Once);
    }

    [Fact]
    public async Task AddDocumentsRequested_WhenDisposedDuringWorkspaceRefresh_DoesNotContinueOpeningDocuments()
    {
        Guid workspaceId = Guid.NewGuid();
        Guid documentId = Guid.NewGuid();

        Document document = Document.Create(
            documentId,
            "document.csv",
            "Document",
            "test-sha256",
            $"Data\\{documentId}\\document.csv");

        Workspace workspace = Workspace.CreatePersistent(
            workspaceId,
            "Workspace");

        var refreshStarted =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var refreshCompletion =
            new TaskCompletionSource<IReadOnlyList<Document>>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        int getAllCallCount = 0;

        var documentRepository =
            new Mock<IDocumentRepository>();

        documentRepository
            .Setup(x => x.GetAllAsync(
                It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                int callNumber =
                    Interlocked.Increment(ref getAllCallCount);

                if (callNumber == 1)
                {
                    return Task.FromResult<IReadOnlyList<Document>>(
                        [document]);
                }

                refreshStarted.TrySetResult(true);

                return refreshCompletion.Task;
            });

        documentRepository
            .Setup(x => x.GetByIdAsync(
                documentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var workspaceRepository =
            new Mock<IWorkspaceRepository>();

        workspaceRepository
            .Setup(x => x.UpdateAsync(
                It.IsAny<Workspace>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var activeWorkspaceRegistry =
            new Mock<IActiveWorkspaceRegistry>();

        activeWorkspaceRegistry
            .Setup(x => x.Get(workspaceId))
            .Returns(workspace);

        var dialogService =
            new Mock<IWorkspaceDialogService>();

        dialogService
            .Setup(x => x.ShowDocumentPicker(
                It.IsAny<IReadOnlyList<Document>>(),
                It.IsAny<IEnumerable<Guid>>()))
            .Returns(
                new WorkspaceDocumentSelectionResult([documentId]));

        var documentPresentationFactory =
            new Mock<IWorkspaceDocumentPresentationFactory>();

        WorkspacePresentationViewHarness view =
            CreateViewHarness();

        var logger =
            new RecordingLogger<WorkspacePresentation>();

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
                documentPresentationFactory,
                logger,
                documentRepository);

        int updatedEventCount = 0;

        presentation.WorkspaceDocumentsUpdated +=
            (_, _) => updatedEventCount++;

        view.View.Raise(
            x => x.AddDocumentsRequested += null,
            EventArgs.Empty);

        await refreshStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        presentation.Dispose();

        refreshCompletion.TrySetResult([document]);

        await Task.Delay(
            100,
            TestContext.Current.CancellationToken);

        Assert.Empty(presentation.WorkspaceDocumentIds);
        Assert.Empty(presentation.PresentedDocumentIds);
        Assert.Equal(0, updatedEventCount);

        documentRepository.Verify(
            x => x.GetAllAsync(
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));

        documentPresentationFactory.Verify(
            x => x.Create(
                workspaceId,
                documentId, 0),
            Times.Never);

        view.View.Verify(
            x => x.SetWorkspaceDocuments(
                It.IsAny<IReadOnlyDictionary<Guid, string>>()),
            Times.Never);

        Assert.DoesNotContain(
            logger.Entries,
            entry => entry.Level == LogLevel.Error);
    }

    [Fact]
    public async Task RemoveDocumentsRequested_WhenDisposedDuringWorkspaceRefresh_DoesNotRaiseUpdatedEvent()
    {
        Guid workspaceId = Guid.NewGuid();
        Guid documentId = Guid.NewGuid();

        Document document = Document.Create(
            documentId,
            "document.csv",
            "Document",
            "test-sha256",
            $"Data\\{documentId}\\document.csv");

        Workspace workspace = Workspace.CreatePersistent(
            workspaceId,
            "Workspace");

        workspace.AddDocument(documentId);

        var refreshStarted =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var refreshCompletion =
            new TaskCompletionSource<IReadOnlyList<Document>>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        int getAllCallCount = 0;

        var documentRepository =
            new Mock<IDocumentRepository>();

        documentRepository
            .Setup(x => x.GetAllAsync(
                It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                getAllCallCount++;

                if (getAllCallCount <= 2)
                {
                    return Task.FromResult<IReadOnlyList<Document>>(
                        [document]);
                }

                refreshStarted.TrySetResult(true);

                return refreshCompletion.Task;
            });

        var workspaceRepository =
            new Mock<IWorkspaceRepository>();

        workspaceRepository
            .Setup(x => x.UpdateAsync(
                It.IsAny<Workspace>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var activeWorkspaceRegistry =
            new Mock<IActiveWorkspaceRegistry>();

        activeWorkspaceRegistry
            .Setup(x => x.Get(workspaceId))
            .Returns(workspace);

        var dialogService =
            new Mock<IWorkspaceDialogService>();

        dialogService
            .Setup(x => x.ShowDocumentRemoval(
                It.IsAny<IReadOnlyList<Document>>()))
            .Returns(
                new WorkspaceDocumentSelectionResult([documentId]));

        WorkspacePresentationViewHarness view =
            CreateViewHarness();

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
                documentRepository: documentRepository);

        await presentation.SetWorkspaceDocumentsAsync(
            [documentId],
            TestContext.Current.CancellationToken);

        int updatedEventCount = 0;

        presentation.WorkspaceDocumentsUpdated +=
            (_, _) => updatedEventCount++;

        view.View.Raise(
            x => x.RemoveDocumentsRequested += null,
            EventArgs.Empty);

        await refreshStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        presentation.Dispose();

        refreshCompletion.TrySetResult([document]);

        await Task.Delay(
            100,
            TestContext.Current.CancellationToken);

        Assert.Empty(presentation.WorkspaceDocumentIds);
        Assert.Empty(presentation.PresentedDocumentIds);
        Assert.Null(presentation.ActiveDocumentId);
        Assert.Equal(0, updatedEventCount);

        view.View.Verify(
            x => x.SetWorkspaceDocuments(
                It.IsAny<IReadOnlyDictionary<Guid, string>>()),
            Times.Once);

        view.View.Verify(
            x => x.ClearPresentation(),
            Times.Once);
    }

    [Fact]
    public void OpenDocument_WhenSessionIsLocked_RejectsPresentationCreation()
    {
        Guid workspaceId = Guid.NewGuid();
        Guid documentId = Guid.NewGuid();

        var securitySession =
            new Mock<IApplicationSecuritySession>();

        securitySession
            .SetupGet(x => x.CurrentSnapshot)
            .Returns(
                new ApplicationSecuritySessionSnapshot(
                    ApplicationSecuritySessionState.Locked,
                    1));

        var view = CreateViewHarness();

        var documentPresentationFactory =
            new Mock<IWorkspaceDocumentPresentationFactory>();

        documentPresentationFactory
        .Setup(x => x.Create(
            workspaceId,
            documentId, 0))
        .Returns(
            CreateDocumentPresentation(
                workspaceId,
                documentId));

        WorkspacePresentation presentation =
            CreatePresentation(
                WorkspaceType.Persistent,
                "Workspace",
                null,
                view,
                workspaceId: workspaceId,
                documentPresentationFactory: documentPresentationFactory,
                applicationSecuritySession: securitySession.Object);

        Assert.Throws<OperationCanceledException>(
            () => presentation.OpenDocument(documentId));

        Assert.Empty(presentation.PresentedDocumentIds);

        documentPresentationFactory.Verify(
            x => x.Create(workspaceId, documentId, 0),
            Times.Never);

        view.View.Verify(
            x => x.AddDocumentPresentation(
                It.IsAny<WorkspaceDocumentPresentation>()),
            Times.Never);
    }

    [Fact]
    public void ActivateDocument_WhenSessionIsLocked_DoesNotActivateDocument()
    {
        Guid workspaceId = Guid.NewGuid();
        Guid documentId = Guid.NewGuid();

        var securitySession =
            new Mock<IApplicationSecuritySession>();

        ApplicationSecuritySessionSnapshot currentSnapshot =
            new(
                ApplicationSecuritySessionState.Authenticated,
                0);

        securitySession
            .SetupGet(x => x.CurrentSnapshot)
            .Returns(() => currentSnapshot);

        var view = CreateViewHarness();

        var documentPresentationFactory =
            new Mock<IWorkspaceDocumentPresentationFactory>();

        documentPresentationFactory
            .Setup(x => x.Create(workspaceId, documentId, 0))
            .Returns(
                CreateDocumentPresentation(workspaceId, documentId));

        WorkspacePresentation presentation =
            CreatePresentation(
                WorkspaceType.Persistent,
                "Workspace",
                null,
                view,
                workspaceId: workspaceId,
                documentPresentationFactory: documentPresentationFactory,
                applicationSecuritySession: securitySession.Object);

        // Arrange: establish an existing document presentation while authenticated.
        presentation.OpenDocument(documentId);

        view.View.Invocations.Clear();

        // Act: lock the session.
        currentSnapshot =
            new ApplicationSecuritySessionSnapshot(
                ApplicationSecuritySessionState.Locked,
                1);

        bool activated =
            presentation.ActivateDocument(documentId);

        // Assert.
        Assert.False(activated);

        view.View.Verify(
            x => x.ActivateDocumentPresentation(documentId),
            Times.Never);
    }

    [Fact]
    public void Activate_WhenSessionIsLocked_DoesNotActivateWorkspaceOrDocument()
    {
        Guid workspaceId = Guid.NewGuid();
        Guid documentId = Guid.NewGuid();

        var securitySession =
            new Mock<IApplicationSecuritySession>();

        ApplicationSecuritySessionSnapshot currentSnapshot =
            new(
                ApplicationSecuritySessionState.Authenticated,
                0);

        securitySession
            .SetupGet(x => x.CurrentSnapshot)
            .Returns(() => currentSnapshot);

        var view = CreateViewHarness();

        var documentPresentationFactory =
            new Mock<IWorkspaceDocumentPresentationFactory>();

        documentPresentationFactory
            .Setup(x => x.Create(workspaceId, documentId, 0))
            .Returns(
                CreateDocumentPresentation(workspaceId, documentId));

        WorkspacePresentation presentation =
            CreatePresentation(
                WorkspaceType.Persistent,
                "Workspace",
                null,
                view,
                workspaceId: workspaceId,
                documentPresentationFactory: documentPresentationFactory,
                applicationSecuritySession: securitySession.Object);

        // Arrange: create an active document while authenticated.
        presentation.OpenDocument(documentId);

        view.View.Invocations.Clear();

        // Act: lock the session.
        currentSnapshot =
            new ApplicationSecuritySessionSnapshot(
                ApplicationSecuritySessionState.Locked,
                1);

        bool activated = presentation.Activate();

        // Assert.
        Assert.False(activated);

        view.View.Verify(
            x => x.ActivateWorkspace(),
            Times.Never);

        view.View.Verify(
            x => x.ActivateDocumentPresentation(documentId),
            Times.Never);
    }

    [Fact]
    public void SetNavigationMode_WhenSessionIsLocked_DoesNotChangeNavigationMode()
    {
        var securitySession =
            new Mock<IApplicationSecuritySession>();

        securitySession
            .SetupGet(x => x.CurrentSnapshot)
            .Returns(
                new ApplicationSecuritySessionSnapshot(
                    ApplicationSecuritySessionState.Locked,
                    1));

        WorkspacePresentationViewHarness view =
            CreateViewHarness();

        WorkspacePresentation presentation =
            CreatePresentation(
                WorkspaceType.Persistent,
                "Workspace",
                null,
                view,
                applicationSecuritySession: securitySession.Object);

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
    public void DocumentActivated_WhenSessionIsLocked_DoesNotChangeActiveDocument()
    {
        Guid workspaceId = Guid.NewGuid();
        Guid firstDocumentId = Guid.NewGuid();
        Guid secondDocumentId = Guid.NewGuid();

        WorkspacePresentationViewHarness view =
            CreateViewHarness();

        WorkspaceDocumentPresentation firstPresentation =
            CreateDocumentPresentation(
                workspaceId,
                firstDocumentId);

        WorkspaceDocumentPresentation secondPresentation =
            CreateDocumentPresentation(
                workspaceId,
                secondDocumentId);

        var documentPresentationFactory =
            new Mock<IWorkspaceDocumentPresentationFactory>();

        documentPresentationFactory
            .Setup(x => x.Create(
                workspaceId,
                firstDocumentId, 0))
            .Returns(firstPresentation);

        documentPresentationFactory
            .Setup(x => x.Create(
                workspaceId,
                secondDocumentId, 0))
            .Returns(secondPresentation);

        var securitySession =
            new Mock<IApplicationSecuritySession>();

        ApplicationSecuritySessionSnapshot currentSnapshot =
            new(
                ApplicationSecuritySessionState.Authenticated,
                0);

        securitySession
            .SetupGet(x => x.CurrentSnapshot)
            .Returns(() => currentSnapshot);

        WorkspacePresentation presentation =
            CreatePresentation(
                WorkspaceType.Persistent,
                "Workspace",
                null,
                view,
                documentPresentationFactory: documentPresentationFactory,
                workspaceId: workspaceId,
                applicationSecuritySession: securitySession.Object);

        presentation.OpenDocument(firstDocumentId);
        presentation.OpenDocument(secondDocumentId);

        Assert.Equal(
            secondDocumentId,
            presentation.ActiveDocumentId);

        currentSnapshot =
            new ApplicationSecuritySessionSnapshot(
                ApplicationSecuritySessionState.Locked,
                1);

        securitySession.Raise(
            x => x.StateChanged += null,
            currentSnapshot);

        view.View.Raise(
            x => x.DocumentActivated += null,
            new DocumentActivatedEventArgs(firstDocumentId));

        Assert.Equal(
            secondDocumentId,
            presentation.ActiveDocumentId);

        presentation.Dispose();
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
        Mock<IWorkspaceDocumentPresentationFactory>? documentPresentationFactory = null,
        ILogger<WorkspacePresentation>? logger = null,
        Mock<IDocumentRepository>? documentRepository = null,
        IApplicationSecuritySession? applicationSecuritySession = null,
        long securitySessionRevision = 0)
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

        bool ownsDocumentRepository =
            documentRepository is null;

        Mock<IDocumentRepository> repository =
            documentRepository ?? new Mock<IDocumentRepository>();

        if (ownsDocumentRepository)
        {
            repository
                .Setup(x => x.GetAllAsync(
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);
        }

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

        var workspaceSecuritySession =
            applicationSecuritySession ??
            CreateAuthenticatedApplicationSecuritySession(
                securitySessionRevision);

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
            logger ?? NullLogger<WorkspacePresentation>.Instance,
            workspaceSecuritySession,
            securitySessionRevision);
    }

    private static WorkspaceDocumentPresentation CreateDocumentPresentation(
        Guid workspaceId,
        Guid documentId,
        Exception? openException = null,
        Mock<IDocumentWorkspaceView>? documentView = null,
        Mock<IDocumentRepository>? documentRepository = null,
        Mock<IDocumentReader>? documentReader = null,
        long securitySessionRevision = 0)
    {
        documentView ??= new Mock<IDocumentWorkspaceView>();
        documentRepository ??= new Mock<IDocumentRepository>();
        documentReader ??= new Mock<IDocumentReader>();

        if (openException is not null)
        {
            documentRepository
                .Setup(x => x.GetByIdAsync(
                    documentId,
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(openException);
        }

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
                new DocumentProcessingOptions(),
                NullLogger<OpenDocumentHandler>.Instance);

        var removeDocumentHandler =
            new RemoveDocumentHandler(
                documentRepository.Object,
                workspaceRepository.Object,
                storageService.Object,
                NullLogger<RemoveDocumentHandler>.Instance);

        var applicationSecuritySession =
            new Mock<IApplicationSecuritySession>();

        applicationSecuritySession
            .Setup(x => x.CurrentSnapshot)
            .Returns(
                new ApplicationSecuritySessionSnapshot(
                    ApplicationSecuritySessionState.Authenticated,
                    securitySessionRevision));

        var presenter =
            new DocumentWorkspacePresenter(
                workspaceId,
                documentView.Object,
                documentViewer.Object,
                getDocumentHandler,
                openDocumentHandler,
                removeDocumentHandler,
                NullLogger<DocumentWorkspacePresenter>.Instance,
                applicationSecuritySession.Object,
                securitySessionRevision);

        return new WorkspaceDocumentPresentation(
            documentId,
            presenter,
            documentView.Object);
    }

    private static IApplicationSecuritySession CreateAuthenticatedApplicationSecuritySession(
        long securitySessionRevision = 0)
    {
        var applicationSecuritySession =
            new Mock<IApplicationSecuritySession>();

        applicationSecuritySession
            .SetupGet(x => x.CurrentSnapshot)
            .Returns(
                new ApplicationSecuritySessionSnapshot(
                    ApplicationSecuritySessionState.Authenticated,
                    securitySessionRevision));

        return applicationSecuritySession.Object;
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

    private sealed class RecordingLogger<TCategory> : ILogger<TCategory>
    {
        private readonly List<RecordedLogEntry> _entries = [];

        public IReadOnlyList<RecordedLogEntry> Entries => _entries;

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
                NoopDisposable.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            string message =
                formatter(state, exception);

            Dictionary<string, object?> properties = [];

            if (state is IEnumerable<KeyValuePair<string, object?>> structuredState)
            {
                foreach (KeyValuePair<string, object?> property in structuredState)
                {
                    properties[property.Key] = property.Value;
                }
            }

            _entries.Add(
                new RecordedLogEntry(
                    logLevel,
                    message,
                    exception,
                    properties));
        }
    }

    private sealed record RecordedLogEntry(
        LogLevel Level,
        string Message,
        Exception? Exception,
        IReadOnlyDictionary<string, object?> Properties);

    private sealed class NoopDisposable : IDisposable
    {
        public static NoopDisposable Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
