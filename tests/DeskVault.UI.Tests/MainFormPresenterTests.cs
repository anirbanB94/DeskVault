using DeskVault.Application.Documents;
using DeskVault.Application.Documents.Commands.ImportDocument;
using DeskVault.Application.Documents.Commands.RemoveDocument;
using DeskVault.Application.Documents.Extraction;
using DeskVault.Application.Documents.Queries.GetDocument;
using DeskVault.Application.Documents.Queries.ListDocuments;
using DeskVault.Application.Documents.Queries.OpenDocument;
using DeskVault.Application.Documents.Queries.SearchDocuments;
using DeskVault.Application.Interfaces;
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
using DiagnosticOperationCodes = DeskVault.Shared.Resources.DiagnosticOperationCodes;
using DeskVault.UI.Presenters;
using DeskVault.UI.Tests.TestDoubles;
using DeskVault.UI.Resources;
using DeskVault.UI.Services.Interfaces;
using DeskVault.UI.Services.Workspace;
using DeskVault.UI.Views;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Text;

namespace DeskVault.UI.Tests;

public sealed class MainFormPresenterTests
{
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
                NullLogger<OpenDocumentHandler>.Instance);

        var removeDocumentHandler =
            new RemoveDocumentHandler(
                repository.Object,
                workspaceRepository.Object,
                new Mock<IStorageService>().Object,
                NullLogger<RemoveDocumentHandler>.Instance);

        var workspaceId =
            Guid.NewGuid();

        var workspacePresenter =
            new DocumentWorkspacePresenter(
                workspaceId,
                workspaceView.Object,
                documentViewer.Object,
                getDocumentHandler,
                openDocumentHandler,
                removeDocumentHandler,
                NullLogger<DocumentWorkspacePresenter>.Instance);

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
                documentId))
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
                NullLogger<WorkspacePresentation>.Instance);

        var workspacePresentationManager =
            new Mock<IWorkspacePresentationManager>();

        workspacePresentationManager
            .Setup(x => x.GetOrCreate(
                It.IsAny<Guid>(),
                It.IsAny<WorkspaceType>(),
                It.IsAny<string?>(),
                It.IsAny<string?>()))
            .Returns(workspacePresentation);

        _ =
            CreatePresenter(
                view,
                searchStore,
                repository,
                documentReader,
                workspacePresentationManager:
                    workspacePresentationManager);

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
                It.IsAny<string?>()),
            Times.Once);

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
                NullLogger<WorkspacePresentation>.Instance);

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
                    workspacePresentationManager);

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
                    It.IsAny<string?>()),
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

        WorkspacePresentation presentation =
            CreateWorkspacePresentationForCloseTesting(
                workspaceId,
                workspace.Name,
                workspace.Description,
                workspaceView,
                workspaceRepository,
                activeWorkspaceRegistry);

        var workspacePresentationManager =
            new Mock<IWorkspacePresentationManager>();

        workspacePresentationManager
            .Setup(x => x.GetOrCreate(
                workspaceId,
                WorkspaceType.Persistent,
                workspace.Name,
                workspace.Description))
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
        Mock<IActiveWorkspaceRegistry> activeWorkspaceRegistry)
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
            NullLogger<WorkspacePresentation>.Instance);
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
        ILogger<MainFormPresenter>? logger = null)
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
            searchOptions);
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
