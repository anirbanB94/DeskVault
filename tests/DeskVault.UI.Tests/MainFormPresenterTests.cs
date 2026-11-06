using DeskVault.Application.Configurations;
using DeskVault.Application.Documents;
using DeskVault.Application.Documents.Commands.ImportDocument;
using DeskVault.Application.Documents.Commands.RemoveDocument;
using DeskVault.Application.Documents.Extraction;
using DeskVault.Application.Documents.Queries.GetDocument;
using DeskVault.Application.Documents.Queries.ListDocuments;
using DeskVault.Application.Documents.Queries.OpenDocument;
using DeskVault.Application.Documents.Queries.SearchDocuments;
using DeskVault.Application.Interfaces;
using DeskVault.Application.Security.Sessions;
using DeskVault.Application.Workspaces.Commands.AddDocumentToWorkspace;
using DeskVault.Application.Workspaces.Commands.CloseWorkspace;
using DeskVault.Application.Workspaces.Commands.CreateWorkspace;
using DeskVault.Application.Workspaces.Commands.DeleteWorkspace;
using DeskVault.Application.Workspaces.Commands.OpenWorkspace;
using DeskVault.Application.Workspaces.Commands.RemoveDocumentFromWorkspace;
using DeskVault.Application.Workspaces.Commands.RenameWorkspace;
using DeskVault.Application.Workspaces.Commands.SaveTemporaryWorkspace;
using DeskVault.Application.Workspaces.Commands.UpdateWorkspaceDescription;
using DeskVault.Application.Workspaces.Queries.GetWorkspaces;
using DeskVault.Domain.Documents;
using DeskVault.Domain.Workspaces;
using DeskVault.UI.Presenters;
using DeskVault.UI.Resources;
using DeskVault.UI.Services.Interfaces;
using DeskVault.UI.Services.Workspace;
using DeskVault.UI.Tests.TestDoubles;
using DeskVault.UI.Views;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Reflection;
using System.Text;
using DiagnosticOperationCodes = DeskVault.Shared.Resources.DiagnosticOperationCodes;

namespace DeskVault.UI.Tests;

public sealed class MainFormPresenterTests
{
    [Fact]
    public async Task SecuritySessionLocked_CancelsPendingSearchAndClearsMainPresentation()
    {
        var searchStore =
            new Mock<IDocumentSearchStore>();

        var searchStarted =
            new TaskCompletionSource<CancellationToken>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var searchCompletion =
            new TaskCompletionSource<IReadOnlyList<SearchDocumentsResult>>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        searchStore
            .Setup(x => x.SearchAsync(
                It.IsAny<SearchDocumentsQuery>(),
                It.IsAny<SearchDocumentsRankingKey?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Returns((
                SearchDocumentsQuery _,
                SearchDocumentsRankingKey? _,
                int _,
                CancellationToken cancellationToken) =>
            {
                searchStarted.TrySetResult(cancellationToken);

                return searchCompletion.Task;
            });

        var view =
            new Mock<IMainFormView>();

        view
            .SetupGet(x => x.SearchText)
            .Returns("security");

        var securitySession =
            new Mock<IApplicationSecuritySession>();

        ApplicationSecuritySessionSnapshot currentSnapshot =
            new(
                ApplicationSecuritySessionState.Authenticated,
                0);

        securitySession
            .SetupGet(x => x.CurrentSnapshot)
            .Returns(() => currentSnapshot);

        var workspacePresentationManager =
            new Mock<IWorkspacePresentationManager>();

        using MainFormPresenter presenter =
            CreatePresenter(
                view,
                searchStore,
                workspacePresentationManager: workspacePresentationManager,
                applicationSecuritySession: securitySession);

        // Arrange: start a search that has not returned yet.
        view.Raise(
            x => x.SearchRequested += null,
            EventArgs.Empty);

        CancellationToken searchToken =
            await searchStarted.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);

        // Act: the authoritative session transitions to Locked.
        currentSnapshot =
            new ApplicationSecuritySessionSnapshot(
                ApplicationSecuritySessionState.Locked,
                1);

        securitySession.Raise(
            x => x.StateChanged += null,
            currentSnapshot);

        // Assert: pending search work is invalidated and the main view is cleared.
        Assert.True(searchToken.IsCancellationRequested);

        searchCompletion.SetResult(
            CreateSearchResults(
                Guid.NewGuid(),
                Guid.NewGuid()));

        await WaitForBackgroundOperationAsync();

        view.Verify(
            x => x.ShowLockedState(),
            Times.Once);

        workspacePresentationManager.Verify(
            x => x.CloseAll(),
            Times.Once);

        view.Verify(
            x => x.ShowSearchResults(
                It.IsAny<IReadOnlyList<SearchResultListItem>>()),
            Times.Never);

        view.Verify(
            x => x.AppendSearchResults(
                It.IsAny<IReadOnlyList<SearchResultListItem>>()),
            Times.Never);
    }

    [Fact]
    public async Task SearchCompletion_BeforeQueuedLockNotificationIsApplied_DoesNotRestoreProtectedResults()
    {
        var searchStore =
            new Mock<IDocumentSearchStore>();

        var searchStarted =
            new TaskCompletionSource<CancellationToken>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var searchCompletion =
            new TaskCompletionSource<IReadOnlyList<SearchDocumentsResult>>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        searchStore
            .Setup(x => x.SearchAsync(
                It.IsAny<SearchDocumentsQuery>(),
                It.IsAny<SearchDocumentsRankingKey?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Returns((
                SearchDocumentsQuery _,
                SearchDocumentsRankingKey? _,
                int _,
                CancellationToken cancellationToken) =>
            {
                searchStarted.TrySetResult(cancellationToken);

                return searchCompletion.Task;
            });

        var view =
            new Mock<IMainFormView>();

        view
            .SetupGet(x => x.SearchText)
            .Returns("security");

        var securitySession =
            new Mock<IApplicationSecuritySession>();

        ApplicationSecuritySessionSnapshot currentSnapshot =
            new(
                ApplicationSecuritySessionState.Authenticated,
                0);

        securitySession
            .SetupGet(x => x.CurrentSnapshot)
            .Returns(() => currentSnapshot);

        var workspacePresentationManager =
            new Mock<IWorkspacePresentationManager>();

        var postedActions =
            new List<Action>();

        using MainFormPresenter presenter =
            CreatePresenter(
                view,
                searchStore,
                workspacePresentationManager: workspacePresentationManager,
                applicationSecuritySession: securitySession,
                postToUi: postedActions.Add);

        // Arrange: start a search and keep its result pending.
        view.Raise(
            x => x.SearchRequested += null,
            EventArgs.Empty);

        CancellationToken searchToken =
            await searchStarted.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);

        // Act: lock the authoritative session, but do not dispatch the UI callback.
        currentSnapshot =
            new ApplicationSecuritySessionSnapshot(
                ApplicationSecuritySessionState.Locked,
                1);

        securitySession.Raise(
            x => x.StateChanged += null,
            currentSnapshot);

        Assert.Single(postedActions);

        // Revocation is observed, but UI cleanup has not run yet.
        Assert.True(searchToken.IsCancellationRequested);

        // Complete the search while the lock notification is still queued.
        searchCompletion.SetResult(
            CreateSearchResults(
                Guid.NewGuid(),
                Guid.NewGuid()));

        await WaitForBackgroundOperationAsync();

        // Assert: the late search must not restore protected search results.
        view.Verify(
            x => x.ShowSearchResults(
                It.IsAny<IReadOnlyList<SearchResultListItem>>()),
            Times.Never);

        view.Verify(
            x => x.AppendSearchResults(
                It.IsAny<IReadOnlyList<SearchResultListItem>>()),
            Times.Never);

        // Now apply the queued UI notification.
        Action postedAction =
            Assert.Single(postedActions);

        postedAction();

        view.Verify(
            x => x.ShowLockedState(),
            Times.Once);

        workspacePresentationManager.Verify(
            x => x.CloseAll(),
            Times.Once);
    }

    [Fact]
    public async Task StaleSessionNotification_AppliesLatestSnapshotAndReloadsFreshMetadata()
    {
        Guid documentId =
            Guid.NewGuid();

        Document document =
            Document.Restore(
                documentId,
                "security-policy.md",
                "Security Policy",
                "test-hash",
                "document.dvault",
                DateTime.UtcNow,
                DocumentStatus.Available);

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetAllAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([document]);

        var workspaceRepository =
            new Mock<IWorkspaceRepository>();

        workspaceRepository
            .Setup(x => x.GetAllAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var view =
            new Mock<IMainFormView>();

        var securitySession =
            new Mock<IApplicationSecuritySession>();

        ApplicationSecuritySessionSnapshot currentSnapshot =
            new(
                ApplicationSecuritySessionState.Locked,
                1);

        securitySession
            .SetupGet(x => x.CurrentSnapshot)
            .Returns(() => currentSnapshot);

        var workspacePresentationManager =
            new Mock<IWorkspacePresentationManager>();

        using MainFormPresenter presenter =
            CreatePresenter(
                view,
                new Mock<IDocumentSearchStore>(),
                repository: repository,
                workspaceRepository: workspaceRepository,
                workspacePresentationManager: workspacePresentationManager,
                applicationSecuritySession: securitySession);

        // Queue session notifications so the test controls when the UI applies them.
        var postedActions =
            new List<Action>();

        view
            .Setup(x => x.PostToUi(It.IsAny<Action>()))
            .Callback<Action>(postedActions.Add);

        // Arrange: the presenter starts while the session is locked.
        await presenter.InitializeAsync();

        view.Verify(
            x => x.ShowLockedState(),
            Times.Once);

        // The session has since authenticated at revision 3.
        currentSnapshot =
            new ApplicationSecuritySessionSnapshot(
                ApplicationSecuritySessionState.Authenticated,
                3);

        // Act: a delayed notification for the older Locked revision arrives.
        securitySession.Raise(
            x => x.StateChanged += null,
            new ApplicationSecuritySessionSnapshot(
                ApplicationSecuritySessionState.Locked,
                2));

        Action postedAction =
            Assert.Single(postedActions);

        postedAction();

        await WaitForBackgroundOperationAsync();

        // Assert: the latest authoritative snapshot wins.
        view.Verify(
            x => x.ShowLockedState(),
            Times.Exactly(2));

        view.Verify(
            x => x.ShowAuthenticatedState(),
            Times.Once);

        view.Verify(
            x => x.ShowDocuments(
                It.Is<IReadOnlyList<DocumentListItem>>(
                    items =>
                        items.Count == 1 &&
                        items[0].Id == documentId &&
                        items[0].FileName == "security-policy.md")),
            Times.Once);

        workspacePresentationManager.Verify(
            x => x.CloseAll(),
            Times.Exactly(2));

        view.Verify(
            x => x.ShowWorkspaces(
                It.Is<IReadOnlyList<WorkspaceListItem>>(
                    items => items.Count == 0)),
            Times.Once);

        view.Verify(
            x => x.ShowSearchResults(
                It.IsAny<IReadOnlyList<SearchResultListItem>>()),
            Times.Never);

        view.Verify(
            x => x.AppendSearchResults(
                It.IsAny<IReadOnlyList<SearchResultListItem>>()),
            Times.Never);

        repository.Verify(
            x => x.GetAllAsync(
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));

        workspaceRepository.Verify(
            x => x.GetAllAsync(
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void Dispose_UnsubscribesFromSecuritySessionAndView()
    {
        var view =
            new Mock<IMainFormView>();

        var searchStore =
            new Mock<IDocumentSearchStore>();

        var securitySession =
            new Mock<IApplicationSecuritySession>();

        securitySession
            .SetupGet(x => x.CurrentSnapshot)
            .Returns(
                new ApplicationSecuritySessionSnapshot(
                    ApplicationSecuritySessionState.Authenticated,
                    0));

        var workspacePresentationManager =
            new Mock<IWorkspacePresentationManager>();

        MainFormPresenter presenter =
            CreatePresenter(
                view,
                searchStore,
                workspacePresentationManager: workspacePresentationManager,
                applicationSecuritySession: securitySession);

        // Act: dispose twice to verify idempotence.
        presenter.Dispose();
        presenter.Dispose();

        workspacePresentationManager.Verify(
            x => x.CloseAll(),
            Times.Once);

        securitySession.Raise(
            x => x.StateChanged += null,
            new ApplicationSecuritySessionSnapshot(
                ApplicationSecuritySessionState.Locked,
                1));

        view.Raise(
            x => x.SearchRequested += null,
            EventArgs.Empty);

        // Assert: neither the session nor the view can call back into the presenter.
        view.Verify(
            x => x.PostToUi(It.IsAny<Action>()),
            Times.Never);

        searchStore.Verify(
            x => x.SearchAsync(
                It.IsAny<SearchDocumentsQuery>(),
                It.IsAny<SearchDocumentsRankingKey?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task LockAndReauthenticationBeforeUiDispatch_ClearsOldPresentationAndReloadsFreshMetadata()
    {
        Guid documentId =
            Guid.NewGuid();

        Document document =
            Document.Restore(
                documentId,
                "security-policy.md",
                "Security Policy",
                "test-hash",
                "document.dvault",
                DateTime.UtcNow,
                DocumentStatus.Available);

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetAllAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([document]);

        var workspaceRepository =
            new Mock<IWorkspaceRepository>();

        workspaceRepository
            .Setup(x => x.GetAllAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var view =
            new Mock<IMainFormView>();

        var securitySession =
            new Mock<IApplicationSecuritySession>();

        ApplicationSecuritySessionSnapshot currentSnapshot =
            new(
                ApplicationSecuritySessionState.Authenticated,
                0);

        securitySession
            .SetupGet(x => x.CurrentSnapshot)
            .Returns(() => currentSnapshot);

        var workspacePresentationManager =
            new Mock<IWorkspacePresentationManager>();

        using MainFormPresenter presenter =
            CreatePresenter(
                view,
                new Mock<IDocumentSearchStore>(),
                repository: repository,
                workspaceRepository: workspaceRepository,
                workspacePresentationManager: workspacePresentationManager,
                applicationSecuritySession: securitySession);

        // Arrange: display protected metadata while authenticated.
        await presenter.InitializeAsync();

        view.Verify(
            x => x.ShowDocuments(
                It.Is<IReadOnlyList<DocumentListItem>>(
                    items =>
                        items.Count == 1 &&
                        items[0].Id == documentId &&
                        items[0].FileName == "security-policy.md")),
            Times.Once);

        // Queue UI callbacks so both session transitions occur before dispatch.
        var postedActions =
            new List<Action>();

        view
            .Setup(x => x.PostToUi(It.IsAny<Action>()))
            .Callback<Action>(postedActions.Add);

        // Act: lock and re-authenticate before the UI processes either notification.
        currentSnapshot =
            new ApplicationSecuritySessionSnapshot(
                ApplicationSecuritySessionState.Locked,
                1);

        securitySession.Raise(
            x => x.StateChanged += null,
            currentSnapshot);

        currentSnapshot =
            new ApplicationSecuritySessionSnapshot(
                ApplicationSecuritySessionState.Authenticated,
                2);

        securitySession.Raise(
            x => x.StateChanged += null,
            currentSnapshot);

        Assert.Equal(2, postedActions.Count);

        foreach (Action action in postedActions)
        {
            action();
        }

        await WaitForBackgroundOperationAsync();

        // Assert: old presentation was cleared and metadata was freshly retrieved.
        view.Verify(
            x => x.ShowLockedState(),
            Times.Once);

        view.Verify(
            x => x.ShowAuthenticatedState(),
            Times.Exactly(2));

        view.Verify(
            x => x.ShowDocuments(
                It.Is<IReadOnlyList<DocumentListItem>>(
                    items =>
                        items.Count == 1 &&
                        items[0].Id == documentId &&
                        items[0].FileName == "security-policy.md")),
            Times.Exactly(2));

        workspacePresentationManager.Verify(
            x => x.CloseAll(),
            Times.Once);

        view.Verify(
            x => x.ShowSearchResults(
                It.IsAny<IReadOnlyList<SearchResultListItem>>()),
            Times.Never);

        view.Verify(
            x => x.AppendSearchResults(
                It.IsAny<IReadOnlyList<SearchResultListItem>>()),
            Times.Never);
    }

    [Fact]
    public async Task InitializeAsync_WhenSessionLocksDuringDocumentRefresh_DoesNotShowStaleDocuments()
    {
        Guid documentId = Guid.NewGuid();

        Document document =
            Document.Restore(
                documentId,
                "security-policy.md",
                "Security Policy",
                "test-hash",
                "document.dvault",
                DateTime.UtcNow,
                DocumentStatus.Available);

        var repository =
            new Mock<IDocumentRepository>();

        var refreshStarted =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var refreshCompletion =
            new TaskCompletionSource<IReadOnlyList<Document>>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        repository
            .Setup(x => x.GetAllAsync(
                It.IsAny<CancellationToken>()))
            .Callback<CancellationToken>(
                _ => refreshStarted.TrySetResult())
            .Returns(refreshCompletion.Task);

        var view =
            new Mock<IMainFormView>();

        var searchStore =
            new Mock<IDocumentSearchStore>();

        var securitySession =
            new Mock<IApplicationSecuritySession>();

        ApplicationSecuritySessionSnapshot currentSnapshot =
            new(
                ApplicationSecuritySessionState.Authenticated,
                0);

        securitySession
            .SetupGet(x => x.CurrentSnapshot)
            .Returns(() => currentSnapshot);

        using MainFormPresenter presenter =
            CreatePresenter(
                view,
                searchStore,
                repository: repository,
                applicationSecuritySession: securitySession);

        // Arrange: initialization starts loading protected document metadata.
        Task initializationTask =
            presenter.InitializeAsync();

        await refreshStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        // Act: lock the session before the repository returns.
        currentSnapshot =
            new ApplicationSecuritySessionSnapshot(
                ApplicationSecuritySessionState.Locked,
                1);

        securitySession.Raise(
            x => x.StateChanged += null,
            currentSnapshot);

        // The repository deliberately completes with stale data.
        refreshCompletion.SetResult([document]);

        await initializationTask;

        // Assert: the authenticated-era document metadata must not be presented.
        view.Verify(
            x => x.ShowDocuments(
                It.IsAny<IReadOnlyList<DocumentListItem>>()),
            Times.Never);

        view.Verify(
            x => x.ShowLockedState(),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task InitializeAsync_WhenSessionLocksDuringWorkspaceLookup_DoesNotShowStaleWorkspaces()
    {
        Workspace workspace =
            Workspace.CreatePersistent(
                Guid.NewGuid(),
                "Confidential Workspace",
                "Sensitive workspace description");

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetAllAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var workspaceRepository =
            new Mock<IWorkspaceRepository>();

        var workspaceLookupStarted =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var workspaceLookupCompletion =
            new TaskCompletionSource<IReadOnlyList<Workspace>>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        workspaceRepository
            .Setup(x => x.GetAllAsync(
                It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                workspaceLookupStarted.TrySetResult();

                return workspaceLookupCompletion.Task;
            });

        var view =
            new Mock<IMainFormView>();

        var securitySession =
            new Mock<IApplicationSecuritySession>();

        ApplicationSecuritySessionSnapshot currentSnapshot =
            new(
                ApplicationSecuritySessionState.Authenticated,
                0);

        securitySession
            .SetupGet(x => x.CurrentSnapshot)
            .Returns(() => currentSnapshot);

        var workspacePresentationManager =
            new Mock<IWorkspacePresentationManager>();

        using MainFormPresenter presenter =
            CreatePresenter(
                view,
                new Mock<IDocumentSearchStore>(),
                repository: repository,
                workspaceRepository: workspaceRepository,
                workspacePresentationManager: workspacePresentationManager,
                applicationSecuritySession: securitySession);

        // Arrange: initialize documents and begin workspace retrieval.
        Task initializationTask =
            presenter.InitializeAsync();

        await workspaceLookupStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        // Act: lock while workspace metadata is being retrieved.
        currentSnapshot =
            new ApplicationSecuritySessionSnapshot(
                ApplicationSecuritySessionState.Locked,
                1);

        securitySession.Raise(
            x => x.StateChanged += null,
            currentSnapshot);

        // Complete the repository operation with authenticated-era data.
        workspaceLookupCompletion.SetResult([workspace]);

        await initializationTask;

        // Assert: stale workspace metadata must never reach the main view.
        view.Verify(
            x => x.ShowWorkspaces(
                It.IsAny<IReadOnlyList<WorkspaceListItem>>()),
            Times.Never);

        view.Verify(
            x => x.ShowLockedState(),
            Times.AtLeastOnce);

        workspacePresentationManager.Verify(
            x => x.CloseAll(),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task InitializeAsync_WhenSessionLocksDuringWorkspaceDocumentLookup_DoesNotShowStaleWorkspaces()
    {
        Guid documentId =
            Guid.NewGuid();

        Document document =
            Document.Restore(
                documentId,
                "security-policy.md",
                "Security Policy",
                "test-hash",
                "document.dvault",
                DateTime.UtcNow,
                DocumentStatus.Available);

        Workspace workspace =
            Workspace.CreatePersistent(
                Guid.NewGuid(),
                "Confidential Workspace",
                "Sensitive workspace description");

        workspace.AddDocument(documentId);

        var repository =
            new Mock<IDocumentRepository>();

        var documentLookupStarted =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var documentLookupCompletion =
            new TaskCompletionSource<IReadOnlyList<Document>>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        int documentLookupCount = 0;

        repository
            .Setup(x => x.GetAllAsync(
                It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                int invocation =
                    Interlocked.Increment(
                        ref documentLookupCount);

                if (invocation == 1)
                {
                    return Task.FromResult<IReadOnlyList<Document>>([]);
                }

                documentLookupStarted.TrySetResult();

                return documentLookupCompletion.Task;
            });

        var workspaceRepository =
            new Mock<IWorkspaceRepository>();

        workspaceRepository
            .Setup(x => x.GetAllAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([workspace]);

        var view =
            new Mock<IMainFormView>();

        var securitySession =
            new Mock<IApplicationSecuritySession>();

        ApplicationSecuritySessionSnapshot currentSnapshot =
            new(
                ApplicationSecuritySessionState.Authenticated,
                0);

        securitySession
            .SetupGet(x => x.CurrentSnapshot)
            .Returns(() => currentSnapshot);

        var workspacePresentationManager =
            new Mock<IWorkspacePresentationManager>();

        using MainFormPresenter presenter =
            CreatePresenter(
                view,
                new Mock<IDocumentSearchStore>(),
                repository: repository,
                workspaceRepository: workspaceRepository,
                workspacePresentationManager: workspacePresentationManager,
                applicationSecuritySession: securitySession);

        // Arrange: initial document refresh is empty.
        // Workspace retrieval succeeds and the second document lookup is delayed.
        Task initializationTask =
            presenter.InitializeAsync();

        await documentLookupStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        Assert.Equal(2, documentLookupCount);

        // Act: lock while document names for workspace memberships are pending.
        currentSnapshot =
            new ApplicationSecuritySessionSnapshot(
                ApplicationSecuritySessionState.Locked,
                1);

        securitySession.Raise(
            x => x.StateChanged += null,
            currentSnapshot);

        // The delayed lookup deliberately completes after the lock.
        documentLookupCompletion.SetResult([document]);

        await initializationTask;

        // Assert: neither workspace metadata nor membership filenames are presented.
        view.Verify(
            x => x.ShowWorkspaces(
                It.IsAny<IReadOnlyList<WorkspaceListItem>>()),
            Times.Never);

        view.Verify(
            x => x.ShowLockedState(),
            Times.AtLeastOnce);

        repository.Verify(
            x => x.GetAllAsync(
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task SearchRequested_MatchingChunks_DisplaysSearchResults()
    {
        Guid firstDocumentId =
            Guid.NewGuid();

        Guid secondDocumentId =
            Guid.NewGuid();

        var searchStore =
            new Mock<IDocumentSearchStore>();

        searchStore
            .Setup(x => x.SearchAsync(
                It.Is<SearchDocumentsQuery>(
                    query =>
                        query.SearchText == "security"),
                It.IsAny<SearchDocumentsRankingKey?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                CreateSearchResults(
                    firstDocumentId,
                    secondDocumentId));

        var view =
            new Mock<IMainFormView>();

        view
            .SetupGet(x => x.SearchText)
            .Returns("security");

        _ =
            CreatePresenter(
                view,
                searchStore);

        view.Raise(
            x => x.SearchRequested += null,
            EventArgs.Empty);

        await WaitForBackgroundOperationAsync();

        searchStore.Verify(
            x => x.SearchAsync(
                It.Is<SearchDocumentsQuery>(
                    query =>
                        query.SearchText == "security" &&
                        query.Continuation == null &&
                        query.Limit == 20),
                It.IsAny<SearchDocumentsRankingKey?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        view.Verify(
            x => x.ShowSearchResults(
                It.Is<IReadOnlyList<SearchResultListItem>>(
                    results =>
                        results.Count == 2 &&
                        results[0].DocumentId == firstDocumentId &&
                        results[0].DisplayName == "Security Policy" &&
                        results[0].FileName == "security-policy.md" &&
                        results[0].Snippet ==
                            "Security policy introduction." &&
                        results[0].MatchCount == 2 &&
                        results[1].DocumentId == secondDocumentId &&
                        results[1].DisplayName == "Incident Response" &&
                        results[1].FileName == "incident-response.md" &&
                        results[1].Snippet ==
                            "Security incident response procedure." &&
                        results[1].MatchCount == 1)),
            Times.Once);

        view.Verify(
            x => x.ShowDocuments(
                It.IsAny<IReadOnlyList<DocumentListItem>>()),
            Times.Never);
    }

    [Fact]
    public async Task SearchRequested_WithoutFileTypeFilter_PassesNullFileTypes()
    {
        const string searchText =
            "security";

        var searchStore =
            new Mock<IDocumentSearchStore>();

        searchStore
            .Setup(x => x.SearchAsync(
                It.Is<SearchDocumentsQuery>(
                    query =>
                        query.SearchText == searchText &&
                        query.FileTypes == null),
                It.IsAny<SearchDocumentsRankingKey?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var view =
            new Mock<IMainFormView>();

        view
            .SetupGet(x => x.SearchText)
            .Returns(searchText);

        view
            .SetupGet(x => x.SearchFileType)
            .Returns((string?)null);

        _ =
            CreatePresenter(
                view,
                searchStore);

        view.Raise(
            x => x.SearchRequested += null,
            EventArgs.Empty);

        await WaitForBackgroundOperationAsync();

        searchStore.Verify(
            x => x.SearchAsync(
                It.Is<SearchDocumentsQuery>(
                    query =>
                        query.SearchText == searchText &&
                        query.FileTypes == null &&
                        query.Continuation == null &&
                        query.Limit == 20),
                It.IsAny<SearchDocumentsRankingKey?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SearchRequested_WithFileTypeFilter_PassesSelectedFileType()
    {
        const string searchText =
            "security";

        const string fileType =
            ".pdf";

        var searchStore =
            new Mock<IDocumentSearchStore>();

        searchStore
            .Setup(x => x.SearchAsync(
                It.Is<SearchDocumentsQuery>(
                    query =>
                        query.SearchText == searchText &&
                        query.FileTypes != null &&
                        query.FileTypes.Count == 1 &&
                        query.FileTypes[0] == fileType),
                It.IsAny<SearchDocumentsRankingKey?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var view =
            new Mock<IMainFormView>();

        view
            .SetupGet(x => x.SearchText)
            .Returns(searchText);

        view
            .SetupGet(x => x.SearchFileType)
            .Returns(fileType);

        _ =
            CreatePresenter(
                view,
                searchStore);

        view.Raise(
            x => x.SearchRequested += null,
            EventArgs.Empty);

        await WaitForBackgroundOperationAsync();

        searchStore.Verify(
            x => x.SearchAsync(
                It.Is<SearchDocumentsQuery>(
                    query =>
                        query.SearchText == searchText &&
                        query.FileTypes != null &&
                        query.FileTypes.Count == 1 &&
                        query.FileTypes[0] == fileType &&
                        query.Continuation == null &&
                        query.Limit == 20),
                It.IsAny<SearchDocumentsRankingKey?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SearchRequested_NoMatches_ShowsEmptyStateAndDisablesDocumentActions()
    {
        var searchStore =
            new Mock<IDocumentSearchStore>();

        searchStore
            .Setup(x => x.SearchAsync(
                It.Is<SearchDocumentsQuery>(
                    query => query.SearchText == "unknown"),
                It.IsAny<SearchDocumentsRankingKey?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var view =
            new Mock<IMainFormView>();

        view
            .SetupGet(x => x.SearchText)
            .Returns("unknown");

        _ =
            CreatePresenter(
                view,
                searchStore);

        view.Raise(
            x => x.SearchRequested += null,
            EventArgs.Empty);

        await WaitForBackgroundOperationAsync();

        searchStore.Verify(
            x => x.SearchAsync(
                It.Is<SearchDocumentsQuery>(
                    query =>
                        query.SearchText == "unknown" &&
                        query.Continuation == null &&
                        query.Limit == 20),
                It.IsAny<SearchDocumentsRankingKey?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        view.Verify(
            x => x.ShowEmptyState(),
            Times.Once);

        view.Verify(
            x => x.SetOpenEnabled(false),
            Times.Once);

        view.Verify(
            x => x.SetRemoveEnabled(false),
            Times.Once);

        view.Verify(
            x => x.ShowDocuments(
                It.IsAny<IReadOnlyList<DocumentListItem>>()),
            Times.Never);

        view.Verify(
            x => x.ShowSearchResults(
                It.IsAny<IReadOnlyList<SearchResultListItem>>()),
            Times.Never);
    }

    [Fact]
    public async Task SearchRequested_SearchStoreFails_ShowsErrorAndFailureStatus()
    {
        const string searchText =
            "SEARCH-QUERY-SENTINEL-232";

        string[] sensitiveMarkers =
        [
            "DOCUMENT-CONTENT-SENTINEL-232",
            "ENCRYPTION-KEY-SENTINEL-232",
            @"C:\Sensitive\Vault\SearchReport-232.pdf",
            searchText,
            "EXCEPTION-DETAIL-SENTINEL-232"
        ];

        string errorMessage =
            string.Join("|", sensitiveMarkers);

        var searchStore =
            new Mock<IDocumentSearchStore>();

        searchStore
            .Setup(x => x.SearchAsync(
                It.Is<SearchDocumentsQuery>(
                    query =>
                        query.SearchText == searchText &&
                        query.Continuation == null &&
                        query.Limit == 20),
                It.IsAny<SearchDocumentsRankingKey?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new InvalidOperationException(
                    errorMessage));

        var view =
            new Mock<IMainFormView>();

        view
            .SetupGet(x => x.SearchText)
            .Returns(searchText);

        var logger =
            new RecordingLogger<MainFormPresenter>();

        _ =
            CreatePresenter(
                view,
                searchStore,
                logger: logger);

        view.Raise(
            x => x.SearchRequested += null,
            EventArgs.Empty);

        await WaitForBackgroundOperationAsync();

        searchStore.Verify(
            x => x.SearchAsync(
                It.Is<SearchDocumentsQuery>(
                    query =>
                        query.SearchText == searchText &&
                        query.Continuation == null &&
                        query.Limit == 20),
                It.IsAny<SearchDocumentsRankingKey?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        view.Verify(
            x => x.SetStatus(
                UiMessages.SearchFailedStatus),
            Times.Once);

        view.Verify(
            x => x.ShowError(
                UiMessages.SearchFailedMessage,
                UiMessages.DeskVaultTitle),
            Times.Once);

        AssertSafeSearchFailureLog(
            logger,
            DiagnosticOperationCodes.DocumentSearchFailure,
            sensitiveMarkers);

        view.Verify(
            x => x.ShowDocuments(
                It.IsAny<IReadOnlyList<DocumentListItem>>()),
            Times.Never);

        view.Verify(
            x => x.ShowSearchResults(
                It.IsAny<IReadOnlyList<SearchResultListItem>>()),
            Times.Never);

        view.Verify(
            x => x.ShowEmptyState(),
            Times.Never);
    }

    [Fact]
    public async Task SearchRequested_WhenOlderSearchCompletesAfterNewerSearch_DoesNotReplaceNewerResults()
    {
        const string firstSearchText =
            "first";

        const string secondSearchText =
            "second";

        Guid firstDocumentId =
            Guid.NewGuid();

        Guid secondDocumentId =
            Guid.NewGuid();

        var firstSearchCompletion =
            new TaskCompletionSource<IReadOnlyList<SearchDocumentsResult>>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var secondSearchCompletion =
            new TaskCompletionSource<IReadOnlyList<SearchDocumentsResult>>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var searchStore =
            new Mock<IDocumentSearchStore>();

        searchStore
            .Setup(x => x.SearchAsync(
                It.Is<SearchDocumentsQuery>(
                    query =>
                        query.SearchText == firstSearchText),
                It.IsAny<SearchDocumentsRankingKey?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Returns((
                SearchDocumentsQuery _,
                SearchDocumentsRankingKey? _,
                int _,
                CancellationToken _) =>
                    firstSearchCompletion.Task);

        searchStore
            .Setup(x => x.SearchAsync(
                It.Is<SearchDocumentsQuery>(
                    query =>
                        query.SearchText == secondSearchText),
                It.IsAny<SearchDocumentsRankingKey?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Returns((
                SearchDocumentsQuery _,
                SearchDocumentsRankingKey? _,
                int _,
                CancellationToken _) =>
                    secondSearchCompletion.Task);

        var view =
            new Mock<IMainFormView>();

        var searchText =
            firstSearchText;

        view
            .SetupGet(x => x.SearchText)
            .Returns(() => searchText);

        _ =
            CreatePresenter(
                view,
                searchStore);

        view.Raise(
            x => x.SearchRequested += null,
            EventArgs.Empty);

        await WaitForBackgroundOperationAsync();

        searchText =
            secondSearchText;

        view.Raise(
            x => x.SearchRequested += null,
            EventArgs.Empty);

        await WaitForBackgroundOperationAsync();

        secondSearchCompletion.SetResult(
        [
            CreateResult(
                "Second Search Result")
        ]);

        await WaitForBackgroundOperationAsync();

        firstSearchCompletion.SetResult(
        [
            new SearchDocumentsResult(
                firstDocumentId,
                "first-search-result.md",
                "First Search Result",
                [
                    new SearchMatch(
                        SearchMatchSource.ProcessedContent,
                        SearchMatchKind.Exact,
                        "First search matching content.")
                ],
                1)
        ]);

        await WaitForBackgroundOperationAsync();

        view.Verify(
            x => x.ShowSearchResults(
                It.Is<IReadOnlyList<SearchResultListItem>>(
                    results =>
                        results.Count == 1 &&
                        results[0].DisplayName ==
                            "Second Search Result")),
            Times.Once);

        view.Verify(
            x => x.ShowSearchResults(
                It.Is<IReadOnlyList<SearchResultListItem>>(
                    results =>
                        results.Count == 1 &&
                        results[0].DocumentId == firstDocumentId)),
            Times.Never);

        searchStore.Verify(
            x => x.SearchAsync(
                It.Is<SearchDocumentsQuery>(
                    query =>
                        query.SearchText == firstSearchText),
                It.IsAny<SearchDocumentsRankingKey?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        searchStore.Verify(
            x => x.SearchAsync(
                It.Is<SearchDocumentsQuery>(
                    query =>
                        query.SearchText == secondSearchText),
                It.IsAny<SearchDocumentsRankingKey?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _ = secondDocumentId;
    }

    [Fact]
    public async Task LoadMoreRequested_WhenSupersededByNewSearch_DoesNotAppendStaleResults()
    {
        const string searchText =
            "security";

        const string newerSearchText =
            "policy";

        var loadMoreCompletion =
            new TaskCompletionSource<IReadOnlyList<SearchDocumentsResult>>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var newerSearchCompletion =
            new TaskCompletionSource<IReadOnlyList<SearchDocumentsResult>>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var searchStore =
            new Mock<IDocumentSearchStore>();

        searchStore
            .Setup(x => x.SearchAsync(
                It.Is<SearchDocumentsQuery>(
                    query =>
                        query.SearchText == searchText &&
                        query.Continuation == null),
                It.IsAny<SearchDocumentsRankingKey?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                .. CreateSearchResultsForBatching(21)
            ]);

        searchStore
            .Setup(x => x.SearchAsync(
                It.Is<SearchDocumentsQuery>(
                    query =>
                        query.SearchText == searchText &&
                        query.Continuation != null),
                It.IsAny<SearchDocumentsRankingKey?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Returns((
                SearchDocumentsQuery _,
                SearchDocumentsRankingKey? _,
                int _,
                CancellationToken _) =>
                    loadMoreCompletion.Task);

        searchStore
            .Setup(x => x.SearchAsync(
                It.Is<SearchDocumentsQuery>(
                    query =>
                        query.SearchText == newerSearchText &&
                        query.Continuation == null),
                It.IsAny<SearchDocumentsRankingKey?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Returns((
                SearchDocumentsQuery _,
                SearchDocumentsRankingKey? _,
                int _,
                CancellationToken _) =>
                    newerSearchCompletion.Task);

        var view =
            new Mock<IMainFormView>();

        var currentSearchText =
            searchText;

        view
            .SetupGet(x => x.SearchText)
            .Returns(() => currentSearchText);

        _ =
            CreatePresenter(
                view,
                searchStore);

        view.Raise(
            x => x.SearchRequested += null,
            EventArgs.Empty);

        await WaitForBackgroundOperationAsync();

        view.Raise(
            x => x.LoadMoreSearchResultsRequested += null,
            EventArgs.Empty);

        await WaitForBackgroundOperationAsync();

        currentSearchText =
            newerSearchText;

        view.Raise(
            x => x.SearchRequested += null,
            EventArgs.Empty);

        await WaitForBackgroundOperationAsync();

        IReadOnlyList<SearchDocumentsResult> newerResults =
        [
            CreateResult(
                "Policy Search Result")
        ];

        newerSearchCompletion.SetResult(
            newerResults);

        await WaitForBackgroundOperationAsync();

        IReadOnlyList<SearchDocumentsResult> staleResults =
        [
            CreateResult(
                "Stale Load More Result")
        ];

        loadMoreCompletion.SetResult(
            staleResults);

        await WaitForBackgroundOperationAsync();

        view.Verify(
            x => x.ShowSearchResults(
                It.Is<IReadOnlyList<SearchResultListItem>>(
                    results =>
                        results.Count == 1 &&
                        results[0].DisplayName ==
                            "Policy Search Result")),
            Times.Once);

        view.Verify(
            x => x.AppendSearchResults(
                It.Is<IReadOnlyList<SearchResultListItem>>(
                    results =>
                        results.Any(
                            result =>
                                result.DisplayName ==
                                "Stale Load More Result"))),
            Times.Never);
    }

    [Fact]
    public async Task LoadMoreRequested_WhenSearchStoreFails_LogsSafeDiagnosticAndKeepsRetryEnabled()
    {
        const string searchText =
            "SEARCH-QUERY-SENTINEL-232";

        string[] sensitiveMarkers =
        [
            "DOCUMENT-CONTENT-SENTINEL-232",
            "ENCRYPTION-KEY-SENTINEL-232",
            @"C:\Sensitive\Vault\SearchReport-232.pdf",
            searchText,
            "EXCEPTION-DETAIL-SENTINEL-232"
        ];

        var searchStore =
            new Mock<IDocumentSearchStore>();

        searchStore
            .Setup(x => x.SearchAsync(
                It.Is<SearchDocumentsQuery>(
                    query =>
                        query.SearchText == searchText &&
                        query.Continuation == null &&
                        query.Limit == 20),
                It.IsAny<SearchDocumentsRankingKey?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                CreateSearchResultsForBatching(21));

        searchStore
            .Setup(x => x.SearchAsync(
                It.Is<SearchDocumentsQuery>(
                    query =>
                        query.SearchText == searchText &&
                        query.Continuation != null &&
                        query.Limit == 20),
                It.IsAny<SearchDocumentsRankingKey?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new InvalidOperationException(
                    string.Join("|", sensitiveMarkers)));

        var view =
            new Mock<IMainFormView>();

        view
            .SetupGet(x => x.SearchText)
            .Returns(searchText);

        var logger =
            new RecordingLogger<MainFormPresenter>();

        _ =
            CreatePresenter(
                view,
                searchStore,
                logger: logger);

        // Arrange: complete an initial search to establish a continuation.
        view.Raise(
            x => x.SearchRequested += null,
            EventArgs.Empty);

        await WaitForBackgroundOperationAsync();

        // Act: the next page fails.
        view.Raise(
            x => x.LoadMoreSearchResultsRequested += null,
            EventArgs.Empty);

        await WaitForBackgroundOperationAsync();

        // Assert: the continuation request happened and retry remains enabled.
        searchStore.Verify(
            x => x.SearchAsync(
                It.Is<SearchDocumentsQuery>(
                    query =>
                        query.SearchText == searchText &&
                        query.Continuation != null &&
                        query.Limit == 20),
                It.IsAny<SearchDocumentsRankingKey?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        view.Verify(
            x => x.SetLoadMoreEnabled(true),
            Times.Exactly(2));

        view.Verify(
            x => x.SetStatus(
                UiMessages.SearchFailedStatus),
            Times.Once);

        view.Verify(
            x => x.ShowError(
                UiMessages.SearchFailedMessage,
                UiMessages.DeskVaultTitle),
            Times.Once);

        AssertSafeSearchFailureLog(
            logger,
            DiagnosticOperationCodes.DocumentSearchLoadMoreFailure,
            sensitiveMarkers);
    }

    [Fact]
    public async Task LoadMoreRequested_UsesReturnedContinuationAndAppendsResults()
    {
        const string searchText =
            "security";

        IReadOnlyList<SearchDocumentsResult> initialSearchResults =
            CreateSearchResultsForBatching(21);

        SearchDocumentsResult finalResult =
            initialSearchResults[20];

        var searchStore =
            new Mock<IDocumentSearchStore>();

        searchStore
            .Setup(x => x.SearchAsync(
                It.Is<SearchDocumentsQuery>(
                    query =>
                        query.SearchText == searchText &&
                        query.Continuation == null &&
                        query.Limit == 20),
                It.IsAny<SearchDocumentsRankingKey?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                initialSearchResults);

        searchStore
            .Setup(x => x.SearchAsync(
                It.Is<SearchDocumentsQuery>(
                    query =>
                        query.SearchText == searchText &&
                        query.Continuation != null &&
                        query.Limit == 20),
                It.IsAny<SearchDocumentsRankingKey?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                finalResult
            ]);

        var view =
            new Mock<IMainFormView>();

        view
            .SetupGet(x => x.SearchText)
            .Returns(searchText);

        _ =
            CreatePresenter(
                view,
                searchStore);

        view.Raise(
            x => x.SearchRequested += null,
            EventArgs.Empty);

        await WaitForBackgroundOperationAsync();

        view.Raise(
            x => x.LoadMoreSearchResultsRequested += null,
            EventArgs.Empty);

        await WaitForBackgroundOperationAsync();

        searchStore.Verify(
            x => x.SearchAsync(
                It.Is<SearchDocumentsQuery>(
                    query =>
                        query.SearchText == searchText &&
                        query.Continuation == null &&
                        query.Limit == 20),
                It.IsAny<SearchDocumentsRankingKey?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        searchStore.Verify(
            x => x.SearchAsync(
                It.Is<SearchDocumentsQuery>(
                    query =>
                        query.SearchText == searchText &&
                        query.Continuation != null &&
                        query.Limit == 20),
                It.IsAny<SearchDocumentsRankingKey?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        view.Verify(
            x => x.AppendSearchResults(
                It.Is<IReadOnlyList<SearchResultListItem>>(
                    results =>
                        results.Count == 1 &&
                        results[0].DocumentId ==
                            finalResult.DocumentId)),
            Times.Once);

        view.Verify(
            x => x.ShowSearchResults(
                It.Is<IReadOnlyList<SearchResultListItem>>(
                    results =>
                        results.Count == 20)),
            Times.Once);
    }

    [Fact]
    public async Task LoadMoreRequested_WithFileTypeFilter_PreservesSelectedFileType()
    {
        const string searchText =
            "security";

        const string fileType =
            ".pdf";

        IReadOnlyList<SearchDocumentsResult> searchResults =
            CreateSearchResultsForBatching(21);

        var searchStore =
            new Mock<IDocumentSearchStore>();

        searchStore
            .Setup(x => x.SearchAsync(
                It.Is<SearchDocumentsQuery>(
                    query =>
                        query.SearchText == searchText),
                It.IsAny<SearchDocumentsRankingKey?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        var view =
            new Mock<IMainFormView>();

        view
            .SetupGet(x => x.SearchText)
            .Returns(searchText);

        view
            .SetupGet(x => x.SearchFileType)
            .Returns(fileType);

        _ =
            CreatePresenter(
                view,
                searchStore);

        view.Raise(
            x => x.SearchRequested += null,
            EventArgs.Empty);

        await WaitForBackgroundOperationAsync();

        view.Raise(
            x => x.LoadMoreSearchResultsRequested += null,
            EventArgs.Empty);

        await WaitForBackgroundOperationAsync();

        searchStore.Verify(
            x => x.SearchAsync(
                It.Is<SearchDocumentsQuery>(
                    query =>
                        query.SearchText == searchText &&
                        query.FileTypes != null &&
                        query.FileTypes.Count == 1 &&
                        query.FileTypes[0] == fileType &&
                        query.Continuation == null &&
                        query.Limit == 20),
                It.IsAny<SearchDocumentsRankingKey?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        searchStore.Verify(
            x => x.SearchAsync(
                It.Is<SearchDocumentsQuery>(
                    query =>
                        query.SearchText == searchText &&
                        query.FileTypes != null &&
                        query.FileTypes.Count == 1 &&
                        query.FileTypes[0] == fileType &&
                        query.Continuation != null &&
                        query.Limit == 20),
                It.IsAny<SearchDocumentsRankingKey?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task LoadMoreRequested_WhenFinalBatchIsReturned_DisablesLoadMore()
    {
        const string searchText =
            "security";

        IReadOnlyList<SearchDocumentsResult> initialSearchResults =
            CreateSearchResultsForBatching(21);

        SearchDocumentsResult finalResult =
            initialSearchResults[20];

        var searchStore =
            new Mock<IDocumentSearchStore>();

        searchStore
            .Setup(x => x.SearchAsync(
                It.Is<SearchDocumentsQuery>(
                    query =>
                        query.SearchText == searchText &&
                        query.Continuation == null &&
                        query.Limit == 20),
                It.IsAny<SearchDocumentsRankingKey?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                initialSearchResults);

        searchStore
            .Setup(x => x.SearchAsync(
                It.Is<SearchDocumentsQuery>(
                    query =>
                        query.SearchText == searchText &&
                        query.Continuation != null &&
                        query.Limit == 20),
                It.IsAny<SearchDocumentsRankingKey?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                finalResult
            ]);

        var view =
            new Mock<IMainFormView>();

        view
            .SetupGet(x => x.SearchText)
            .Returns(searchText);

        _ =
            CreatePresenter(
                view,
                searchStore);

        view.Raise(
            x => x.SearchRequested += null,
            EventArgs.Empty);

        await WaitForBackgroundOperationAsync();

        view.Raise(
            x => x.LoadMoreSearchResultsRequested += null,
            EventArgs.Empty);

        await WaitForBackgroundOperationAsync();

        view.Verify(
            x => x.SetLoadMoreEnabled(false),
            Times.AtLeastOnce);

        view.Verify(
            x => x.AppendSearchResults(
                It.Is<IReadOnlyList<SearchResultListItem>>(
                    results =>
                        results.Count == 1 &&
                        results[0].DocumentId ==
                            finalResult.DocumentId)),
            Times.Once);
    }

    [Fact]
    public async Task LoadMoreRequested_WithoutContinuation_DisablesLoadMoreAndDoesNotSearch()
    {
        const string searchText =
            "security";

        var searchStore =
            new Mock<IDocumentSearchStore>();

        var view =
            new Mock<IMainFormView>();

        view
            .SetupGet(x => x.SearchText)
            .Returns(searchText);

        _ =
            CreatePresenter(
                view,
                searchStore);

        view.Raise(
            x => x.LoadMoreSearchResultsRequested += null,
            EventArgs.Empty);

        await WaitForBackgroundOperationAsync();

        searchStore.Verify(
            x => x.SearchAsync(
                It.IsAny<SearchDocumentsQuery>(),
                It.IsAny<SearchDocumentsRankingKey?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        view.Verify(
            x => x.SetLoadMoreEnabled(false),
            Times.Once);
    }

    [Fact]
    public async Task OpenRequested_SelectedDocument_OpensDocumentInWorkspace()
    {
        Guid documentId =
            Guid.NewGuid();

        const string fileName =
            "security-policy.md";

        const string content =
            "# Security Policy";

        var searchStore =
            new Mock<IDocumentSearchStore>();

        var view =
            new Mock<IMainFormView>();

        view
            .SetupGet(x => x.SelectedDocumentId)
            .Returns(documentId);

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetByIdAsync(
                documentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Document.Create(
                    documentId,
                    fileName,
                    "Security Policy",
                    "test-hash",
                    "document.dvault"));

        var documentReader =
            new Mock<IDocumentReader>();

        documentReader
            .Setup(x => x.OpenReadAsync(
                documentId,
                It.IsAny<string>(),
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            CreateContentStream(
                content));

        var workspaceView =
            new Mock<IDocumentWorkspaceView>();

        var documentViewer =
            new Mock<IDocumentViewer>();

        var workspaceRepository =
            new Mock<IWorkspaceRepository>();

        var getDocumentHandler =
            new GetDocumentHandler(
                repository.Object,
                NullLogger<GetDocumentHandler>.Instance);

        var openDocumentHandler =
            new OpenDocumentHandler(
                repository.Object,
                documentReader.Object,
                new DocumentProcessingOptions(),
                NullLogger<OpenDocumentHandler>.Instance);

        var removeDocumentHandler =
            new RemoveDocumentHandler(
                repository.Object,
                workspaceRepository.Object,
                new Mock<IStorageService>().Object,
                NullLogger<RemoveDocumentHandler>.Instance);

        var workspaceId =
            Guid.NewGuid();

        var applicationSecuritySession =
            new Mock<IApplicationSecuritySession>();

        applicationSecuritySession
            .SetupGet(x => x.CurrentSnapshot)
            .Returns(
                new ApplicationSecuritySessionSnapshot(
                    ApplicationSecuritySessionState.Authenticated,
                    0));

        var workspacePresenter =
            new DocumentWorkspacePresenter(
                workspaceId,
                workspaceView.Object,
                documentViewer.Object,
                getDocumentHandler,
                openDocumentHandler,
                removeDocumentHandler,
                NullLogger<DocumentWorkspacePresenter>.Instance,
                applicationSecuritySession.Object,
                0L);

        var documentPresentation =
            new WorkspaceDocumentPresentation(
                documentId,
                workspacePresenter,
                workspaceView.Object);

        var documentPresentationFactory =
            new Mock<IWorkspaceDocumentPresentationFactory>();

        documentPresentationFactory
            .Setup(x => x.Create(
                workspaceId,
                documentId,
                0L))
            .Returns(documentPresentation);

        var workspacePresentationView =
            new Mock<IWorkspacePresentationView>();

        var listDocumentsHandler =
            new ListDocumentsHandler(
                repository.Object,
                NullLogger<ListDocumentsHandler>.Instance);

        var activeWorkspaceRegistry =
            new Mock<IActiveWorkspaceRegistry>();

        activeWorkspaceRegistry
            .Setup(registry => registry.Get(workspaceId))
            .Returns((Workspace?)null);

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

        var workspaceDialogService =
            new Mock<IWorkspaceDialogService>();

        var workspacePresentation =
            new WorkspacePresentation(
                workspaceId,
                WorkspaceType.Temporary,
                null,
                null,
                documentPresentationFactory.Object,
                listDocumentsHandler,
                addDocumentToWorkspaceHandler,
                removeDocumentFromWorkspaceHandler,
                renameWorkspaceHandler,
                saveTemporaryWorkspaceHandler,
                updateWorkspaceDescriptionHandler,
                workspaceDialogService.Object,
                workspacePresentationView.Object,
                NullLogger<WorkspacePresentation>.Instance,
                applicationSecuritySession.Object,
                0L);

        var workspacePresentationManager =
            new Mock<IWorkspacePresentationManager>();

        workspacePresentationManager
            .Setup(x => x.GetOrCreate(
                It.IsAny<Guid>(),
                It.IsAny<WorkspaceType>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<long>()))
            .Returns(workspacePresentation);

        _ =
            CreatePresenter(
                view,
                searchStore,
                repository,
                documentReader,
                workspacePresentationManager:
                    workspacePresentationManager,
                applicationSecuritySession:
                    applicationSecuritySession);

        view.Raise(
            x => x.OpenRequested += null,
            EventArgs.Empty);

        await WaitForBackgroundOperationAsync();

        repository.Verify(
            x => x.GetByIdAsync(
                documentId,
                It.IsAny<CancellationToken>()),
            Times.Once);

        repository.Verify(
            x => x.GetAllAsync(
                It.IsAny<CancellationToken>()),
            Times.Once);

        workspacePresentationManager.Verify(
            x => x.GetOrCreate(
                It.IsAny<Guid>(),
                It.IsAny<WorkspaceType>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<long>()),
            Times.Once);

    }


    [Fact]
    public async Task OpenRequested_WhenSessionLocksDuringWorkspaceCreation_DoesNotCreatePresentation()
    {
        Guid documentId = Guid.NewGuid();

        var repository =
            new Mock<IDocumentRepository>();

        var workspaceCreationStarted =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var workspaceCreationCompletion =
            new TaskCompletionSource<Document?>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        repository
            .Setup(x => x.GetByIdAsync(
                documentId,
                It.IsAny<CancellationToken>()))
            .Callback<Guid, CancellationToken>(
                (_, _) => workspaceCreationStarted.TrySetResult())
            .Returns(workspaceCreationCompletion.Task);

        var view =
            new Mock<IMainFormView>();

        view
            .SetupGet(x => x.SelectedDocumentId)
            .Returns(documentId);

        var securitySession =
            new Mock<IApplicationSecuritySession>();

        ApplicationSecuritySessionSnapshot currentSnapshot =
            new(
                ApplicationSecuritySessionState.Authenticated,
                0);

        securitySession
            .SetupGet(x => x.CurrentSnapshot)
            .Returns(() => currentSnapshot);

        var workspacePresentationManager =
            new Mock<IWorkspacePresentationManager>();

        var activeWorkspaceRegistry =
            new Mock<IActiveWorkspaceRegistry>();

        using MainFormPresenter presenter =
            CreatePresenter(
                view,
                new Mock<IDocumentSearchStore>(),
                repository: repository,
                workspacePresentationManager: workspacePresentationManager,
                activeWorkspaceRegistry: activeWorkspaceRegistry,
                applicationSecuritySession: securitySession);

        // Arrange: begin opening a document and pause workspace creation
        // while the document metadata lookup is pending.
        view.Raise(
            x => x.OpenRequested += null,
            EventArgs.Empty);

        await workspaceCreationStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        // Act: revoke access before the workspace creation handler completes.
        currentSnapshot =
            new ApplicationSecuritySessionSnapshot(
                ApplicationSecuritySessionState.Locked,
                1);

        securitySession.Raise(
            x => x.StateChanged += null,
            currentSnapshot);

        // Complete the handler with authenticated-era document metadata.
        workspaceCreationCompletion.SetResult(
            Document.Create(
                documentId,
                "security-policy.md",
                "Security Policy",
                "test-hash",
                "document.dvault"));

        await WaitForBackgroundOperationAsync();

        // Assert: the stale continuation must not create a presentation.
        workspacePresentationManager.Verify(
            x => x.GetOrCreate(
                It.IsAny<Guid>(),
                It.IsAny<WorkspaceType>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<long>()),
            Times.Never);

        view.Verify(
            x => x.SetStatus(
                UiMessages.DocumentOpenedStatus),
            Times.Never);

        view.Verify(
            x => x.SetStatus(
                UiMessages.UnableToOpenDocumentStatus),
            Times.Never);

        view.Verify(
            x => x.ShowError(
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Never);

        // The operation's finally block must not re-enable Open.
        view.Verify(
            x => x.SetOpenEnabled(true),
            Times.Never);

        view.Verify(
            x => x.ShowLockedState(),
            Times.AtLeastOnce);

        workspacePresentationManager.Verify(
            x => x.CloseAll(),
            Times.AtLeastOnce);
    }


    [Fact]
    public async Task OpenRequested_WhenTemporaryPresentationAlreadyExists_ActivatesExistingPresentationWithoutCreatingAnother()
    {
        Guid documentId =
            Guid.NewGuid();

        Guid workspaceId =
            Guid.NewGuid();

        var searchStore =
            new Mock<IDocumentSearchStore>();

        var view =
            new Mock<IMainFormView>();

        view
            .SetupGet(x => x.SelectedDocumentId)
            .Returns(documentId);

        var repository =
            new Mock<IDocumentRepository>();

        var workspaceRepository =
            new Mock<IWorkspaceRepository>();

        var activeWorkspaceRegistry =
            new Mock<IActiveWorkspaceRegistry>();

        activeWorkspaceRegistry
            .Setup(registry => registry.Get(workspaceId))
            .Returns((Workspace?)null);

        var documentPresentationFactory =
            new Mock<IWorkspaceDocumentPresentationFactory>();

        var workspacePresentationView =
            new Mock<IWorkspacePresentationView>();

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

        var workspaceDialogService =
            new Mock<IWorkspaceDialogService>();

        var applicationSecuritySession =
            new Mock<IApplicationSecuritySession>();

        applicationSecuritySession
            .SetupGet(x => x.CurrentSnapshot)
            .Returns(
                new ApplicationSecuritySessionSnapshot(
                    ApplicationSecuritySessionState.Authenticated,
                    0));

        var workspacePresentation =
            new WorkspacePresentation(
                workspaceId,
                WorkspaceType.Temporary,
                null,
                null,
                documentPresentationFactory.Object,
                listDocumentsHandler,
                addDocumentToWorkspaceHandler,
                removeDocumentFromWorkspaceHandler,
                renameWorkspaceHandler,
                saveTemporaryWorkspaceHandler,
                updateWorkspaceDescriptionHandler,
                workspaceDialogService.Object,
                workspacePresentationView.Object,
                NullLogger<WorkspacePresentation>.Instance,
                applicationSecuritySession.Object,
                0L);

        var workspacePresentationManager =
            new Mock<IWorkspacePresentationManager>();

        workspacePresentationManager
            .Setup(x =>
                x.FindTemporaryByDocument(documentId))
            .Returns(workspacePresentation);

        _ =
            CreatePresenter(
                view,
                searchStore,
                repository,
                workspacePresentationManager:
                    workspacePresentationManager,
                applicationSecuritySession:
                    applicationSecuritySession);

        view.Raise(
            x => x.OpenRequested += null,
            EventArgs.Empty);

        await WaitForBackgroundOperationAsync();

        workspacePresentationManager.Verify(
            x =>
                x.FindTemporaryByDocument(documentId),
            Times.Once);

        workspacePresentationManager.Verify(
            x =>
                x.GetOrCreate(
                    It.IsAny<Guid>(),
                    It.IsAny<WorkspaceType>(),
                    It.IsAny<string?>(),
                    It.IsAny<string?>(),
                    It.IsAny<long>()),
            Times.Never);

        workspacePresentationView.Verify(
            x => x.ActivateWorkspace(),
            Times.Once);

        view.Verify(
            x => x.SetStatus(
                UiMessages.DocumentOpenedStatus),
            Times.Once);
    }

    [Fact]
    public async Task OpenRequested_WithoutSelection_DoesNothing()
    {
        var searchStore =
            new Mock<IDocumentSearchStore>();

        var view =
            new Mock<IMainFormView>();

        view
            .SetupGet(x => x.SelectedDocumentId)
            .Returns((Guid?)null);

        var repository =
            new Mock<IDocumentRepository>();

        var documentReader =
            new Mock<IDocumentReader>();

        _ =
            CreatePresenter(
                view,
                searchStore,
                repository,
                documentReader);

        view.Raise(
            x => x.OpenRequested += null,
            EventArgs.Empty);

        await WaitForBackgroundOperationAsync();

        repository.Verify(
            x => x.GetByIdAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        documentReader.Verify(
        x => x.OpenReadAsync(
            It.IsAny<Guid>(),
            It.IsAny<string>(),
            It.IsAny<long>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task OpenRequested_WhenOpeningFails_ShowsErrorAndFailureStatus()
    {
        Guid documentId =
            Guid.NewGuid();

        string[] sensitiveMarkers =
        [
            "DOCUMENT-CONTENT-SENTINEL-232",
            "ENCRYPTION-KEY-SENTINEL-232",
            @"C:\Sensitive\Vault\OpenReport-232.pdf",
            "EXCEPTION-DETAIL-SENTINEL-232"
        ];

        string errorMessage =
            string.Join("|", sensitiveMarkers);

        var searchStore =
            new Mock<IDocumentSearchStore>();

        var view =
            new Mock<IMainFormView>();

        view
            .SetupGet(x => x.SelectedDocumentId)
            .Returns(documentId);

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetByIdAsync(
                documentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Document.Create(
                    documentId,
                    "security-policy.md",
                    "Security Policy",
                    "test-hash",
                    "document.dvault"));

        var documentReader =
            new Mock<IDocumentReader>();

        documentReader
            .Setup(x => x.OpenReadAsync(
                documentId,
                It.IsAny<string>(),
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
            new InvalidOperationException(
                errorMessage));

        var logger =
            new RecordingLogger<MainFormPresenter>();

        _ =
            CreatePresenter(
                view,
                searchStore,
                repository,
                documentReader,
                logger: logger);

        view.Raise(
            x => x.OpenRequested += null,
            EventArgs.Empty);

        await WaitForBackgroundOperationAsync();

        view.Verify(
            x => x.SetStatus(
                UiMessages.UnableToOpenDocumentStatus),
            Times.Once);

        view.Verify(
            x => x.ShowError(
                UiMessages.UnableToOpenDocument,
                UiMessages.OpenDocumentTitle),
            Times.Once);

        view.Verify(
            x => x.SetOpenEnabled(
                It.IsAny<bool>()),
            Times.AtLeastOnce);

        view.Verify(
            x => x.ShowError(
                It.Is<string>(
                    message => sensitiveMarkers.Any(
                        marker => message.Contains(
                            marker,
                            StringComparison.Ordinal))),
                It.IsAny<string>()),
            Times.Never);

        RecordedLogEntry entry =
            Assert.Single(
                logger.Entries,
                item => item.Level == LogLevel.Error);

        Assert.Contains(
            LogMessages.DocumentOpenFailed,
            entry.Message);

        Assert.Contains(
            $"OperationCode={DiagnosticOperationCodes.DocumentOpenFailure}",
            entry.Message);

        string failureType =
            Assert.IsType<string>(
                entry.Properties["FailureType"]);

        Assert.False(
            string.IsNullOrWhiteSpace(failureType));

        Assert.Contains(
            $"FailureType={failureType}",
            entry.Message);

        Assert.DoesNotContain(
            documentId.ToString(),
            entry.Message);

        Assert.Null(entry.Exception);

        foreach (string marker in sensitiveMarkers)
        {
            Assert.DoesNotContain(marker, entry.Message);

            foreach (object? value in entry.Properties.Values)
            {
                Assert.DoesNotContain(
                    marker,
                    value?.ToString() ?? string.Empty);
            }
        }

        foreach (object? value in entry.Properties.Values)
        {
            Assert.DoesNotContain(
                documentId.ToString(),
                value?.ToString() ?? string.Empty);
        }
    }

    [Fact]
    public async Task ImportRequested_WhenDocumentIsDuplicate_ShowsWarningAndDoesNotRefreshDocuments()
    {
        const string filePath =
            @"C:\Documents\security-policy.md";

        const string duplicateHash =
            "duplicate-hash";

        const string duplicateDescription =
            "The document has already been imported.";

        var searchStore =
            new Mock<IDocumentSearchStore>();

        var view =
            new Mock<IMainFormView>();

        view
            .SetupGet(x => x.SelectedFilePath)
            .Returns(filePath);

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.ExistsByHashAsync(
                duplicateHash,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var hashService =
            new Mock<IHashService>();

        hashService
            .Setup(x => x.ComputeSha256Async(
                filePath,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(duplicateHash);

        var importValidator =
            CreateSuccessfulImportValidator();

        _ =
            CreatePresenter(
                view,
                searchStore,
                repository,
                hashService: hashService,
                importValidator: importValidator);

        view.Raise(
            x => x.ImportRequested += null,
            EventArgs.Empty);

        await WaitForBackgroundOperationAsync();

        hashService.Verify(
            x => x.ComputeSha256Async(
                filePath,
                It.IsAny<CancellationToken>()),
            Times.Once);

        repository.Verify(
            x => x.ExistsByHashAsync(
                duplicateHash,
                It.IsAny<CancellationToken>()),
            Times.Once);

        repository.Verify(
            x => x.AddAsync(
                It.IsAny<Document>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        view.Verify(
            x => x.SetStatus(
                duplicateDescription),
            Times.Once);

        view.Verify(
            x => x.ShowWarning(
                duplicateDescription,
                UiMessages.ImportFailedTitle),
            Times.Once);

        view.Verify(
            x => x.ShowDocuments(
                It.IsAny<IReadOnlyList<DocumentListItem>>()),
            Times.Never);

        view.Verify(
            x => x.SetImportEnabled(true),
            Times.Once);
    }

    [Fact]
    public async Task ImportRequested_WhenImportSucceeds_ProcessesDocumentAndRefreshesDocuments()
    {
        const string filePath =
            @"C:\Documents\security-policy.md";

        const string hash =
            "successful-import-hash";

        Guid? importedDocumentId = null;

        var searchStore =
            new Mock<IDocumentSearchStore>();

        var view =
            new Mock<IMainFormView>();

        view
            .SetupGet(x => x.SelectedFilePath)
            .Returns(filePath);

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.ExistsByHashAsync(
                hash,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        repository
            .Setup(x => x.AddAsync(
                It.IsAny<Document>(),
                It.IsAny<CancellationToken>()))
            .Callback<Document, CancellationToken>(
                (document, _) =>
                {
                    importedDocumentId = document.Id;
                })
            .Returns(Task.CompletedTask);

        repository
            .Setup(x => x.GetAllAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                if (importedDocumentId is not Guid documentId)
                {
                    return [];
                }

                return
                [
                    Document.Restore(
                        documentId,
                        "security-policy.md",
                        "security-policy",
                        hash,
                        "document.dvault",
                        DateTime.UtcNow,
                        DocumentStatus.Imported)
                ];
            });

        var hashService =
            new Mock<IHashService>();

        hashService
            .Setup(x => x.ComputeSha256Async(
                filePath,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(hash);

        var storageService =
            new Mock<IStorageService>();

        storageService
            .Setup(x => x.StoreAsync(
                filePath,
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>(),
                hash))
            .ReturnsAsync("document.dvault");

        var processingService =
            new Mock<IDocumentProcessingService>();

        _ =
            CreatePresenter(
                view,
                searchStore,
                repository,
                hashService: hashService,
                storageService: storageService,
                processingService: processingService);

        view.Raise(
            x => x.ImportRequested += null,
            EventArgs.Empty);

        await WaitForBackgroundOperationAsync();

        Assert.NotNull(importedDocumentId);

        hashService.Verify(
            x => x.ComputeSha256Async(
                filePath,
                It.IsAny<CancellationToken>()),
            Times.Once);

        repository.Verify(
            x => x.ExistsByHashAsync(
                hash,
                It.IsAny<CancellationToken>()),
            Times.Once);

        storageService.Verify(
            x => x.StoreAsync(
                filePath,
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>(),
                hash),
            Times.Once);

        repository.Verify(
            x => x.AddAsync(
                It.Is<Document>(
                    document =>
                        document.Id == importedDocumentId &&
                        document.FileName == "security-policy.md" &&
                        document.DisplayName == "security-policy"),
                It.IsAny<CancellationToken>()),
            Times.Once);

        repository.Verify(
            x => x.GetAllAsync(
                It.IsAny<CancellationToken>()),
            Times.Once);

        processingService.Verify(
            x => x.ProcessAsync(
                It.Is<Guid>(
                    documentId =>
                        documentId == importedDocumentId),
                It.IsAny<CancellationToken>()),
            Times.Once);

        view.Verify(
            x => x.ShowDocuments(
                It.Is<IReadOnlyList<DocumentListItem>>(
                    documents =>
                        documents.Count == 1 &&
                        documents[0].Id == importedDocumentId &&
                        documents[0].FileName == "security-policy.md" &&
                        documents[0].ProcessingState == DocumentProcessingState.NeverProcessed.ToString())),
            Times.Once);

        view.Verify(
            x => x.SetSelectedDocumentId(
                importedDocumentId),
            Times.Once);

        view.Verify(
            x => x.SetOpenEnabled(true),
            Times.Once);

        view.Verify(
            x => x.SetStatus(
                "Document imported successfully."),
            Times.Once);

        view.Verify(
            x => x.ShowInformation(
                "Document imported successfully.",
                UiMessages.ImportCompleteTitle),
            Times.Once);

        view.Verify(
            x => x.SetImportEnabled(true),
            Times.Once);
    }

    [Fact]
    public async Task ImportRequested_WhenSessionLocksDuringImport_DoesNotContinueProtectedWork()
    {
        const string filePath =
            @"C:\Documents\security-policy.md";

        const string hash =
            "locked-import-hash";

        var view =
            new Mock<IMainFormView>();

        view
            .SetupGet(x => x.SelectedFilePath)
            .Returns(filePath);

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.ExistsByHashAsync(
                hash,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var importSaveStarted =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var importSaveCompletion =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        repository
            .Setup(x => x.AddAsync(
                It.IsAny<Document>(),
                It.IsAny<CancellationToken>()))
            .Callback<Document, CancellationToken>(
                (_, _) => importSaveStarted.TrySetResult())
            .Returns(() => importSaveCompletion.Task);

        var hashService =
            new Mock<IHashService>();

        hashService
            .Setup(x => x.ComputeSha256Async(
                filePath,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(hash);

        var storageService =
            new Mock<IStorageService>();

        storageService
            .Setup(x => x.StoreAsync(
                filePath,
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>(),
                hash))
            .ReturnsAsync("document.dvault");

        var processingService =
            new Mock<IDocumentProcessingService>();

        var securitySession =
            new Mock<IApplicationSecuritySession>();

        ApplicationSecuritySessionSnapshot currentSnapshot =
            new(
                ApplicationSecuritySessionState.Authenticated,
                0);

        securitySession
            .SetupGet(x => x.CurrentSnapshot)
            .Returns(() => currentSnapshot);

        var workspacePresentationManager =
            new Mock<IWorkspacePresentationManager>();

        using MainFormPresenter presenter =
            CreatePresenter(
                view,
                new Mock<IDocumentSearchStore>(),
                repository: repository,
                hashService: hashService,
                storageService: storageService,
                processingService: processingService,
                workspacePresentationManager: workspacePresentationManager,
                applicationSecuritySession: securitySession);

        // Arrange: begin importing and pause the repository save.
        view.Raise(
            x => x.ImportRequested += null,
            EventArgs.Empty);

        await importSaveStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        // Act: revoke protected access while the import is still pending.
        currentSnapshot =
            new ApplicationSecuritySessionSnapshot(
                ApplicationSecuritySessionState.Locked,
                1);

        securitySession.Raise(
            x => x.StateChanged += null,
            currentSnapshot);

        // Complete the import operation after the session has been locked.
        importSaveCompletion.SetResult();

        await WaitForBackgroundOperationAsync();

        // Assert: the import may finish its already-started persistence,
        // but protected follow-up work must not continue.
        repository.Verify(
            x => x.AddAsync(
                It.IsAny<Document>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        processingService.Verify(
            x => x.ProcessAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        repository.Verify(
            x => x.GetAllAsync(
                It.IsAny<CancellationToken>()),
            Times.Never);

        view.Verify(
            x => x.ShowDocuments(
                It.IsAny<IReadOnlyList<DocumentListItem>>()),
            Times.Never);

        view.Verify(
            x => x.SetSelectedDocumentId(
                It.IsAny<Guid?>()),
            Times.Never);

        view.Verify(
            x => x.SetStatus(
                "Document imported successfully."),
            Times.Never);

        view.Verify(
            x => x.ShowInformation(
                "Document imported successfully.",
                UiMessages.ImportCompleteTitle),
            Times.Never);

        view.Verify(
            x => x.SetImportEnabled(true),
            Times.Never);

        view.Verify(
            x => x.ShowLockedState(),
            Times.AtLeastOnce);

        workspacePresentationManager.Verify(
            x => x.CloseAll(),
            Times.AtLeastOnce);
    }

    [Theory]
    [MemberData(nameof(ImportableFormatsWithoutExtractors))]
    public async Task ImportRequested_WhenNoExtractorIsAvailable_SkipsProcessingAndRefreshesDocuments(
        string extension)
    {
        string filePath =
            $@"C:\Sensitive\PRIVATE_DOCUMENT_SENTINEL_232{extension}";

        string fileName =
            $"PRIVATE_DOCUMENT_SENTINEL_232{extension}";

        const string hash =
            "unsupported-format-hash";

        Guid? importedDocumentId = null;

        var searchStore =
            new Mock<IDocumentSearchStore>();

        var view =
            new Mock<IMainFormView>();

        view
            .SetupGet(x => x.SelectedFilePath)
            .Returns(filePath);

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.ExistsByHashAsync(
                hash,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        repository
            .Setup(x => x.AddAsync(
                It.IsAny<Document>(),
                It.IsAny<CancellationToken>()))
            .Callback<Document, CancellationToken>(
                (document, _) =>
                {
                    importedDocumentId = document.Id;
                })
            .Returns(Task.CompletedTask);

        repository
            .Setup(x => x.GetAllAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                if (importedDocumentId is not Guid documentId)
                {
                    return [];
                }

                return
                [
                    Document.Restore(
                    documentId,
                    fileName,
                    "document",
                    hash,
                    "document.dvault",
                    DateTime.UtcNow,
                    DocumentStatus.Imported)
                ];
            });

        var hashService =
            new Mock<IHashService>();

        hashService
            .Setup(x => x.ComputeSha256Async(
                filePath,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(hash);

        var storageService =
            new Mock<IStorageService>();

        storageService
            .Setup(x => x.StoreAsync(
                filePath,
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>(),
                hash))
            .ReturnsAsync("document.dvault");

        var processingService =
            new Mock<IDocumentProcessingService>();

        var logger =
            new RecordingLogger<MainFormPresenter>();

        _ =
            CreatePresenter(
                view,
                searchStore,
                repository,
                hashService: hashService,
                storageService: storageService,
                processingService: processingService,
                logger: logger);

        view.Raise(
            x => x.ImportRequested += null,
            EventArgs.Empty);

        await WaitForBackgroundOperationAsync();

        Assert.NotNull(importedDocumentId);

        repository.Verify(
            x => x.AddAsync(
                It.Is<Document>(
                    document =>
                        document.Id == importedDocumentId &&
                        document.FileName == fileName &&
                        document.DisplayName == "PRIVATE_DOCUMENT_SENTINEL_232"),
                It.IsAny<CancellationToken>()),
            Times.Once);

        repository.Verify(
            x => x.GetAllAsync(
                It.IsAny<CancellationToken>()),
            Times.Once);

        processingService.Verify(
            x => x.ProcessAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        var skipLog = Assert.Single(
            logger.Entries,
            entry => entry.Message.Contains(
                "Document processing skipped because no text extractor is available for file extension",
                StringComparison.Ordinal));

        Assert.Null(skipLog.Exception);
        Assert.Equal(
            Path.GetExtension(fileName),
            Assert.IsType<string>(skipLog.Properties["FileExtension"]));
        Assert.DoesNotContain(fileName, skipLog.Message);
        Assert.DoesNotContain(
            fileName,
            string.Join(
                Environment.NewLine,
                skipLog.Properties.Select(
                    property => $"{property.Key}={property.Value}")));

        view.Verify(
            x => x.ShowDocuments(
                It.Is<IReadOnlyList<DocumentListItem>>(
                    documents =>
                        documents.Count == 1 &&
                        documents[0].Id == importedDocumentId &&
                        documents[0].FileName == fileName)),
            Times.Once);

        view.Verify(
            x => x.SetSelectedDocumentId(
                importedDocumentId),
            Times.Once);

        view.Verify(
            x => x.SetOpenEnabled(true),
            Times.Once);

        view.Verify(
            x => x.SetStatus(
                "Document imported successfully."),
            Times.Once);

        view.Verify(
            x => x.ShowInformation(
                "Document imported successfully.",
                UiMessages.ImportCompleteTitle),
            Times.Once);

        view.Verify(
            x => x.SetImportEnabled(true),
            Times.Once);
    }

    [Fact]
    public async Task ImportRequested_WhenProcessingFails_ShowsUnexpectedImportError()
    {
        const string filePath =
            @"C:\Documents\security-policy.md";

        const string hash =
            "processing-failure-hash";

        string[] sensitiveMarkers =
        [
            "DOCUMENT-CONTENT-SENTINEL-232",
            "ENCRYPTION-KEY-SENTINEL-232",
            filePath,
            "EXCEPTION-DETAIL-SENTINEL-232"
        ];

        string errorMessage =
            string.Join("|", sensitiveMarkers);

        Guid? importedDocumentId = null;

        var searchStore =
            new Mock<IDocumentSearchStore>();

        var view =
            new Mock<IMainFormView>();

        view
            .SetupGet(x => x.SelectedFilePath)
            .Returns(filePath);

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.ExistsByHashAsync(
                hash,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        repository
            .Setup(x => x.AddAsync(
                It.IsAny<Document>(),
                It.IsAny<CancellationToken>()))
            .Callback<Document, CancellationToken>(
                (document, _) =>
                {
                    importedDocumentId = document.Id;
                })
            .Returns(Task.CompletedTask);

        var hashService =
            new Mock<IHashService>();

        hashService
            .Setup(x => x.ComputeSha256Async(
                filePath,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(hash);

        var storageService =
            new Mock<IStorageService>();

        storageService
            .Setup(x => x.StoreAsync(
                filePath,
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>(),
                hash))
            .ReturnsAsync("document.dvault");

        var processingService =
            new Mock<IDocumentProcessingService>();

        processingService
            .Setup(x => x.ProcessAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new InvalidOperationException(
                    errorMessage));

        var logger =
            new RecordingLogger<MainFormPresenter>();

        _ =
            CreatePresenter(
                view,
                searchStore,
                repository,
                hashService: hashService,
                storageService: storageService,
                processingService: processingService,
                logger: logger);

        view.Raise(
            x => x.ImportRequested += null,
            EventArgs.Empty);

        await WaitForBackgroundOperationAsync();

        Assert.NotNull(importedDocumentId);

        repository.Verify(
            x => x.AddAsync(
                It.Is<Document>(
                    document =>
                        document.Id == importedDocumentId &&
                        document.FileName == "security-policy.md" &&
                        document.DisplayName == "security-policy"),
                It.IsAny<CancellationToken>()),
            Times.Once);

        processingService.Verify(
            x => x.ProcessAsync(
                It.Is<Guid>(
                    documentId =>
                        documentId == importedDocumentId),
                It.IsAny<CancellationToken>()),
            Times.Once);

        repository.Verify(
            x => x.GetAllAsync(
                It.IsAny<CancellationToken>()),
            Times.Never);

        view.Verify(
            x => x.ShowDocuments(
                It.IsAny<IReadOnlyList<DocumentListItem>>()),
            Times.Never);

        view.Verify(
            x => x.SetSelectedDocumentId(
                It.IsAny<Guid?>()),
            Times.Never);

        view.Verify(
            x => x.SetStatus(
                UiMessages.UnexpectedImportError),
            Times.Once);

        view.Verify(
            x => x.ShowError(
                UiMessages.UnexpectedImportError,
                UiMessages.DeskVaultTitle),
            Times.Once);

        view.Verify(
            x => x.SetImportEnabled(true),
            Times.Once);

        view.Verify(
            x => x.ShowError(
                It.Is<string>(
                    message => sensitiveMarkers.Any(
                        marker => message.Contains(
                            marker,
                            StringComparison.Ordinal))),
                It.IsAny<string>()),
            Times.Never);

        RecordedLogEntry entry =
            Assert.Single(
                logger.Entries,
                item => item.Level == LogLevel.Error);

        Assert.Contains(
            LogMessages.DocumentImportFailed,
            entry.Message);

        Assert.Contains(
            $"OperationCode={DiagnosticOperationCodes.DocumentImportUnexpectedFailure}",
            entry.Message);

        Assert.Contains(
            $"FailureType={nameof(InvalidOperationException)}",
            entry.Message);

        Assert.Null(entry.Exception);

        foreach (string marker in sensitiveMarkers)
        {
            Assert.DoesNotContain(marker, entry.Message);

            foreach (object? value in entry.Properties.Values)
            {
                Assert.DoesNotContain(
                    marker,
                    value?.ToString() ?? string.Empty);
            }
        }
    }

    [Fact]
    public async Task ReprocessRequested_WhenProcessingFails_LogsSafeDiagnosticAndShowsGenericError()
    {
        Guid documentId = Guid.NewGuid();

        string[] sensitiveMarkers =
        [
            "DOCUMENT-CONTENT-SENTINEL-232",
            "ENCRYPTION-KEY-SENTINEL-232",
            @"C:\Sensitive\Vault\ReprocessReport-232.csv",
            "EXCEPTION-DETAIL-SENTINEL-232"
        ];

        string errorMessage =
            string.Join("|", sensitiveMarkers);

        var view =
            new Mock<IMainFormView>();

        view
            .SetupGet(x => x.SelectedDocumentId)
            .Returns(documentId);

        view
            .SetupGet(x => x.SelectedDocumentFileName)
            .Returns("ReprocessReport-232.csv");

        var processingService =
            new Mock<IDocumentProcessingService>();

        processingService
            .Setup(x => x.ProcessAsync(
                documentId,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new InvalidOperationException(errorMessage));

        var logger =
            new RecordingLogger<MainFormPresenter>();

        _ = CreatePresenter(
            view,
            new Mock<IDocumentSearchStore>(),
            processingService: processingService,
            logger: logger);

        view.Raise(
            x => x.ReprocessRequested += null,
            EventArgs.Empty);

        await WaitForBackgroundOperationAsync();

        processingService.Verify(
            x => x.ProcessAsync(
                documentId,
                It.IsAny<CancellationToken>()),
            Times.Once);

        view.Verify(
            x => x.SetStatus(
                UiMessages.UnableToReprocessDocumentStatus),
            Times.Once);

        view.Verify(
            x => x.ShowError(
                UiMessages.UnableToReprocessDocument,
                UiMessages.ReprocessDocumentTitle),
            Times.Once);

        view.Verify(
            x => x.ShowError(
                It.Is<string>(
                    message => sensitiveMarkers.Any(
                        marker => message.Contains(
                            marker,
                            StringComparison.Ordinal))),
                It.IsAny<string>()),
            Times.Never);

        view.Verify(
            x => x.SetImportEnabled(true),
            Times.Once);

        view.Verify(
            x => x.SetOpenEnabled(true),
            Times.Once);

        view.Verify(
            x => x.SetRemoveEnabled(true),
            Times.Once);

        RecordedLogEntry entry =
            Assert.Single(
                logger.Entries,
                item => item.Level == LogLevel.Error);

        Assert.Contains(
            LogMessages.DocumentReprocessFailed,
            entry.Message);

        Assert.Contains(
            $"OperationCode={DiagnosticOperationCodes.DocumentReprocessFailure}",
            entry.Message);

        string failureType =
            Assert.IsType<string>(
                entry.Properties["FailureType"]);

        Assert.False(
            string.IsNullOrWhiteSpace(failureType));

        Assert.Contains(
            $"FailureType={failureType}",
            entry.Message);

        Assert.DoesNotContain(
            documentId.ToString(),
            entry.Message);

        Assert.Null(entry.Exception);

        foreach (string marker in sensitiveMarkers)
        {
            Assert.DoesNotContain(marker, entry.Message);

            foreach (object? value in entry.Properties.Values)
            {
                Assert.DoesNotContain(
                    marker,
                    value?.ToString() ?? string.Empty);
            }
        }

        foreach (object? value in entry.Properties.Values)
        {
            Assert.DoesNotContain(
                documentId.ToString(),
                value?.ToString() ?? string.Empty);
        }
    }
    [Fact]
    public async Task ReprocessRequested_WhenProcessingSucceeds_RefreshesDocumentsAndShowsSuccess()
    {
        Guid documentId =
            Guid.NewGuid();

        var searchStore =
            new Mock<IDocumentSearchStore>();

        var view =
            new Mock<IMainFormView>();

        view
            .SetupGet(x => x.SelectedDocumentId)
            .Returns(documentId);

        view
            .SetupGet(x => x.SelectedDocumentFileName)
            .Returns("security-policy.md");

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetAllAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                Document.Restore(
                    documentId,
                    "security-policy.md",
                    "Security Policy",
                    "test-hash",
                    "document.dvault",
                    DateTime.UtcNow,
                    DocumentStatus.Available)
            ]);

        var processingService =
            new Mock<IDocumentProcessingService>();

        processingService
            .Setup(x => x.ProcessAsync(
                documentId,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _ =
            CreatePresenter(
                view,
                searchStore,
                repository,
                processingService: processingService);

        view.Raise(
            x => x.ReprocessRequested += null,
            EventArgs.Empty);

        await WaitForBackgroundOperationAsync();

        processingService.Verify(
            x => x.ProcessAsync(
                documentId,
                It.IsAny<CancellationToken>()),
            Times.Once);

        repository.Verify(
            x => x.GetAllAsync(
                It.IsAny<CancellationToken>()),
            Times.Once);

        view.Verify(
            x => x.ShowDocuments(
                It.Is<IReadOnlyList<DocumentListItem>>(
                    documents =>
                        documents.Count == 1 &&
                        documents[0].Id == documentId &&
                        documents[0].FileName == "security-policy.md")),
            Times.Once);

        view.Verify(
            x => x.SetStatus(
                UiMessages.DocumentReprocessedStatus),
            Times.Once);

        view.Verify(
            x => x.ShowInformation(
                UiMessages.DocumentReprocessedStatus,
                UiMessages.ReprocessDocumentTitle),
            Times.Once);

        view.Verify(
            x => x.SetImportEnabled(true),
            Times.Once);

        view.Verify(
            x => x.SetOpenEnabled(true),
            Times.AtLeastOnce);

        view.Verify(
            x => x.SetRemoveEnabled(true),
            Times.AtLeastOnce);

        view.Verify(
            x => x.SetReprocessEnabled(true),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ReprocessRequested_WhenSessionLocksDuringProcessing_DoesNotPublishStaleSuccessOrReenableControls()
    {
        Guid documentId =
            Guid.NewGuid();

        var view =
            new Mock<IMainFormView>();

        view
            .SetupGet(x => x.SelectedDocumentId)
            .Returns(documentId);

        view
            .SetupGet(x => x.SelectedDocumentFileName)
            .Returns("security-policy.md");

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetAllAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var processingStarted =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var processingCompletion =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var processingService =
            new Mock<IDocumentProcessingService>();

        processingService
            .Setup(x => x.ProcessAsync(
                documentId,
                It.IsAny<CancellationToken>()))
            .Callback<Guid, CancellationToken>(
                (_, _) => processingStarted.TrySetResult())
            .Returns(processingCompletion.Task);

        var securitySession =
            new Mock<IApplicationSecuritySession>();

        ApplicationSecuritySessionSnapshot currentSnapshot =
            new(
                ApplicationSecuritySessionState.Authenticated,
                0);

        securitySession
            .SetupGet(x => x.CurrentSnapshot)
            .Returns(() => currentSnapshot);

        using MainFormPresenter presenter =
            CreatePresenter(
                view,
                new Mock<IDocumentSearchStore>(),
                repository,
                processingService: processingService,
                applicationSecuritySession: securitySession);

        view.Raise(
            x => x.ReprocessRequested += null,
            EventArgs.Empty);

        await processingStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        // Lock the session while processing is still in flight.
        currentSnapshot =
            new ApplicationSecuritySessionSnapshot(
                ApplicationSecuritySessionState.Locked,
                1);

        securitySession.Raise(
            x => x.StateChanged += null,
            currentSnapshot);

        processingCompletion.SetResult();

        await WaitForBackgroundOperationAsync();

        processingService.Verify(
            x => x.ProcessAsync(
                documentId,
                It.IsAny<CancellationToken>()),
            Times.Once);

        repository.Verify(
            x => x.GetAllAsync(
                It.IsAny<CancellationToken>()),
            Times.Never);

        view.Verify(
            x => x.ShowLockedState(),
            Times.AtLeastOnce);

        view.Verify(
            x => x.SetStatus(
                UiMessages.DocumentReprocessedStatus),
            Times.Never);

        view.Verify(
            x => x.ShowInformation(
                UiMessages.DocumentReprocessedStatus,
                UiMessages.ReprocessDocumentTitle),
            Times.Never);

        view.Verify(
            x => x.SetImportEnabled(true),
            Times.Never);

        view.Verify(
            x => x.SetOpenEnabled(true),
            Times.Never);

        view.Verify(
            x => x.SetRemoveEnabled(true),
            Times.Never);

        view.Verify(
            x => x.SetReprocessEnabled(true),
            Times.Never);
    }

    [Fact]
    public async Task ReprocessRequested_UnsupportedDocument_DisablesReprocessAndDoesNotProcess()
    {
        Guid documentId =
            Guid.NewGuid();

        var searchStore =
            new Mock<IDocumentSearchStore>();

        var view =
            new Mock<IMainFormView>();

        view
            .SetupGet(x => x.SelectedDocumentId)
            .Returns(documentId);

        view
            .SetupGet(x => x.SelectedDocumentFileName)
            .Returns("PRIVATE_DOCUMENT_SENTINEL_232.docx");

        var processingService =
            new Mock<IDocumentProcessingService>();

        var logger =
            new RecordingLogger<MainFormPresenter>();

        _ =
            CreatePresenter(
                view,
                searchStore,
                processingService: processingService,
                logger: logger);

        view.Raise(
            x => x.ReprocessRequested += null,
            EventArgs.Empty);

        await WaitForBackgroundOperationAsync();

        processingService.Verify(
            x => x.ProcessAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        var skipLog = Assert.Single(
            logger.Entries,
            entry => entry.Message.Contains(
                "Document reprocessing skipped because no text extractor is available for file extension",
                StringComparison.Ordinal));

        Assert.Null(skipLog.Exception);
        Assert.Equal(
            ".docx",
            Assert.IsType<string>(skipLog.Properties["FileExtension"]));
        Assert.DoesNotContain(
            "PRIVATE_DOCUMENT_SENTINEL_232.docx",
            skipLog.Message);
        Assert.DoesNotContain(
            "PRIVATE_DOCUMENT_SENTINEL_232",
            string.Join(
                Environment.NewLine,
                skipLog.Properties.Select(
                    property => $"{property.Key}={property.Value}")));

        view.Verify(
            x => x.SetReprocessEnabled(false),
            Times.AtLeastOnce);

        view.Verify(
            x => x.SetStatus(
                UiMessages.ReprocessingDocumentStatus),
            Times.Never);
    }

    [Fact]
    public async Task WorkspaceCreateRequested_WhenCreationThrows_LogsSafeDiagnosticAndShowsGenericError()
    {
        string[] sensitiveMarkers =
        [
            "WORKSPACE-NAME-SENTINEL-232",
            "WORKSPACE-DESCRIPTION-SENTINEL-232",
            @"C:\Sensitive\Workspace\workspace.db",
            "EXCEPTION-DETAIL-SENTINEL-232"
        ];

        string exceptionMessage =
            string.Join("|", sensitiveMarkers);

        var workspaceRepository =
            new Mock<IWorkspaceRepository>();

        workspaceRepository
            .Setup(x => x.AddAsync(
                It.IsAny<Workspace>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new InvalidOperationException(exceptionMessage));

        var view =
            new Mock<IMainFormView>();

        view
            .Setup(x => x.ShowCreateWorkspaceDialog())
            .Returns(
                new WorkspaceCreateRequest(
                    sensitiveMarkers[0],
                    sensitiveMarkers[1]));

        var logger =
            new RecordingLogger<MainFormPresenter>();

        _ = CreatePresenter(
            view,
            new Mock<IDocumentSearchStore>(),
            workspaceRepository: workspaceRepository,
            logger: logger);

        view.Raise(
            x => x.WorkspaceCreateRequested += null,
            EventArgs.Empty);

        await WaitForBackgroundOperationAsync();

        workspaceRepository.Verify(
            x => x.AddAsync(
                It.IsAny<Workspace>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        view.Verify(
            x => x.SetStatus(
                UiMessages.UnableToCreateWorkspace),
            Times.Once);

        view.Verify(
            x => x.ShowError(
                UiMessages.UnableToCreateWorkspace,
                UiMessages.CreateWorkspaceTitle),
            Times.Once);

        view.Verify(
            x => x.ShowError(
                It.Is<string>(
                    message => sensitiveMarkers.Any(
                        marker => message.Contains(
                            marker,
                            StringComparison.Ordinal))),
                It.IsAny<string>()),
            Times.Never);

        RecordedLogEntry entry =
            Assert.Single(
                logger.Entries,
                item => item.Level == LogLevel.Error);

        Assert.Contains(
            LogMessages.WorkspaceCreationUnexpectedFailure,
            entry.Message);

        Assert.Contains(
            $"OperationCode={DiagnosticOperationCodes.WorkspaceCreationFailure}",
            entry.Message);

        Assert.Contains(
            $"FailureType={nameof(InvalidOperationException)}",
            entry.Message);

        Assert.Null(entry.Exception);

        foreach (string marker in sensitiveMarkers)
        {
            Assert.DoesNotContain(marker, entry.Message);

            foreach (object? value in entry.Properties.Values)
            {
                Assert.DoesNotContain(
                    marker,
                    value?.ToString() ?? string.Empty);
            }
        }
    }

    [Fact]
    public async Task WorkspaceCloseRequested_WhenCloseThrows_LogsSafeDiagnosticAndShowsGenericError()
    {
        Guid workspaceId = Guid.NewGuid();

        string[] sensitiveMarkers =
        [
            "WORKSPACE-NAME-SENTINEL-232",
            "WORKSPACE-DESCRIPTION-SENTINEL-232",
            @"C:\Sensitive\Workspace\workspace.db",
            "EXCEPTION-DETAIL-SENTINEL-232"
        ];

        string exceptionMessage =
            string.Join("|", sensitiveMarkers);

        Workspace workspace =
            Workspace.CreatePersistent(
                workspaceId,
                sensitiveMarkers[0],
                sensitiveMarkers[1]);

        var activeWorkspaceRegistry =
            new Mock<IActiveWorkspaceRegistry>();

        activeWorkspaceRegistry
            .Setup(x => x.Get(workspaceId))
            .Returns(workspace);

        activeWorkspaceRegistry
            .Setup(x => x.Remove(workspaceId))
            .Throws(
                new InvalidOperationException(exceptionMessage));

        var workspaceRepository =
            new Mock<IWorkspaceRepository>();

        var workspaceView =
            new Mock<IWorkspacePresentationView>();

        var closeWorkspaceSecuritySession =
            new Mock<IApplicationSecuritySession>();

        WorkspacePresentation presentation =
            CreateWorkspacePresentationForCloseTesting(
                workspaceId,
                workspace.Name,
                workspace.Description,
                workspaceView,
                workspaceRepository,
                activeWorkspaceRegistry,
                closeWorkspaceSecuritySession);

        var workspacePresentationManager =
            new Mock<IWorkspacePresentationManager>();

        workspacePresentationManager
            .Setup(x => x.GetOrCreate(
                workspaceId,
                WorkspaceType.Persistent,
                workspace.Name,
                workspace.Description,
                0L))
            .Returns(presentation);

        var view =
            new Mock<IMainFormView>();

        view
            .SetupGet(x => x.SelectedWorkspaceId)
            .Returns(workspaceId);

        var logger =
            new RecordingLogger<MainFormPresenter>();

        _ = CreatePresenter(
            view,
            new Mock<IDocumentSearchStore>(),
            workspacePresentationManager: workspacePresentationManager,
            workspaceRepository: workspaceRepository,
            activeWorkspaceRegistry: activeWorkspaceRegistry,
            applicationSecuritySession: closeWorkspaceSecuritySession,
            logger: logger);

        // Open the workspace so the presenter subscribes to its close event.
        view.Raise(
            x => x.WorkspaceOpenRequested += null,
            EventArgs.Empty);

        await WaitForBackgroundOperationAsync();

        // Raise the actual WorkspacePresentation close event.
        workspaceView.Raise(
            x => x.CloseWorkspaceRequested += null,
            EventArgs.Empty);

        await WaitForBackgroundOperationAsync();

        activeWorkspaceRegistry.Verify(
            x => x.Remove(workspaceId),
            Times.Once);

        view.Verify(
            x => x.SetStatus(
                UiMessages.UnableToCloseWorkspace),
            Times.Once);

        view.Verify(
            x => x.ShowError(
                UiMessages.UnableToCloseWorkspace,
                UiMessages.CloseWorkspace),
            Times.Once);

        view.Verify(
            x => x.ShowError(
                It.Is<string>(
                    message => sensitiveMarkers.Any(
                        marker => message.Contains(
                            marker,
                            StringComparison.Ordinal))),
                It.IsAny<string>()),
            Times.Never);

        RecordedLogEntry entry =
            Assert.Single(
                logger.Entries,
                item => item.Level == LogLevel.Error);

        Assert.Contains(
            LogMessages.WorkspaceCloseUnexpectedFailure,
            entry.Message);

        Assert.Contains(
            $"OperationCode={DiagnosticOperationCodes.WorkspaceCloseFailure}",
            entry.Message);

        Assert.Contains(
            $"FailureType={nameof(InvalidOperationException)}",
            entry.Message);

        Assert.DoesNotContain(
            workspaceId.ToString(),
            entry.Message);

        Assert.Null(entry.Exception);

        foreach (string marker in sensitiveMarkers)
        {
            Assert.DoesNotContain(marker, entry.Message);

            foreach (object? value in entry.Properties.Values)
            {
                Assert.DoesNotContain(
                    marker,
                    value?.ToString() ?? string.Empty);
            }
        }

        foreach (object? value in entry.Properties.Values)
        {
            Assert.DoesNotContain(
                workspaceId.ToString(),
                value?.ToString() ?? string.Empty);
        }

        presentation.Dispose();
    }

    private static WorkspacePresentation CreateWorkspacePresentationForCloseTesting(
        Guid workspaceId,
        string? workspaceName,
        string? description,
        Mock<IWorkspacePresentationView> view,
        Mock<IWorkspaceRepository> workspaceRepository,
        Mock<IActiveWorkspaceRegistry> activeWorkspaceRegistry,
        Mock<IApplicationSecuritySession> workspaceSecuritySession)
    {
        var dialogService =
            new Mock<IWorkspaceDialogService>();

        var documentPresentationFactory =
            new Mock<IWorkspaceDocumentPresentationFactory>();

        var documentRepository =
            new Mock<IDocumentRepository>();

        documentRepository
            .Setup(x => x.GetAllAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var listDocumentsHandler =
            new ListDocumentsHandler(
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

        workspaceSecuritySession
            .SetupGet(x => x.CurrentSnapshot)
            .Returns(
                new ApplicationSecuritySessionSnapshot(
                    ApplicationSecuritySessionState.Authenticated,
                    0));

        return new WorkspacePresentation(
            workspaceId,
            WorkspaceType.Persistent,
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
            view.Object,
            NullLogger<WorkspacePresentation>.Instance,
            workspaceSecuritySession.Object,
            0L);
    }

    [Fact]
    public async Task RemoveRequested_WhenHandlerThrows_LogsSafeDiagnosticAndShowsGenericError()
    {
        Guid documentId = Guid.NewGuid();

        string[] sensitiveMarkers =
        [
            "DOCUMENT-CONTENT-SENTINEL-232",
            "ENCRYPTION-KEY-SENTINEL-232",
            @"C:\Sensitive\Vault\RemoveReport-232.pdf",
            "EXCEPTION-DETAIL-SENTINEL-232"
        ];

        string errorMessage =
            string.Join("|", sensitiveMarkers);

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetByIdAsync(
                documentId,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new InvalidOperationException(errorMessage));

        var view =
            new Mock<IMainFormView>();

        view
            .SetupGet(x => x.SelectedDocumentId)
            .Returns(documentId);

        view
            .SetupGet(x => x.SelectedDocumentFileName)
            .Returns("RemoveReport-232.pdf");

        view
            .Setup(x => x.ConfirmRemoval(
                It.IsAny<string>()))
            .Returns(true);

        var logger =
            new RecordingLogger<MainFormPresenter>();

        _ = CreatePresenter(
            view,
            new Mock<IDocumentSearchStore>(),
            repository: repository,
            logger: logger);

        view.Raise(
            x => x.RemoveRequested += null,
            EventArgs.Empty);

        await WaitForBackgroundOperationAsync();

        repository.Verify(
            x => x.GetByIdAsync(
                documentId,
                It.IsAny<CancellationToken>()),
            Times.Once);

        view.Verify(
            x => x.ConfirmRemoval("RemoveReport-232.pdf"),
            Times.Once);

        view.Verify(
            x => x.SetStatus(
                UiMessages.UnableToRemoveDocumentStatus),
            Times.Once);

        view.Verify(
            x => x.ShowError(
                UiMessages.UnableToRemoveDocument,
                UiMessages.DeskVaultTitle),
            Times.Once);

        view.Verify(
            x => x.ShowError(
                It.Is<string>(
                    message => sensitiveMarkers.Any(
                        marker => message.Contains(
                            marker,
                            StringComparison.Ordinal))),
                It.IsAny<string>()),
            Times.Never);

        view.Verify(
            x => x.SetImportEnabled(true),
            Times.Once);

        RecordedLogEntry entry =
            Assert.Single(
                logger.Entries,
                item => item.Level == LogLevel.Error);

        Assert.Contains(
            LogMessages.DocumentRemovalFailed,
            entry.Message);

        Assert.Contains(
            $"OperationCode={DiagnosticOperationCodes.DocumentRemovalUnexpectedFailure}",
            entry.Message);

        string failureType =
            Assert.IsType<string>(
                entry.Properties["FailureType"]);

        Assert.False(
            string.IsNullOrWhiteSpace(failureType));

        Assert.Contains(
            $"FailureType={failureType}",
            entry.Message);

        Assert.DoesNotContain(
            documentId.ToString(),
            entry.Message);

        Assert.Null(entry.Exception);

        foreach (string marker in sensitiveMarkers)
        {
            Assert.DoesNotContain(marker, entry.Message);

            foreach (object? value in entry.Properties.Values)
            {
                Assert.DoesNotContain(
                    marker,
                    value?.ToString() ?? string.Empty);
            }
        }

        foreach (object? value in entry.Properties.Values)
        {
            Assert.DoesNotContain(
                documentId.ToString(),
                value?.ToString() ?? string.Empty);
        }
    }

    [Fact]
    public async Task RemoveRequested_WhenSessionLocksDuringConfirmation_DoesNotInvokeRemovalHandler()
    {
        Guid documentId = Guid.NewGuid();

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetByIdAsync(
                documentId,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new InvalidOperationException(
                    "Removal handler must not run after session lock."));

        var securitySession =
            new Mock<IApplicationSecuritySession>();

        ApplicationSecuritySessionSnapshot currentSnapshot =
            new(
                ApplicationSecuritySessionState.Authenticated,
                0);

        securitySession
            .SetupGet(x => x.CurrentSnapshot)
            .Returns(() => currentSnapshot);

        var view =
            new Mock<IMainFormView>();

        view
            .SetupGet(x => x.SelectedDocumentId)
            .Returns(documentId);

        view
            .SetupGet(x => x.SelectedDocumentFileName)
            .Returns("security-policy.md");

        view
            .Setup(x => x.ConfirmRemoval("security-policy.md"))
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
            .Returns(true);

        using MainFormPresenter presenter =
            CreatePresenter(
                view,
                new Mock<IDocumentSearchStore>(),
                repository: repository,
                applicationSecuritySession: securitySession);

        view.Raise(
            x => x.RemoveRequested += null,
            EventArgs.Empty);

        await WaitForBackgroundOperationAsync();

        view.Verify(
            x => x.ConfirmRemoval("security-policy.md"),
            Times.Once);

        repository.Verify(
            x => x.GetByIdAsync(
                documentId,
                It.IsAny<CancellationToken>()),
            Times.Never);

        view.Verify(
            x => x.ShowLockedState(),
            Times.Once);

        view.Verify(
            x => x.ShowError(
                UiMessages.UnableToRemoveDocument,
                UiMessages.DeskVaultTitle),
            Times.Never);
    }

    [Fact]
    public async Task WorkspaceRemoveRequested_WhenDeletionThrows_LogsSafeDiagnosticAndShowsGenericError()
    {
        Guid workspaceId = Guid.NewGuid();

        string[] sensitiveMarkers =
        [
            "WORKSPACE-NAME-SENTINEL-232",
            "WORKSPACE-DESCRIPTION-SENTINEL-232",
            @"C:\Sensitive\Workspace\workspace.db",
            "EXCEPTION-DETAIL-SENTINEL-232"
        ];

        string exceptionMessage =
            string.Join("|", sensitiveMarkers);

        Workspace workspace =
            Workspace.CreatePersistent(
                workspaceId,
                sensitiveMarkers[0],
                sensitiveMarkers[1]);

        var activeWorkspaceRegistry =
            new Mock<IActiveWorkspaceRegistry>();

        activeWorkspaceRegistry
            .Setup(x => x.Get(workspaceId))
            .Returns(workspace);

        var workspaceRepository =
            new Mock<IWorkspaceRepository>();

        workspaceRepository
            .Setup(x => x.DeleteAsync(
                workspaceId,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new InvalidOperationException(exceptionMessage));

        var view =
            new Mock<IMainFormView>();

        view
            .SetupGet(x => x.SelectedWorkspaceId)
            .Returns(workspaceId);

        view
            .SetupGet(x => x.SelectedWorkspaceName)
            .Returns(sensitiveMarkers[0]);

        view
            .Setup(x => x.ConfirmWorkspaceRemoval(
                sensitiveMarkers[0]))
            .Returns(true);

        var logger =
            new RecordingLogger<MainFormPresenter>();

        _ = CreatePresenter(
            view,
            new Mock<IDocumentSearchStore>(),
            workspaceRepository: workspaceRepository,
            activeWorkspaceRegistry: activeWorkspaceRegistry,
            logger: logger);

        view.Raise(
            x => x.WorkspaceRemoveRequested += null,
            EventArgs.Empty);

        await WaitForBackgroundOperationAsync();

        workspaceRepository.Verify(
            x => x.DeleteAsync(
                workspaceId,
                It.IsAny<CancellationToken>()),
            Times.Once);

        view.Verify(
            x => x.SetStatus(
                UiMessages.UnableToRemoveWorkspace),
            Times.Once);

        view.Verify(
            x => x.ShowError(
                UiMessages.UnableToRemoveWorkspace,
                UiMessages.DeleteWorkspace),
            Times.Once);

        view.Verify(
            x => x.ShowError(
                It.Is<string>(
                    message => sensitiveMarkers.Any(
                        marker => message.Contains(
                            marker,
                            StringComparison.Ordinal))),
                It.IsAny<string>()),
            Times.Never);

        RecordedLogEntry entry =
            Assert.Single(
                logger.Entries,
                item => item.Level == LogLevel.Error);

        Assert.Contains(
            LogMessages.WorkspaceRemovalUnexpectedFailure,
            entry.Message);

        Assert.Contains(
            $"OperationCode={DiagnosticOperationCodes.WorkspaceRemovalFailure}",
            entry.Message);

        Assert.Contains(
            $"FailureType={nameof(InvalidOperationException)}",
            entry.Message);

        Assert.DoesNotContain(
            workspaceId.ToString(),
            entry.Message);

        Assert.Null(entry.Exception);

        foreach (string marker in sensitiveMarkers)
        {
            Assert.DoesNotContain(marker, entry.Message);

            foreach (object? value in entry.Properties.Values)
            {
                Assert.DoesNotContain(
                    marker,
                    value?.ToString() ?? string.Empty);
            }
        }

        foreach (object? value in entry.Properties.Values)
        {
            Assert.DoesNotContain(
                workspaceId.ToString(),
                value?.ToString() ?? string.Empty);
        }
    }

    [Fact]
    public async Task WorkspaceRemoveRequested_WhenSessionLocksDuringConfirmation_DoesNotDeleteWorkspace()
    {
        Guid workspaceId = Guid.NewGuid();

        Workspace workspace =
            Workspace.CreatePersistent(
                workspaceId,
                "Workspace");

        var workspaceRepository =
            new Mock<IWorkspaceRepository>();

        workspaceRepository
            .Setup(x => x.DeleteAsync(
                workspaceId,
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

        var view =
            new Mock<IMainFormView>();

        view
            .SetupGet(x => x.SelectedWorkspaceId)
            .Returns(workspaceId);

        view
            .SetupGet(x => x.SelectedWorkspaceName)
            .Returns("Workspace");

        view
            .Setup(x => x.ConfirmWorkspaceRemoval("Workspace"))
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
            .Returns(true);

        var workspacePresentationManager =
            new Mock<IWorkspacePresentationManager>();

        using MainFormPresenter presenter =
            CreatePresenter(
                view,
                new Mock<IDocumentSearchStore>(),
                workspacePresentationManager: workspacePresentationManager,
                workspaceRepository: workspaceRepository,
                activeWorkspaceRegistry: activeWorkspaceRegistry,
                applicationSecuritySession: securitySession);

        view.Raise(
            x => x.WorkspaceRemoveRequested += null,
            EventArgs.Empty);

        await WaitForBackgroundOperationAsync();

        view.Verify(
            x => x.ConfirmWorkspaceRemoval("Workspace"),
            Times.Once);

        workspaceRepository.Verify(
            x => x.DeleteAsync(
                workspaceId,
                It.IsAny<CancellationToken>()),
            Times.Never);

        workspacePresentationManager.Verify(
            x => x.Close(workspaceId),
            Times.Never);
    }

    [Fact]
    public async Task WorkspaceOpenRequested_WhenSessionLocksDuringWorkspaceLookup_DoesNotCreatePresentation()
    {
        Guid workspaceId = Guid.NewGuid();

        Workspace workspace =
            Workspace.CreatePersistent(
                workspaceId,
                "Confidential Workspace",
                "Sensitive workspace description");

        var workspaceRepository =
            new Mock<IWorkspaceRepository>();

        var workspaceLookupStarted =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var workspaceLookupCompletion =
            new TaskCompletionSource<Workspace?>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        workspaceRepository
            .Setup(x => x.GetByIdAsync(
                workspaceId,
                It.IsAny<CancellationToken>()))
            .Callback<Guid, CancellationToken>(
                (_, _) => workspaceLookupStarted.TrySetResult())
            .Returns(() => workspaceLookupCompletion.Task);

        var activeWorkspaceRegistry =
            new Mock<IActiveWorkspaceRegistry>();

        activeWorkspaceRegistry
            .Setup(x => x.Get(workspaceId))
            .Returns((Workspace?)null);

        var workspacePresentationManager =
            new Mock<IWorkspacePresentationManager>();

        var view =
            new Mock<IMainFormView>();

        view
            .SetupGet(x => x.SelectedWorkspaceId)
            .Returns(workspaceId);

        var securitySession =
            new Mock<IApplicationSecuritySession>();

        ApplicationSecuritySessionSnapshot currentSnapshot =
            new(
                ApplicationSecuritySessionState.Authenticated,
                0);

        securitySession
            .SetupGet(x => x.CurrentSnapshot)
            .Returns(() => currentSnapshot);

        using MainFormPresenter presenter =
            CreatePresenter(
                view,
                new Mock<IDocumentSearchStore>(),
                workspacePresentationManager: workspacePresentationManager,
                workspaceRepository: workspaceRepository,
                activeWorkspaceRegistry: activeWorkspaceRegistry,
                applicationSecuritySession: securitySession);

        // Arrange: start workspace opening and pause its repository lookup.
        view.Raise(
            x => x.WorkspaceOpenRequested += null,
            EventArgs.Empty);

        await workspaceLookupStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        // Act: lock the session before the workspace lookup completes.
        currentSnapshot =
            new ApplicationSecuritySessionSnapshot(
                ApplicationSecuritySessionState.Locked,
                1);

        securitySession.Raise(
            x => x.StateChanged += null,
            currentSnapshot);

        // Complete the lookup with authenticated-era workspace metadata.
        workspaceLookupCompletion.SetResult(workspace);

        await WaitForBackgroundOperationAsync();

        // Assert: the old operation must not create a workspace presentation.
        workspacePresentationManager.Verify(
            x => x.GetOrCreate(
                workspaceId,
                WorkspaceType.Persistent,
                workspace.Name,
                workspace.Description,
                0L),
            Times.Never);

        view.Verify(
            x => x.SetStatus(
                "The workspace was opened."),
            Times.Never);

        view.Verify(
            x => x.ShowWarning(
                It.IsAny<string>(),
                UiMessages.OpenWorkspaceTitle),
            Times.Never);

        view.Verify(
            x => x.ShowError(
                It.IsAny<string>(),
                UiMessages.OpenWorkspaceTitle),
            Times.Never);

        view.Verify(
            x => x.ShowLockedState(),
            Times.AtLeastOnce);

        workspacePresentationManager.Verify(
            x => x.CloseAll(),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task WorkspaceOpenRequested_WhenOpeningThrows_LogsSafeDiagnosticAndShowsGenericError()
    {
        Guid workspaceId = Guid.NewGuid();

        string[] sensitiveMarkers =
        [
            "WORKSPACE-NAME-SENTINEL-232",
            "WORKSPACE-DESCRIPTION-SENTINEL-232",
            @"C:\Sensitive\Workspace\workspace.db",
            "EXCEPTION-DETAIL-SENTINEL-232"
        ];

        string exceptionMessage =
            string.Join("|", sensitiveMarkers);

        var workspaceRepository =
            new Mock<IWorkspaceRepository>();

        workspaceRepository
            .Setup(x => x.GetByIdAsync(
                workspaceId,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new InvalidOperationException(exceptionMessage));

        var activeWorkspaceRegistry =
            new Mock<IActiveWorkspaceRegistry>();

        activeWorkspaceRegistry
            .Setup(x => x.Get(workspaceId))
            .Returns((Workspace?)null);

        var view =
            new Mock<IMainFormView>();

        view
            .SetupGet(x => x.SelectedWorkspaceId)
            .Returns(workspaceId);

        var logger =
            new RecordingLogger<MainFormPresenter>();

        _ = CreatePresenter(
            view,
            new Mock<IDocumentSearchStore>(),
            workspaceRepository: workspaceRepository,
            activeWorkspaceRegistry: activeWorkspaceRegistry,
            logger: logger);

        view.Raise(
            x => x.WorkspaceOpenRequested += null,
            EventArgs.Empty);

        await WaitForBackgroundOperationAsync();

        workspaceRepository.Verify(
            x => x.GetByIdAsync(
                workspaceId,
                It.IsAny<CancellationToken>()),
            Times.Once);

        view.Verify(
            x => x.SetStatus(
                UiMessages.UnableToOpenWorkspace),
            Times.Once);

        view.Verify(
            x => x.ShowError(
                UiMessages.UnableToOpenWorkspace,
                UiMessages.OpenWorkspaceTitle),
            Times.Once);

        view.Verify(
            x => x.ShowError(
                It.Is<string>(
                    message => sensitiveMarkers.Any(
                        marker => message.Contains(
                            marker,
                            StringComparison.Ordinal))),
                It.IsAny<string>()),
            Times.Never);

        RecordedLogEntry entry =
            Assert.Single(
                logger.Entries,
                item => item.Level == LogLevel.Error);

        Assert.Contains(
            LogMessages.WorkspaceOpenUnexpectedFailure,
            entry.Message);

        Assert.Contains(
            $"OperationCode={DiagnosticOperationCodes.WorkspaceOpenFailure}",
            entry.Message);

        Assert.Contains(
            $"FailureType={nameof(InvalidOperationException)}",
            entry.Message);

        Assert.DoesNotContain(
            workspaceId.ToString(),
            entry.Message);

        Assert.Null(entry.Exception);

        foreach (string marker in sensitiveMarkers)
        {
            Assert.DoesNotContain(marker, entry.Message);

            foreach (object? value in entry.Properties.Values)
            {
                Assert.DoesNotContain(
                    marker,
                    value?.ToString() ?? string.Empty);
            }
        }

        foreach (object? value in entry.Properties.Values)
        {
            Assert.DoesNotContain(
                workspaceId.ToString(),
                value?.ToString() ?? string.Empty);
        }
    }

    [Fact]
    public async Task InitializeAsync_WhenDocumentRepositoryThrows_LogsSafeDiagnosticAndShowsGenericError()
    {
        string[] sensitiveMarkers =
        [
            "DOCUMENT-CONTENT-SENTINEL-232",
            "ENCRYPTION-KEY-SENTINEL-232",
            @"C:\Sensitive\Vault\InitializeReport-232.pdf",
            "EXCEPTION-DETAIL-SENTINEL-232"
        ];

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetAllAsync(
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new InvalidOperationException(
                    string.Join("|", sensitiveMarkers)));

        var view =
            new Mock<IMainFormView>();

        var logger =
            new RecordingLogger<MainFormPresenter>();

        MainFormPresenter presenter =
            CreatePresenter(
                view,
                new Mock<IDocumentSearchStore>(),
                repository: repository,
                logger: logger);

        await presenter.InitializeAsync();

        repository.Verify(
            x => x.GetAllAsync(
                It.IsAny<CancellationToken>()),
            Times.Once);

        view.Verify(
            x => x.SetStatus(
                UiMessages.UnableToLoadDocumentsStatus),
            Times.Once);

        view.Verify(
            x => x.ShowError(
                UiMessages.UnableToLoadDocuments,
                UiMessages.DeskVaultTitle),
            Times.Once);

        view.Verify(
            x => x.ShowError(
                It.Is<string>(
                    message => sensitiveMarkers.Any(
                        marker => message.Contains(
                            marker,
                            StringComparison.Ordinal))),
                It.IsAny<string>()),
            Times.Never);

        RecordedLogEntry entry =
            Assert.Single(
                logger.Entries,
                item => item.Level == LogLevel.Error);

        Assert.Contains(
            LogMessages.MainWorkspaceInitializationFailed,
            entry.Message);

        Assert.Contains(
            $"OperationCode={DiagnosticOperationCodes.MainWorkspaceInitializationFailure}",
            entry.Message);

        Assert.Contains(
            $"FailureType={nameof(InvalidOperationException)}",
            entry.Message);

        Assert.Null(entry.Exception);

        foreach (string marker in sensitiveMarkers)
        {
            Assert.DoesNotContain(marker, entry.Message);

            foreach (object? value in entry.Properties.Values)
            {
                Assert.DoesNotContain(
                    marker,
                    value?.ToString() ?? string.Empty);
            }
        }
    }

    [Fact]
    public async Task LockNotification_WhenSearchCancellationCallbackThrows_StillPostsLockedStateForUiApplication()
    {
        var searchStore =
            new Mock<IDocumentSearchStore>();

        var searchStarted =
            new TaskCompletionSource<CancellationToken>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var searchCompletion =
            new TaskCompletionSource<IReadOnlyList<SearchDocumentsResult>>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        CancellationTokenRegistration cancellationRegistration = default;

        searchStore
            .Setup(x => x.SearchAsync(
                It.IsAny<SearchDocumentsQuery>(),
                It.IsAny<SearchDocumentsRankingKey?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Returns((
                SearchDocumentsQuery _,
                SearchDocumentsRankingKey? _,
                int _,
                CancellationToken cancellationToken) =>
            {
                cancellationRegistration =
                    cancellationToken.Register(
                        static () => throw new InvalidOperationException(
                            "Search cancellation callback failed."));

                searchStarted.TrySetResult(cancellationToken);

                return searchCompletion.Task;
            });

        var view =
            new Mock<IMainFormView>();

        view
            .SetupGet(x => x.SearchText)
            .Returns("security");

        var securitySession =
            new Mock<IApplicationSecuritySession>();

        ApplicationSecuritySessionSnapshot currentSnapshot =
            new(
                ApplicationSecuritySessionState.Authenticated,
                0);

        securitySession
            .SetupGet(x => x.CurrentSnapshot)
            .Returns(() => currentSnapshot);

        var workspacePresentationManager =
            new Mock<IWorkspacePresentationManager>();

        var postedActions =
            new List<Action>();

        using MainFormPresenter presenter =
            CreatePresenter(
                view,
                searchStore,
                workspacePresentationManager: workspacePresentationManager,
                applicationSecuritySession: securitySession,
                postToUi: postedActions.Add);

        // Arrange: start a search whose cancellation callback will throw.
        view.Raise(
            x => x.SearchRequested += null,
            EventArgs.Empty);

        CancellationToken searchToken =
            await searchStarted.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);

        // Act: lock the authoritative session.
        currentSnapshot =
            new ApplicationSecuritySessionSnapshot(
                ApplicationSecuritySessionState.Locked,
                1);

        Exception? notificationException =
            Record.Exception(
                () => securitySession.Raise(
                    x => x.StateChanged += null,
                    currentSnapshot));

        try
        {
            // Revocation must be processed even when search cancellation callbacks fail.
            Assert.Null(notificationException);
            Assert.True(searchToken.IsCancellationRequested);

            Action postedAction =
                Assert.Single(postedActions);

            postedAction();

            view.Verify(
                x => x.ShowLockedState(),
                Times.Once);

            workspacePresentationManager.Verify(
                x => x.CloseAll(),
                Times.Once);

            searchCompletion.SetResult(
                CreateSearchResults(
                    Guid.NewGuid(),
                    Guid.NewGuid()));

            await WaitForBackgroundOperationAsync();

            view.Verify(
                x => x.ShowSearchResults(
                    It.IsAny<IReadOnlyList<SearchResultListItem>>()),
                Times.Never);
        }
        finally
        {
            cancellationRegistration.Dispose();

            searchCompletion.TrySetResult([]);

            presenter.Dispose();
        }
    }

    [Fact]
    public void LockNotification_WhenWorkspaceDisposalThrows_StillCompletesProtectedUiCleanup()
    {
        var view =
            new Mock<IMainFormView>();

        var searchStore =
            new Mock<IDocumentSearchStore>();

        var securitySession =
            new Mock<IApplicationSecuritySession>();

        ApplicationSecuritySessionSnapshot currentSnapshot =
            new(
                ApplicationSecuritySessionState.Authenticated,
                0);

        securitySession
            .SetupGet(x => x.CurrentSnapshot)
            .Returns(() => currentSnapshot);

        var workspacePresentationManager =
            new Mock<IWorkspacePresentationManager>();

        int closeAllInvocationCount = 0;

        workspacePresentationManager
            .Setup(x => x.CloseAll())
            .Callback(() =>
            {
                if (Interlocked.Increment(ref closeAllInvocationCount) == 1)
                {
                    throw new InvalidOperationException(
                        "Workspace presentation disposal failed.");
                }
            });

        var postedActions =
            new List<Action>();

        using MainFormPresenter presenter =
            CreatePresenter(
                view,
                searchStore,
                workspacePresentationManager: workspacePresentationManager,
                applicationSecuritySession: securitySession,
                postToUi: postedActions.Add);

        // Arrange: the authoritative session transitions to Locked.
        currentSnapshot =
            new ApplicationSecuritySessionSnapshot(
                ApplicationSecuritySessionState.Locked,
                1);

        securitySession.Raise(
            x => x.StateChanged += null,
            currentSnapshot);

        Action postedAction =
            Assert.Single(postedActions);

        // Act: apply the lock notification on the UI thread.
        Exception? cleanupException =
            Record.Exception(postedAction);

        // Assert: disposal failure must not escape the lock-cleanup callback.
        Assert.Null(cleanupException);

        view.Verify(
            x => x.ShowLockedState(),
            Times.Once);

        workspacePresentationManager.Verify(
            x => x.CloseAll(),
            Times.Once);

        // Allow presenter teardown to run independently of the simulated lock-cleanup failure.
        workspacePresentationManager
            .Setup(x => x.CloseAll());

        presenter.Dispose();
    }

    [Fact]
    public void WorkspaceDeleteRequested_WhenSenderBelongsToSupersededSession_DoesNotShowConfirmation()
    {
        Guid workspaceId = Guid.NewGuid();

        var session =
            new PresentationSessionRevisionTestSupport.SecuritySessionHarness(
                ApplicationSecuritySessionState.Authenticated,
                revision: 150);

        WorkspacePresentation stalePresentation =
            PresentationSessionRevisionTestSupport.CreateWorkspacePresentation(
                workspaceId,
                session,
                revision: 150);

        var view = new Mock<IMainFormView>();

        using MainFormPresenter presenter =
            CreatePresenter(
                view,
                new Mock<IDocumentSearchStore>(),
                applicationSecuritySession: session.Session);

        // The old presentation still exists, but a newer authenticated session
        // is already authoritative.
        session.Set(
            ApplicationSecuritySessionState.Authenticated,
            revision: 152);

        MethodInfo handler =
            typeof(MainFormPresenter).GetMethod(
                "OnWorkspaceDeleteRequested",
                BindingFlags.Instance | BindingFlags.NonPublic)!;

        handler.Invoke(
            presenter,
            new object?[]
            {
            stalePresentation,
            EventArgs.Empty
            });

        view.Verify(
            x => x.ConfirmWorkspaceRemoval(
                It.IsAny<string>(),
                It.IsAny<long>()),
            Times.Never);

        stalePresentation.Dispose();
    }

    [Fact]
    public void WorkspaceCloseRequested_WhenSenderBelongsToSupersededSession_DoesNotStartClose()
    {
        Guid workspaceId = Guid.NewGuid();

        var session =
            new PresentationSessionRevisionTestSupport.SecuritySessionHarness(
                ApplicationSecuritySessionState.Authenticated,
                revision: 160);

        WorkspacePresentation stalePresentation =
            PresentationSessionRevisionTestSupport.CreateWorkspacePresentation(
                workspaceId,
                session,
                revision: 160);

        var view = new Mock<IMainFormView>();
        var activeWorkspaceRegistry =
            new Mock<IActiveWorkspaceRegistry>();

        using MainFormPresenter presenter =
            CreatePresenter(
                view,
                new Mock<IDocumentSearchStore>(),
                activeWorkspaceRegistry: activeWorkspaceRegistry,
                applicationSecuritySession: session.Session);

        session.Set(
            ApplicationSecuritySessionState.Authenticated,
            revision: 162);

        MethodInfo handler =
            typeof(MainFormPresenter).GetMethod(
                "OnWorkspaceCloseRequested",
                BindingFlags.Instance | BindingFlags.NonPublic)!;

        handler.Invoke(
            presenter,
            new object?[]
            {
            stalePresentation,
            EventArgs.Empty
            });

        activeWorkspaceRegistry.Verify(
            x => x.Remove(workspaceId),
            Times.Never);

        view.Verify(
            x => x.ShowWarning(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<long>()),
            Times.Never);

        view.Verify(
            x => x.ShowError(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<long>()),
            Times.Never);

        stalePresentation.Dispose();
    }

    private static void AssertSafeSearchFailureLog(
        RecordingLogger<MainFormPresenter> logger,
        string operationCode,
        IReadOnlyCollection<string> sensitiveMarkers)
    {
        RecordedLogEntry entry =
            Assert.Single(
                logger.Entries,
                item => item.Level == LogLevel.Error);

        Assert.Contains(
            LogMessages.DocumentSearchFailed,
            entry.Message);

        Assert.Contains(
            $"OperationCode={operationCode}",
            entry.Message);

        Assert.Contains(
            $"FailureType={nameof(InvalidOperationException)}",
            entry.Message);

        Assert.Null(entry.Exception);

        foreach (string marker in sensitiveMarkers)
        {
            Assert.DoesNotContain(marker, entry.Message);

            foreach (object? value in entry.Properties.Values)
            {
                Assert.DoesNotContain(
                    marker,
                    value?.ToString() ?? string.Empty);
            }
        }
    }

    private static IReadOnlyList<SearchDocumentsResult> CreateSearchResults(
        Guid firstDocumentId,
        Guid secondDocumentId)
    {
        return
        [
            new SearchDocumentsResult(
                firstDocumentId,
                "security-policy.md",
                "Security Policy",
                [
                    new SearchMatch(
                        SearchMatchSource.ProcessedContent,
                        SearchMatchKind.Exact,
                        "Security policy introduction."),

                    new SearchMatch(
                        SearchMatchSource.ProcessedContent,
                        SearchMatchKind.Exact,
                        "Security retention requirements.")
                ],
                2),

            new SearchDocumentsResult(
                secondDocumentId,
                "incident-response.md",
                "Incident Response",
                [
                    new SearchMatch(
                        SearchMatchSource.ProcessedContent,
                        SearchMatchKind.Exact,
                        "Security incident response procedure.")
                ],
                1)
        ];
    }

    private static IReadOnlyList<SearchDocumentsResult>
        CreateSearchResultsForBatching(
            int count)
    {
        return Enumerable
            .Range(1, count)
            .Select(index =>
                CreateResult(
                    $"Search Result {index:D2}"))
            .ToList();
    }

    private static SearchDocumentsResult CreateResult(
        string displayName)
    {
        return new SearchDocumentsResult(
            Guid.NewGuid(),
            $"{displayName.ToLowerInvariant().Replace(' ', '-')}.md",
            displayName,
            [
                new SearchMatch(
                    SearchMatchSource.ProcessedContent,
                    SearchMatchKind.Exact,
                    $"{displayName} matching content.")
            ],
            1);
    }

    private static Mock<IImportDocumentValidator>
        CreateSuccessfulImportValidator()
    {
        var validator =
            new Mock<IImportDocumentValidator>();

        validator
            .Setup(x => x.Validate(
                It.IsAny<ImportDocumentCommand>()))
            .Returns(
                new ImportDocumentResult(
                    ImportDocumentResultStatus.Success,
                    null,
                    "Validation successful."));

        return validator;
    }

    private static DocumentTextExtractorResolver
        CreateDocumentTextExtractorResolver()
    {
        var extractor =
            new Mock<IDocumentTextExtractor>();

        extractor
            .Setup(x => x.CanExtract(
                It.IsAny<string>()))
            .Returns(
                (string fileName) =>
                    fileName.EndsWith(
                        ".txt",
                        StringComparison.OrdinalIgnoreCase) ||
                    fileName.EndsWith(
                        ".md",
                        StringComparison.OrdinalIgnoreCase) ||
                    fileName.EndsWith(
                        ".csv",
                        StringComparison.OrdinalIgnoreCase));

        return new DocumentTextExtractorResolver(
            [extractor.Object]);
    }

    private static MainFormPresenter CreatePresenter(
        Mock<IMainFormView> view,
        Mock<IDocumentSearchStore> searchStore,
        Mock<IDocumentRepository>? repository = null,
        Mock<IDocumentReader>? documentReader = null,
        Mock<IHashService>? hashService = null,
        Mock<IImportDocumentValidator>? importValidator = null,
        Mock<IStorageService>? storageService = null,
        Mock<IDocumentProcessingService>? processingService = null,
        DocumentTextExtractorResolver? documentTextExtractorResolver = null,
        Mock<IWorkspacePresentationManager>? workspacePresentationManager = null,
        Mock<IWorkspaceRepository>? workspaceRepository = null,
        Mock<IActiveWorkspaceRegistry>? activeWorkspaceRegistry = null,
        ILogger<MainFormPresenter>? logger = null,
        Mock<IApplicationSecuritySession>? applicationSecuritySession = null,
        Action<Action>? postToUi = null)
    {
        repository ??=
            new Mock<IDocumentRepository>();

        storageService ??=
            new Mock<IStorageService>();

        hashService ??=
            new Mock<IHashService>();

        importValidator ??=
            CreateSuccessfulImportValidator();

        documentReader ??=
            new Mock<IDocumentReader>();

        processingService ??=
            new Mock<IDocumentProcessingService>();

        documentTextExtractorResolver ??=
            CreateDocumentTextExtractorResolver();

        workspacePresentationManager ??=
            new Mock<IWorkspacePresentationManager>();

        workspaceRepository ??=
            new Mock<IWorkspaceRepository>();

        activeWorkspaceRegistry ??=
            new Mock<IActiveWorkspaceRegistry>();

        var importDocumentHandler =
            new ImportDocumentHandler(
                importValidator.Object,
                hashService.Object,
                storageService.Object,
                repository.Object,
                NullLogger<ImportDocumentHandler>.Instance);

        var removeDocumentHandler =
            new RemoveDocumentHandler(
                repository.Object,
                workspaceRepository.Object,
                storageService.Object,
                NullLogger<RemoveDocumentHandler>.Instance);

        var listDocumentsHandler =
            new ListDocumentsHandler(
                repository.Object,
                NullLogger<ListDocumentsHandler>.Instance);

        var searchDocumentsRetriever =
            new SearchDocumentsRetriever(
                searchStore.Object,
                new SearchDocumentsRanker(),
                new SearchDocumentsContinuationCodec(),
                NullLogger<SearchDocumentsRetriever>.Instance);

        var searchDocumentsHandler =
            new SearchDocumentsHandler(
                searchDocumentsRetriever);

        var createWorkspaceHandler =
            new CreateWorkspaceHandler(
                repository.Object,
                workspaceRepository.Object,
                activeWorkspaceRegistry.Object,
                NullLogger<CreateWorkspaceHandler>.Instance);

        var deleteWorkspaceHandler =
            new DeleteWorkspaceHandler(
                workspaceRepository.Object,
                activeWorkspaceRegistry.Object,
                NullLogger<DeleteWorkspaceHandler>.Instance);

        var getWorkspacesHandler =
            new GetWorkspacesHandler(
                workspaceRepository.Object);

        var openWorkspaceHandler =
            new OpenWorkspaceHandler(
                repository.Object,
                workspaceRepository.Object,
                activeWorkspaceRegistry.Object,
                NullLogger<OpenWorkspaceHandler>.Instance);

        var closeWorkspaceHandler =
            new CloseWorkspaceHandler(
                activeWorkspaceRegistry.Object,
                NullLogger<CloseWorkspaceHandler>.Instance);

        var searchOptions =
            Options.Create(
                new SearchOptions
                {
                    PageSize = SearchOptions.DefaultPageSize
                });

        if (applicationSecuritySession is null)
        {
            applicationSecuritySession =
                new Mock<IApplicationSecuritySession>();

            applicationSecuritySession
                .SetupGet(x => x.CurrentSnapshot)
                .Returns(
                    new ApplicationSecuritySessionSnapshot(
                        ApplicationSecuritySessionState.Authenticated,
                        0));
        }

        view
            .Setup(x => x.PostToUi(It.IsAny<Action>()))
            .Callback<Action>(action =>
            {
                if (postToUi is null)
                {
                    action();
                }
                else
                {
                    postToUi(action);
                }
            });

        return new MainFormPresenter(
            view.Object,
            importDocumentHandler,
            removeDocumentHandler,
            listDocumentsHandler,
            searchDocumentsHandler,
            createWorkspaceHandler,
            deleteWorkspaceHandler,
            getWorkspacesHandler,
            openWorkspaceHandler,
            closeWorkspaceHandler,
            workspacePresentationManager.Object,
            processingService.Object,
            documentTextExtractorResolver,
            logger ?? NullLogger<MainFormPresenter>.Instance,
            searchOptions,
            applicationSecuritySession.Object);
    }

    private static MemoryStream CreateContentStream(
        string content)
    {
        return new MemoryStream(
            Encoding.UTF8.GetBytes(
                content));
    }

    private static string ReadStream(
        Stream stream)
    {
        stream.Position = 0;

        using var reader =
            new StreamReader(
                stream,
                leaveOpen: true);

        return reader.ReadToEnd();
    }

    private static async Task WaitForBackgroundOperationAsync()
    {
        await Task.Delay(100);
    }

    public static IEnumerable<object[]> ImportableFormatsWithoutExtractors()
    {
        foreach (string extension in SupportedFileTypes.GetAll())
        {
            DocumentFormatCapability capability =
                SupportedFileTypes.GetForExtension(extension)
                ?? throw new InvalidOperationException(
                    $"No capability declaration exists for '{extension}'.");

            if (capability.TextExtraction
                == DocumentTextExtractionKind.None)
            {
                yield return new object[] { extension };
            }
        }
    }
}
