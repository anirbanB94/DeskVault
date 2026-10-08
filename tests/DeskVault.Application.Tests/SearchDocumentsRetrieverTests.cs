using DeskVault.Application.Documents.Queries.SearchDocuments;
using DeskVault.Application.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DeskVault.Application.Tests;

public sealed class SearchDocumentsRetrieverTests
{
    [Fact]
    public async Task RetrieveAsync_ReturnsFirstPageAndCreatesContinuation()
    {
        // Arrange
        SearchDocumentsResult firstResult =
            CreateResult(
                1,
                "First Document");

        SearchDocumentsResult secondResult =
            CreateResult(
                2,
                "Second Document");

        SearchDocumentsResult thirdResult =
            CreateResult(
                3,
                "Third Document");

        IReadOnlyList<SearchDocumentsResult> searchResults =
        [
            firstResult,
            secondResult,
            thirdResult
        ];

        SearchDocumentsQuery query =
            new(
                "matching",
                Limit: 2);

        SearchDocumentsRankingKey secondResultKey =
            CreateRankingKey(
                secondResult,
                90);

        SearchDocumentsContinuation continuation =
            new("opaque-continuation");

        var store =
            new Mock<IDocumentSearchStore>();

        var ranker =
            new Mock<ISearchDocumentsRanker>();

        var codec =
            new Mock<ISearchDocumentsContinuationCodec>();

        ranker
            .Setup(
                x => x.GetRankingKey(
                    secondResult))
            .Returns(
                secondResultKey);

        store
            .Setup(
                x => x.SearchAsync(
                    query,
                    null,
                    query.Limit,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                searchResults);

        codec
            .Setup(
                x => x.Create(
                    query,
                    secondResultKey))
            .Returns(
                continuation);

        SearchDocumentsRetriever retriever =
            CreateRetriever(
                store,
                ranker,
                codec);

        // Act
        SearchDocumentsPage page =
            await retriever.RetrieveAsync(
                query);

        // Assert
        Assert.Equal(
            [
                firstResult,
                secondResult
            ],
            page.Results);

        Assert.True(
            page.HasMore);

        Assert.Equal(
            continuation,
            page.Continuation);

        store.Verify(
            x => x.SearchAsync(
                query,
                null,
                query.Limit,
                It.IsAny<CancellationToken>()),
            Times.Once);

        ranker.Verify(
            x => x.GetRankingKey(
                secondResult),
            Times.Once);

        ranker.Verify(
            x => x.Rank(
                It.IsAny<IReadOnlyList<SearchDocumentsResult>>()),
            Times.Never);

        codec.Verify(
            x => x.Create(
                query,
                secondResultKey),
            Times.Once);
    }

    [Fact]
    public async Task RetrieveAsync_ResumesStrictlyAfterContinuationPosition()
    {
        // Arrange
        SearchDocumentsResult firstResult =
            CreateResult(
                1,
                "First Document");

        SearchDocumentsResult secondResult =
            CreateResult(
                2,
                "Second Document");

        SearchDocumentsResult thirdResult =
            CreateResult(
                3,
                "Third Document");

        SearchDocumentsResult fourthResult =
            CreateResult(
                4,
                "Fourth Document");

        IReadOnlyList<SearchDocumentsResult> searchResults =
        [
            thirdResult,
            fourthResult
        ];

        SearchDocumentsContinuation continuation =
            new("opaque-continuation");

        SearchDocumentsQuery query =
            new(
                "matching",
                Continuation: continuation,
                Limit: 2);

        SearchDocumentsRankingKey continuationPosition =
            CreateRankingKey(
                secondResult,
                90);

        var store =
            new Mock<IDocumentSearchStore>();

        var ranker =
            new Mock<ISearchDocumentsRanker>();

        var codec =
            new Mock<ISearchDocumentsContinuationCodec>();

        store
            .Setup(
                x => x.SearchAsync(
                    query,
                    continuationPosition,
                    query.Limit,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                searchResults);

        codec
            .Setup(
                x => x.Decode(
                    query,
                    continuation))
            .Returns(
                continuationPosition);

        SearchDocumentsRetriever retriever =
            CreateRetriever(
                store,
                ranker,
                codec);

        // Act
        SearchDocumentsPage page =
            await retriever.RetrieveAsync(
                query);

        // Assert
        Assert.Equal(
            [
                thirdResult,
                fourthResult
            ],
            page.Results);

        Assert.False(
            page.HasMore);

        Assert.Null(
            page.Continuation);

        store.Verify(
            x => x.SearchAsync(
                query,
                continuationPosition,
                query.Limit,
                It.IsAny<CancellationToken>()),
            Times.Once);

        codec.Verify(
            x => x.Decode(
                query,
                continuation),
            Times.Once);

        ranker.Verify(
            x => x.Rank(
                It.IsAny<IReadOnlyList<SearchDocumentsResult>>()),
            Times.Never);
    }

    [Fact]
    public async Task RetrieveAsync_WhenResultsHaveSameScore_ResumesAfterDisplayNameTieBreaker()
    {
        // Arrange
        SearchDocumentsResult firstResult =
            CreateResult(
                1,
                "Alpha Document");

        SearchDocumentsResult secondResult =
            CreateResult(
                2,
                "Beta Document");

        SearchDocumentsContinuation continuation =
            new("opaque-continuation");

        SearchDocumentsQuery query =
            new(
                "matching",
                Continuation: continuation,
                Limit: 1);

        SearchDocumentsRankingKey continuationPosition =
            CreateRankingKey(
                firstResult,
                100);

        SearchDocumentsRankingKey secondResultKey =
            CreateRankingKey(
                secondResult,
                100);

        var store =
            new Mock<IDocumentSearchStore>();

        var ranker =
            new Mock<ISearchDocumentsRanker>();

        var codec =
            new Mock<ISearchDocumentsContinuationCodec>();

        IReadOnlyList<SearchDocumentsResult> searchResults =
        [
            secondResult,
            CreateResult(
                3,
                "Gamma Document")
        ];

        store
            .Setup(
                x => x.SearchAsync(
                    query,
                    continuationPosition,
                    query.Limit,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                searchResults);

        codec
            .Setup(
                x => x.Decode(
                    query,
                    continuation))
            .Returns(
                continuationPosition);

        SearchDocumentsRetriever retriever =
            CreateRetriever(
                store,
                ranker,
                codec);

        // Act
        SearchDocumentsPage page =
            await retriever.RetrieveAsync(
                query);

        // Assert
        Assert.Single(
            page.Results);

        Assert.Equal(
            secondResult,
            page.Results[0]);

        Assert.True(
            page.HasMore);

        Assert.Null(
            page.Continuation);

        store.Verify(
            x => x.SearchAsync(
                query,
                continuationPosition,
                query.Limit,
                It.IsAny<CancellationToken>()),
            Times.Once);

        codec.Verify(
            x => x.Decode(
                query,
                continuation),
            Times.Once);

        ranker.Verify(
            x => x.Rank(
                It.IsAny<IReadOnlyList<SearchDocumentsResult>>()),
            Times.Never);
    }

    [Fact]
    public async Task RetrieveAsync_WhenScoreAndDisplayNameAreEqual_ContinuesUsingDocumentIdTieBreaker()
    {
        // Arrange
        SearchDocumentsResult firstResult =
            CreateResult(
                1,
                "Same Name");

        SearchDocumentsResult secondResult =
            CreateResult(
                2,
                "Same Name");

        SearchDocumentsResult thirdResult =
            CreateResult(
                3,
                "Same Name");

        SearchDocumentsContinuation continuation =
            new("opaque-continuation");

        SearchDocumentsQuery query =
            new(
                "matching",
                Continuation: continuation,
                Limit: 1);

        SearchDocumentsRankingKey continuationPosition =
            CreateRankingKey(
                secondResult,
                100);

        IReadOnlyList<SearchDocumentsResult> searchResults =
        [
            thirdResult
        ];

        var store =
            new Mock<IDocumentSearchStore>();

        var ranker =
            new Mock<ISearchDocumentsRanker>();

        var codec =
            new Mock<ISearchDocumentsContinuationCodec>();

        store
            .Setup(
                x => x.SearchAsync(
                    query,
                    continuationPosition,
                    query.Limit,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                searchResults);

        codec
            .Setup(
                x => x.Decode(
                    query,
                    continuation))
            .Returns(
                continuationPosition);

        SearchDocumentsRetriever retriever =
            CreateRetriever(
                store,
                ranker,
                codec);

        // Act
        SearchDocumentsPage page =
            await retriever.RetrieveAsync(
                query);

        // Assert
        Assert.Single(
            page.Results);

        Assert.Equal(
            thirdResult,
            page.Results[0]);

        Assert.False(
            page.HasMore);

        Assert.Null(
            page.Continuation);

        store.Verify(
            x => x.SearchAsync(
                query,
                continuationPosition,
                query.Limit,
                It.IsAny<CancellationToken>()),
            Times.Once);

        codec.Verify(
            x => x.Decode(
                query,
                continuation),
            Times.Once);

        ranker.Verify(
            x => x.Rank(
                It.IsAny<IReadOnlyList<SearchDocumentsResult>>()),
            Times.Never);
    }

    [Fact]
    public async Task RetrieveAsync_WhenContinuationIsIncompatible_DoesNotRetrieveResults()
    {
        // Arrange
        SearchDocumentsContinuation continuation =
            new("incompatible");

        SearchDocumentsQuery query =
            new(
                "matching",
                Continuation: continuation);

        var store =
            new Mock<IDocumentSearchStore>();

        var ranker =
            new Mock<ISearchDocumentsRanker>();

        var codec =
            new Mock<ISearchDocumentsContinuationCodec>();

        codec
            .Setup(
                x => x.Decode(
                    query,
                    continuation))
            .Throws(
                new ArgumentException(
                    "Continuation does not match the search criteria."));

        SearchDocumentsRetriever retriever =
            CreateRetriever(
                store,
                ranker,
                codec);

        // Act
        Func<Task> act =
            () =>
                retriever.RetrieveAsync(
                    query);

        // Assert
        await Assert.ThrowsAsync<ArgumentException>(
            act);

        store.Verify(
            x => x.SearchAsync(
                It.IsAny<SearchDocumentsQuery>(),
                It.IsAny<SearchDocumentsRankingKey?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        ranker.Verify(
            x => x.Rank(
                It.IsAny<IReadOnlyList<SearchDocumentsResult>>()),
            Times.Never);
    }

    [Fact]
    public async Task RetrieveAsync_RejectsEmptyContinuation()
    {
        // Arrange
        SearchDocumentsQuery query =
            new(
                "matching",
                Continuation:
                    new SearchDocumentsContinuation(
                        ""));

        var store =
            new Mock<IDocumentSearchStore>();

        var ranker =
            new Mock<ISearchDocumentsRanker>();

        var codec =
            new Mock<ISearchDocumentsContinuationCodec>();

        SearchDocumentsRetriever retriever =
            CreateRetriever(
                store,
                ranker,
                codec);

        // Act
        Func<Task> act =
            () =>
                retriever.RetrieveAsync(
                    query);

        // Assert
        await Assert.ThrowsAsync<ArgumentException>(
            act);

        store.Verify(
            x => x.SearchAsync(
                It.IsAny<SearchDocumentsQuery>(),
                It.IsAny<SearchDocumentsRankingKey?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        codec.Verify(
            x => x.Decode(
                It.IsAny<SearchDocumentsQuery>(),
                It.IsAny<SearchDocumentsContinuation>()),
            Times.Never);
    }

    [Fact]
    public async Task RetrieveAsync_RejectsNonPositiveLimit()
    {
        // Arrange
        SearchDocumentsQuery query =
            new(
                "matching",
                Limit: 0);

        var store =
            new Mock<IDocumentSearchStore>();

        var ranker =
            new Mock<ISearchDocumentsRanker>();

        var codec =
            new Mock<ISearchDocumentsContinuationCodec>();

        SearchDocumentsRetriever retriever =
            CreateRetriever(
                store,
                ranker,
                codec);

        // Act
        Func<Task> act =
            () =>
                retriever.RetrieveAsync(
                    query);

        // Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            act);

        store.Verify(
            x => x.SearchAsync(
                It.IsAny<SearchDocumentsQuery>(),
                It.IsAny<SearchDocumentsRankingKey?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        codec.Verify(
            x => x.Decode(
                It.IsAny<SearchDocumentsQuery>(),
                It.IsAny<SearchDocumentsContinuation>()),
            Times.Never);
    }

    [Fact]
    public async Task RetrieveAsync_PropagatesCancellationToken()
    {
        // Arrange
        using var cancellationTokenSource =
            new CancellationTokenSource();

        CancellationToken cancellationToken =
            cancellationTokenSource.Token;

        SearchDocumentsQuery query =
            new("matching");

        var store =
            new Mock<IDocumentSearchStore>();

        var ranker =
            new Mock<ISearchDocumentsRanker>();

        var codec =
            new Mock<ISearchDocumentsContinuationCodec>();

        store
            .Setup(
                x => x.SearchAsync(
                    query,
                    null,
                    query.Limit,
                    cancellationToken))
            .ThrowsAsync(
                new OperationCanceledException(
                    cancellationToken));

        SearchDocumentsRetriever retriever =
            CreateRetriever(
                store,
                ranker,
                codec);

        cancellationTokenSource.Cancel();

        // Act
        Func<Task> act =
            () =>
                retriever.RetrieveAsync(
                    query,
                    cancellationToken);

        // Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            act);

        store.Verify(
            x => x.SearchAsync(
                query,
                null,
                query.Limit,
                cancellationToken),
            Times.Once);

        codec.Verify(
            x => x.Decode(
                It.IsAny<SearchDocumentsQuery>(),
                It.IsAny<SearchDocumentsContinuation>()),
            Times.Never);

        ranker.Verify(
            x => x.Rank(
                It.IsAny<IReadOnlyList<SearchDocumentsResult>>()),
            Times.Never);
    }

    [Fact]
    public async Task RetrieveAsync_WhenFinalPageMatchesLimit_DoesNotCreateContinuation()
    {
        // Arrange
        SearchDocumentsResult firstResult =
            CreateResult(
                1,
                "First Document");

        SearchDocumentsResult secondResult =
            CreateResult(
                2,
                "Second Document");

        IReadOnlyList<SearchDocumentsResult> searchResults =
        [
            firstResult,
            secondResult
        ];

        SearchDocumentsQuery query =
            new(
                "matching",
                Limit: 2);

        var store =
            new Mock<IDocumentSearchStore>();

        var ranker =
            new Mock<ISearchDocumentsRanker>();

        var codec =
            new Mock<ISearchDocumentsContinuationCodec>();

        store
            .Setup(
                x => x.SearchAsync(
                    query,
                    null,
                    query.Limit,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                searchResults);

        SearchDocumentsRetriever retriever =
            CreateRetriever(
                store,
                ranker,
                codec);

        // Act
        SearchDocumentsPage page =
            await retriever.RetrieveAsync(
                query);

        // Assert
        Assert.Equal(
            2,
            page.Results.Count);

        Assert.False(
            page.HasMore);

        Assert.Null(
            page.Continuation);

        store.Verify(
            x => x.SearchAsync(
                query,
                null,
                query.Limit,
                It.IsAny<CancellationToken>()),
            Times.Once);

        codec.Verify(
            x => x.Create(
                It.IsAny<SearchDocumentsQuery>(),
                It.IsAny<SearchDocumentsRankingKey>()),
            Times.Never);

        ranker.Verify(
            x => x.Rank(
                It.IsAny<IReadOnlyList<SearchDocumentsResult>>()),
            Times.Never);
    }

    private static SearchDocumentsRetriever CreateRetriever(
        Mock<IDocumentSearchStore> store,
        Mock<ISearchDocumentsRanker> ranker,
        Mock<ISearchDocumentsContinuationCodec> codec)
    {
        return new SearchDocumentsRetriever(
            store.Object,
            ranker.Object,
            codec.Object,
            NullLogger<SearchDocumentsRetriever>.Instance);
    }

    private static SearchDocumentsRankingKey CreateRankingKey(
        SearchDocumentsResult result,
        int relevanceScore = 100)
    {
        return new SearchDocumentsRankingKey(
            relevanceScore,
            result.DisplayName,
            result.DocumentId);
    }

    private static SearchDocumentsResult CreateResult(
        int id,
        string displayName)
    {
        return new SearchDocumentsResult(
            CreateDocumentId(id),
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

    private static Guid CreateDocumentId(
        int id)
    {
        return Guid.Parse(
            $"00000000-0000-0000-0000-{id:D12}");
    }
}
