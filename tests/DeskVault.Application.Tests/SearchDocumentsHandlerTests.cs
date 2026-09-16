using DeskVault.Application.Documents.Queries.SearchDocuments;
using DeskVault.Application.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DeskVault.Application.Tests;

public sealed class SearchDocumentsHandlerTests
{
    [Fact]
    public async Task HandleAsync_ReturnsRankedSearchResults()
    {
        // Arrange
        IReadOnlyList<SearchDocumentsResult> searchResults =
        [
            CreateResult(
                "Second Document"),
            CreateResult(
                "First Document")
        ];

        IReadOnlyList<SearchDocumentsResult> rankedResults =
        [
            searchResults[1],
            searchResults[0]
        ];

        var store =
            new Mock<IDocumentSearchStore>();

        var ranker =
            new Mock<ISearchDocumentsRanker>();

        SearchDocumentsQuery query =
            new("matching");

        store
            .Setup(x => x.SearchAsync(
                query,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        ranker
            .Setup(x => x.Rank(searchResults))
            .Returns(rankedResults);

        SearchDocumentsHandler handler =
            CreateHandler(
                store,
                ranker);

        // Act
        SearchDocumentsPage page =
            await handler.HandleAsync(query);

        // Assert
        Assert.Equal(
            rankedResults,
            page.Results);

        Assert.False(
            page.HasMore);

        Assert.Null(
            page.Continuation);

        store.Verify(
            x => x.SearchAsync(
                query,
                It.IsAny<CancellationToken>()),
            Times.Once);

        ranker.Verify(
            x => x.Rank(searchResults),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ReturnsBoundedFirstBatch()
    {
        // Arrange
        IReadOnlyList<SearchDocumentsResult> searchResults =
        [
            CreateResult("First Document"),
            CreateResult("Second Document"),
            CreateResult("Third Document")
        ];

        IReadOnlyList<SearchDocumentsResult> rankedResults =
        [
            searchResults[0],
            searchResults[1],
            searchResults[2]
        ];

        var store =
            new Mock<IDocumentSearchStore>();

        var ranker =
            new Mock<ISearchDocumentsRanker>();

        SearchDocumentsQuery query =
            new(
                "matching",
                Limit: 2);

        store
            .Setup(x => x.SearchAsync(
                query,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        ranker
            .Setup(x => x.Rank(searchResults))
            .Returns(rankedResults);

        SearchDocumentsHandler handler =
            CreateHandler(
                store,
                ranker);

        // Act
        SearchDocumentsPage page =
            await handler.HandleAsync(query);

        // Assert
        Assert.Equal(
            rankedResults.Take(2),
            page.Results);

        Assert.True(
            page.HasMore);

        Assert.NotNull(
            page.Continuation);

        Assert.Equal(
            "2",
            page.Continuation.Value);
    }

    [Fact]
    public async Task HandleAsync_ReturnsSubsequentBatchFromContinuation()
    {
        // Arrange
        IReadOnlyList<SearchDocumentsResult> searchResults =
        [
            CreateResult("First Document"),
            CreateResult("Second Document"),
            CreateResult("Third Document"),
            CreateResult("Fourth Document")
        ];

        IReadOnlyList<SearchDocumentsResult> rankedResults =
        [
            searchResults[0],
            searchResults[1],
            searchResults[2],
            searchResults[3]
        ];

        var store =
            new Mock<IDocumentSearchStore>();

        var ranker =
            new Mock<ISearchDocumentsRanker>();

        SearchDocumentsContinuation continuation =
            new("2");

        SearchDocumentsQuery query =
            new(
                "matching",
                Continuation: continuation,
                Limit: 2);

        store
            .Setup(x => x.SearchAsync(
                query,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        ranker
            .Setup(x => x.Rank(searchResults))
            .Returns(rankedResults);

        SearchDocumentsHandler handler =
            CreateHandler(
                store,
                ranker);

        // Act
        SearchDocumentsPage page =
            await handler.HandleAsync(query);

        // Assert
        Assert.Equal(
            rankedResults.Skip(2).Take(2),
            page.Results);

        Assert.False(
            page.HasMore);

        Assert.Null(
            page.Continuation);
    }

    [Fact]
    public async Task HandleAsync_AppliesBatchAfterRanking()
    {
        // Arrange
        IReadOnlyList<SearchDocumentsResult> searchResults =
        [
            CreateResult("Third Document"),
            CreateResult("First Document"),
            CreateResult("Second Document")
        ];

        IReadOnlyList<SearchDocumentsResult> rankedResults =
        [
            searchResults[1],
            searchResults[2],
            searchResults[0]
        ];

        var store =
            new Mock<IDocumentSearchStore>();

        var ranker =
            new Mock<ISearchDocumentsRanker>();

        SearchDocumentsContinuation continuation =
            new("1");

        SearchDocumentsQuery query =
            new(
                "matching",
                Continuation: continuation,
                Limit: 1);

        store
            .Setup(x => x.SearchAsync(
                query,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        ranker
            .Setup(x => x.Rank(searchResults))
            .Returns(rankedResults);

        SearchDocumentsHandler handler =
            CreateHandler(
                store,
                ranker);

        // Act
        SearchDocumentsPage page =
            await handler.HandleAsync(query);

        // Assert
        Assert.Single(
            page.Results);

        Assert.Equal(
            "Second Document",
            page.Results[0].DisplayName);

        Assert.True(
            page.HasMore);

        Assert.NotNull(
            page.Continuation);

        Assert.Equal(
            "2",
            page.Continuation.Value);

        ranker.Verify(
            x => x.Rank(searchResults),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ReturnsNoMoreWhenFinalBatchIsExactLimit()
    {
        // Arrange
        IReadOnlyList<SearchDocumentsResult> searchResults =
        [
            CreateResult("First Document"),
            CreateResult("Second Document")
        ];

        IReadOnlyList<SearchDocumentsResult> rankedResults =
            searchResults;

        var store =
            new Mock<IDocumentSearchStore>();

        var ranker =
            new Mock<ISearchDocumentsRanker>();

        SearchDocumentsQuery query =
            new(
                "matching",
                Limit: 2);

        store
            .Setup(x => x.SearchAsync(
                query,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        ranker
            .Setup(x => x.Rank(searchResults))
            .Returns(rankedResults);

        SearchDocumentsHandler handler =
            CreateHandler(
                store,
                ranker);

        // Act
        SearchDocumentsPage page =
            await handler.HandleAsync(query);

        // Assert
        Assert.Equal(
            2,
            page.Results.Count);

        Assert.False(
            page.HasMore);

        Assert.Null(
            page.Continuation);
    }

    [Fact]
    public async Task HandleAsync_RejectsInvalidContinuation()
    {
        // Arrange
        var store =
            new Mock<IDocumentSearchStore>();

        var ranker =
            new Mock<ISearchDocumentsRanker>();

        SearchDocumentsHandler handler =
            CreateHandler(
                store,
                ranker);

        SearchDocumentsQuery query =
            new(
                "matching",
                Continuation: new SearchDocumentsContinuation(""));

        // Act
        Func<Task> act =
            () =>
                handler.HandleAsync(query);

        // Assert
        await Assert.ThrowsAsync<ArgumentException>(
            act);

        store.Verify(
            x => x.SearchAsync(
                It.IsAny<SearchDocumentsQuery>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        ranker.Verify(
            x => x.Rank(
                It.IsAny<IReadOnlyList<SearchDocumentsResult>>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_RejectsNonPositiveLimit()
    {
        // Arrange
        var store =
            new Mock<IDocumentSearchStore>();

        var ranker =
            new Mock<ISearchDocumentsRanker>();

        SearchDocumentsHandler handler =
            CreateHandler(
                store,
                ranker);

        SearchDocumentsQuery query =
            new(
                "matching",
                Limit: 0);

        // Act
        Func<Task> act =
            () =>
                handler.HandleAsync(query);

        // Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            act);

        store.Verify(
            x => x.SearchAsync(
                It.IsAny<SearchDocumentsQuery>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        ranker.Verify(
            x => x.Rank(
                It.IsAny<IReadOnlyList<SearchDocumentsResult>>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_PropagatesCancellationToken()
    {
        // Arrange
        using var cancellationTokenSource =
            new CancellationTokenSource();

        CancellationToken cancellationToken =
            cancellationTokenSource.Token;

        var store =
            new Mock<IDocumentSearchStore>();

        var ranker =
            new Mock<ISearchDocumentsRanker>();

        SearchDocumentsQuery query =
            new("matching");

        store
            .Setup(x => x.SearchAsync(
                query,
                cancellationToken))
            .ThrowsAsync(
                new OperationCanceledException(
                    cancellationToken));

        SearchDocumentsHandler handler =
            CreateHandler(
                store,
                ranker);

        cancellationTokenSource.Cancel();

        // Act
        Func<Task> act =
            () =>
                handler.HandleAsync(
                    query,
                    cancellationToken);

        // Assert
        await Assert.ThrowsAsync<OperationCanceledException>(act);

        store.Verify(
            x => x.SearchAsync(
                query,
                cancellationToken),
            Times.Once);

        ranker.Verify(
            x => x.Rank(It.IsAny<IReadOnlyList<SearchDocumentsResult>>()),
            Times.Never);
    }

    private static SearchDocumentsHandler CreateHandler(
        Mock<IDocumentSearchStore> store,
        Mock<ISearchDocumentsRanker> ranker)
    {
        return new SearchDocumentsHandler(
            store.Object,
            ranker.Object,
            NullLogger<SearchDocumentsHandler>.Instance);
    }

    private static SearchDocumentsResult CreateResult(
        string displayName)
    {
        return new SearchDocumentsResult(
            Guid.NewGuid(),
            $"{displayName}.txt",
            displayName,
            [
                new SearchMatch(
                    SearchMatchSource.ProcessedContent,
                    SearchMatchKind.Exact,
                    "Matching content.")
            ],
            1);
    }
}
