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
using DeskVault.Infrastructure.Services;
using DeskVault.UI.Hosting;
using DeskVault.UI.Presenters;
using DeskVault.UI.Services.Interfaces;
using DeskVault.UI.Services.Workspace;
using DeskVault.UI.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Security.Cryptography;
using System.Text;

namespace DeskVault.UI.Tests;

public sealed class ProductionSecuritySessionPresentationIntegrationTests
{
    [Fact]
    public async Task DocumentPresentation_WhenSessionLocksDuringPendingRender_ClearsRetainedContentAndRejectsLateResultAfterReauthentication()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        await using ProductionSecuritySessionTestHost testHost =
            ProductionSecuritySessionTestHost.Create();

        await testHost.StartAsync(cancellationToken);

        IServiceProvider services =
            testHost.Services;

        DeskVaultStartupCoordinator startupCoordinator =
            services.GetRequiredService<DeskVaultStartupCoordinator>();

        IApplicationSecuritySession securitySession =
            services.GetRequiredService<IApplicationSecuritySession>();

        IApplicationSecuritySessionLifecycleService lifecycleService =
            services.GetRequiredService<
                IApplicationSecuritySessionLifecycleService>();

        IApplicationAuthenticationService authenticationService =
            services.GetRequiredService<
                IApplicationAuthenticationService>();

        IEncryptionKeyService documentKeyService =
            services.GetRequiredService<IEncryptionKeyService>();

        IDatabaseEncryptionKeyService databaseKeyService =
            services.GetRequiredService<IDatabaseEncryptionKeyService>();

        // Establish an authenticated session through the real production
        // workstation readiness and Windows Hello authentication path.
        bool authenticated =
            await startupCoordinator.AuthenticateAsync(
                cancellationToken);

        Assert.True(authenticated);

        ApplicationSecuritySessionSnapshot owningSnapshot =
            securitySession.CurrentSnapshot;

        Assert.Equal(
            ApplicationSecuritySessionState.Authenticated,
            owningSnapshot.State);

        Assert.True(
            owningSnapshot.IsProtectedAccessAvailable);

        // Initialize a real encrypted vault so the presentation test can
        // verify the same production key-enforcement boundary.
        await startupCoordinator.InitializeDatabaseAsync(
            cancellationToken);

        Assert.True(
            File.Exists(testHost.DataPaths.DatabasePath));

        // Arrange a real DocumentWorkspacePresenter with a controlled
        // document metadata repository and a controlled UI dispatcher.
        Guid documentId =
            Guid.NewGuid();

        Document document =
            CreateDocument(documentId);

        var documentRepository =
            CreateDocumentRepository(document);

        var view =
            new Mock<IDocumentWorkspaceView>();

        var queuedUiActions =
            new List<Action>();

        view
            .Setup(x => x.PostToUi(
                It.IsAny<Action>()))
            .Callback<Action>(
                queuedUiActions.Add);

        var presentationStarted =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var pendingPresentation =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        view
            .Setup(x => x.ShowDocumentAsync(
                It.IsAny<Stream>(),
                document.FileName,
                It.IsAny<CancellationToken>()))
            .Callback<Stream, string, CancellationToken>(
                (_, _, _) =>
                    presentationStarted.TrySetResult(true))
            .Returns(
                () => pendingPresentation.Task);

        using var presenter =
            CreateDocumentWorkspacePresenter(
                securitySession,
                view.Object,
                documentRepository,
                owningSnapshot.Revision);

        var retainedStream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(
                    "Id,Name\r\n1,Protected content\r\n"));

        // Act: start a document presentation operation. The view deliberately
        // remains pending and does not cooperate with cancellation.
        Task openTask =
            presenter.OpenAsync(
                documentId,
                retainedStream,
                document.FileName,
                cancellationToken);

        await presentationStarted.Task.WaitAsync(
            cancellationToken);

        // Assert: the presenter retained the document under the owning
        // authenticated session revision before rendering completed.
        Assert.Equal(
            documentId,
            presenter.CurrentDocumentId);

        Assert.Equal(
            document.FileName,
            presenter.CurrentDocumentFileName);

        Assert.True(retainedStream.CanRead);

        view.Verify(
            x => x.ShowDocumentAsync(
                It.IsAny<Stream>(),
                document.FileName,
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Act: lock using the authoritative production lifecycle service.
        lifecycleService.Lock();

        ApplicationSecuritySessionSnapshot lockedSnapshot =
            securitySession.CurrentSnapshot;

        // Assert: the lock revokes authorization immediately.
        Assert.Equal(
            ApplicationSecuritySessionState.Locked,
            lockedSnapshot.State);

        Assert.False(
            lockedSnapshot.IsProtectedAccessAvailable);

        Assert.True(
            lockedSnapshot.Revision > owningSnapshot.Revision);

        // The actual registered document and database key adapters
        // must deny access under the same locked session.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => documentKeyService.GetKeyAsync(
                cancellationToken));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => databaseKeyService.GetKeyAsync(
                cancellationToken));

        // #320 dispatches presentation cleanup to the owning UI thread.
        Assert.Single(queuedUiActions);

        view.Verify(
            x => x.ClearPresentation(),
            Times.Never);

        // Apply the UI-dispatched revocation cleanup.
        queuedUiActions[0]();

        view.Verify(
            x => x.ClearPresentation(),
            Times.Once);

        Assert.False(retainedStream.CanRead);

        // Act: establish a newer authenticated revision before the
        // previous rendering operation returns its delayed result.
        bool reauthenticated =
            await startupCoordinator.AuthenticateAsync(
                cancellationToken);

        Assert.True(reauthenticated);

        ApplicationSecuritySessionSnapshot reauthenticatedSnapshot =
            securitySession.CurrentSnapshot;

        Assert.Equal(
            ApplicationSecuritySessionState.Authenticated,
            reauthenticatedSnapshot.State);

        Assert.True(
            reauthenticatedSnapshot.IsProtectedAccessAvailable);

        Assert.True(
            reauthenticatedSnapshot.Revision > lockedSnapshot.Revision);

        // The production document key path is available again, but the
        // existing presenter remains permanently bound to the older revision.
        EncryptionKeyMaterial restoredDocumentKey =
            await documentKeyService.GetKeyAsync(
                cancellationToken);

        CryptographicOperations.ZeroMemory(
            restoredDocumentKey.KeyBytes);

        // Complete the non-cooperative view operation after reauthentication.
        // Its old result must not be presented under the newer revision.
        Assert.True(
            pendingPresentation.TrySetResult(true));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => openTask);

        // Assert: late completion did not restore document metadata,
        // retain the old stream, or issue a second presentation operation.
        Assert.Null(presenter.CurrentDocumentId);
        Assert.Null(presenter.CurrentDocumentDisplayName);
        Assert.Null(presenter.CurrentDocumentFileName);

        Assert.False(retainedStream.CanRead);

        view.Verify(
            x => x.ShowDocumentAsync(
                It.IsAny<Stream>(),
                document.FileName,
                It.IsAny<CancellationToken>()),
            Times.Once);

        view.Verify(
            x => x.ClearPresentation(),
            Times.Once);

        view.Verify(
            x => x.ShowUnsupportedPreview(
                It.IsAny<string>()),
            Times.Never);

        view.Verify(
            x => x.ShowError(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<long>()),
            Times.Never);

        // The new session is still authenticated, but the old presenter
        // cannot activate or expose its previous document.
        presenter.Activate();

        view.Verify(
            x => x.ActivateWorkspace(),
            Times.Never);
    }

    [Fact]
    public async Task WorkspacePresentation_WhenOwningSessionIsRevoked_DisposesWorkspaceAndChildPresentationOnTheirUiDispatchers()
    {
        // Arrange
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        await using ProductionSecuritySessionTestHost testHost =
            ProductionSecuritySessionTestHost.Create();

        await testHost.StartAsync(cancellationToken);

        IServiceProvider services =
            testHost.Services;

        DeskVaultStartupCoordinator startupCoordinator =
            services.GetRequiredService<DeskVaultStartupCoordinator>();

        IApplicationSecuritySession securitySession =
            services.GetRequiredService<IApplicationSecuritySession>();

        IApplicationSecuritySessionLifecycleService lifecycleService =
            services.GetRequiredService<
                IApplicationSecuritySessionLifecycleService>();

        bool authenticated =
            await startupCoordinator.AuthenticateAsync(
                cancellationToken);

        Assert.True(authenticated);

        ApplicationSecuritySessionSnapshot owningSnapshot =
            securitySession.CurrentSnapshot;

        Guid workspaceId =
            Guid.NewGuid();

        Guid documentId =
            Guid.NewGuid();

        Document document =
            CreateDocument(documentId);

        var documentRepository =
            CreateDocumentRepository(document);

        // Create a real child DocumentWorkspacePresenter bound to the
        // same production session and revision as the workspace.
        var childView =
            new Mock<IDocumentWorkspaceView>();

        var queuedChildUiActions =
            new List<Action>();

        childView
            .Setup(x => x.PostToUi(
                It.IsAny<Action>()))
            .Callback<Action>(
                queuedChildUiActions.Add);

        childView
            .Setup(x => x.ShowDocumentAsync(
                It.IsAny<Stream>(),
                document.FileName,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        using DocumentWorkspacePresenter childPresenter =
            CreateDocumentWorkspacePresenter(
                securitySession,
                childView.Object,
                documentRepository,
                owningSnapshot.Revision);

        var childPresentation =
            new WorkspaceDocumentPresentation(
                documentId,
                childPresenter,
                childView.Object);

        var documentPresentationFactory =
            new Mock<IWorkspaceDocumentPresentationFactory>(
                MockBehavior.Strict);

        documentPresentationFactory
            .Setup(x => x.Create(
                workspaceId,
                documentId,
                owningSnapshot.Revision))
            .Returns(childPresentation);

        // The workspace itself also uses a UI-dispatched view.
        var workspaceView =
            new Mock<IWorkspacePresentationView>();

        var queuedWorkspaceUiActions =
            new List<Action>();

        workspaceView
            .Setup(x => x.PostToUi(
                It.IsAny<Action>()))
            .Callback<Action>(
                queuedWorkspaceUiActions.Add);

        var workspaceDialogService =
            new Mock<IWorkspaceDialogService>();

        // Use the actual production Application handlers registered
        // in the host rather than constructing another test-only graph.
        var workspacePresentation =
            new WorkspacePresentation(
                workspaceId,
                WorkspaceType.Persistent,
                "Protected Workspace",
                "Sensitive workspace description",
                documentPresentationFactory.Object,
                services.GetRequiredService<ListDocumentsHandler>(),
                services.GetRequiredService<AddDocumentToWorkspaceHandler>(),
                services.GetRequiredService<RemoveDocumentFromWorkspaceHandler>(),
                services.GetRequiredService<RenameWorkspaceHandler>(),
                services.GetRequiredService<SaveTemporaryWorkspaceHandler>(),
                services.GetRequiredService<UpdateWorkspaceDescriptionHandler>(),
                workspaceDialogService.Object,
                workspaceView.Object,
                NullLogger<WorkspacePresentation>.Instance,
                securitySession,
                owningSnapshot.Revision);

        // Create the child through the actual WorkspacePresentation
        // ownership boundary.
        WorkspaceDocumentPresentation openedPresentation =
            workspacePresentation.OpenDocument(documentId);

        Assert.Same(
            childPresentation,
            openedPresentation);

        Assert.Contains(
            documentId,
            workspacePresentation.PresentedDocumentIds);

        // Retain actual document content in the child presenter.
        var retainedStream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(
                    "Id,Name\r\n1,Workspace protected content\r\n"));

        await childPresenter.OpenAsync(
            documentId,
            retainedStream,
            document.FileName,
            cancellationToken);

        Assert.Equal(
            documentId,
            childPresenter.CurrentDocumentId);

        Assert.True(retainedStream.CanRead);

        // Act: use the production lock lifecycle.
        lifecycleService.Lock();

        ApplicationSecuritySessionSnapshot lockedSnapshot =
            securitySession.CurrentSnapshot;

        // Assert: workspace and child presenter independently schedule
        // their cleanup on their respective owning UI dispatchers.
        Assert.Equal(
            ApplicationSecuritySessionState.Locked,
            lockedSnapshot.State);

        Assert.Equal(
            owningSnapshot.Revision + 1,
            lockedSnapshot.Revision);

        Assert.Single(queuedWorkspaceUiActions);
        Assert.Single(queuedChildUiActions);

        Assert.Equal(
            "Protected Workspace",
            workspacePresentation.WorkspaceName);

        Assert.True(retainedStream.CanRead);

        workspaceView.Verify(
            x => x.ClearPresentation(),
            Times.Never);

        childView.Verify(
            x => x.ClearPresentation(),
            Times.Never);

        // Act: dispatch parent workspace cleanup first. Its disposal
        // owns and disposes the child presentation as well.
        queuedWorkspaceUiActions[0]();

        // The child also had a cleanup callback queued independently.
        // It must safely observe that the parent already disposed it.
        queuedChildUiActions[0]();

        // Assert: all retained workspace metadata and document ownership
        // have been removed.
        Assert.Null(workspacePresentation.WorkspaceName);
        Assert.Null(workspacePresentation.WorkspaceDescription);
        Assert.Null(workspacePresentation.ActiveDocumentId);

        Assert.Empty(
            workspacePresentation.WorkspaceDocumentIds);

        Assert.Empty(
            workspacePresentation.PresentedDocumentIds);

        Assert.False(retainedStream.CanRead);

        workspaceView.Verify(
            x => x.ClearPresentation(),
            Times.Once);

        workspaceView.Verify(
            x => x.Dispose(),
            Times.Once);

        childView.Verify(
            x => x.ClearPresentation(),
            Times.Once);

        // Disposing an already revoked workspace again must not repeat
        // view cleanup or child disposal.
        workspacePresentation.Dispose();

        workspaceView.Verify(
            x => x.ClearPresentation(),
            Times.Once);

        workspaceView.Verify(
            x => x.Dispose(),
            Times.Once);

        childView.Verify(
            x => x.ClearPresentation(),
            Times.Once);
    }

    private static Document CreateDocument(
        Guid documentId)
    {
        return Document.Create(
            documentId,
            "protected.csv",
            "Protected document",
            "test-sha256",
            $"Data\\{documentId}\\protected.csv");
    }

    private static Mock<IDocumentRepository> CreateDocumentRepository(
        Document document)
    {
        var repository =
            new Mock<IDocumentRepository>(MockBehavior.Strict);

        repository
            .Setup(x => x.GetByIdAsync(
                document.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        return repository;
    }

    private static DocumentWorkspacePresenter CreateDocumentWorkspacePresenter(
        IApplicationSecuritySession securitySession,
        IDocumentWorkspaceView view,
        Mock<IDocumentRepository> documentRepository,
        long securitySessionRevision)
    {
        var workspaceRepository =
            new Mock<IWorkspaceRepository>();

        var storageService =
            new Mock<IStorageService>();

        var documentReader =
            new Mock<IDocumentReader>();

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

        return new DocumentWorkspacePresenter(
            Guid.NewGuid(),
            view,
            new Mock<IDocumentViewer>().Object,
            getDocumentHandler,
            openDocumentHandler,
            removeDocumentHandler,
            NullLogger<DocumentWorkspacePresenter>.Instance,
            securitySession,
            securitySessionRevision);
    }
}
