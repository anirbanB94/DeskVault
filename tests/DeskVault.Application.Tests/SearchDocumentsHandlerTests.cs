using DeskVault.Application.Documents.Queries.SearchDocuments;
using DeskVault.Application.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DeskVault.Application.Tests;

public sealed class SearchDocumentsHandlerTests
{
    [Fact]
    public async Task HandleAsync_ReturnsRankedFirstPageAndCreatesContinuation()
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
            thirdResult,
            firstResult,
            secondResult
        ];

        IReadOnlyList<SearchDocumentsResult> rankedResults =
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

        SetupRanker(
            ranker,
            rankedResults,
            new Dictionary<Guid, int>
            {
                [firstResult.DocumentId] = 100,
                [secondResult.DocumentId] = 90,
                [thirdResult.DocumentId] = 80
            });

        store
            .Setup(
                x => x.SearchAsync(
                    query,
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

        SearchDocumentsHandler handler =
            CreateHandler(
                store,
                ranker,
                codec);

        // Act
        SearchDocumentsPage page =
            await handler.HandleAsync(
                query);

        // Assert
        Assert.Equal(
            rankedResults.Take(2),
            page.Results);

        Assert.True(
            page.HasMore);

        Assert.Equal(
            continuation,
            page.Continuation);

        store.Verify(
            x => x.SearchAsync(
                query,
                It.IsAny<CancellationToken>()),
            Times.Once);

        ranker.Verify(
            x => x.Rank(
                searchResults),
            Times.Once);

        codec.Verify(
            x => x.Create(
                query,
                secondResultKey),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ResumesStrictlyAfterContinuationPosition()
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
            firstResult,
            secondResult,
            thirdResult,
            fourthResult
        ];

        IReadOnlyList<SearchDocumentsResult> rankedResults =
        [
            firstResult,
            secondResult,
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

        SetupRanker(
            ranker,
            rankedResults,
            new Dictionary<Guid, int>
            {
                [firstResult.DocumentId] = 100,
                [secondResult.DocumentId] = 90,
                [thirdResult.DocumentId] = 80,
                [fourthResult.DocumentId] = 70
            });

        store
            .Setup(
                x => x.SearchAsync(
                    query,
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

        SearchDocumentsHandler handler =
            CreateHandler(
                store,
                ranker,
                codec);

        // Act
        SearchDocumentsPage page =
            await handler.HandleAsync(
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

        codec.Verify(
            x => x.Decode(
                query,
                continuation),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenResultsHaveSameScore_ResumesAfterDisplayNameTieBreaker()
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

        SearchDocumentsResult thirdResult =
            CreateResult(
                3,
                "Gamma Document");

        IReadOnlyList<SearchDocumentsResult> rankedResults =
        [
            firstResult,
            secondResult,
            thirdResult
        ];

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

        var store =
            new Mock<IDocumentSearchStore>();

        var ranker =
            new Mock<ISearchDocumentsRanker>();

        var codec =
            new Mock<ISearchDocumentsContinuationCodec>();

        SetupRanker(
            ranker,
            rankedResults);

        store
            .Setup(
                x => x.SearchAsync(
                    query,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                rankedResults);

        codec
            .Setup(
                x => x.Decode(
                    query,
                    continuation))
            .Returns(
                continuationPosition);

        SearchDocumentsHandler handler =
            CreateHandler(
                store,
                ranker,
                codec);

        // Act
        SearchDocumentsPage page =
            await handler.HandleAsync(
                query);

        // Assert
        Assert.Single(
            page.Results);

        Assert.Equal(
            secondResult,
            page.Results[0]);

        Assert.True(
            page.HasMore);

        codec.Verify(
            x => x.Decode(
                query,
                continuation),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenScoreAndDisplayNameAreEqual_ResumesUsingDocumentIdTieBreaker()
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

        IReadOnlyList<SearchDocumentsResult> rankedResults =
        [
            firstResult,
            secondResult,
            thirdResult
        ];

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

        var store =
            new Mock<IDocumentSearchStore>();

        var ranker =
            new Mock<ISearchDocumentsRanker>();

        var codec =
            new Mock<ISearchDocumentsContinuationCodec>();

        SetupRanker(
            ranker,
            rankedResults);

        store
            .Setup(
                x => x.SearchAsync(
                    query,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                rankedResults);

        codec
            .Setup(
                x => x.Decode(
                    query,
                    continuation))
            .Returns(
                continuationPosition);

        SearchDocumentsHandler handler =
            CreateHandler(
                store,
                ranker,
                codec);

        // Act
        SearchDocumentsPage page =
            await handler.HandleAsync(
                query);

        // Assert
        Assert.Single(
            page.Results);

        Assert.Equal(
            thirdResult,
            page.Results[0]);

        Assert.False(
            page.HasMore);

        codec.Verify(
            x => x.Decode(
                query,
                continuation),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenContinuationIsIncompatible_DoesNotRetrieveResults()
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

        SearchDocumentsHandler handler =
            CreateHandler(
                store,
                ranker,
                codec);

        // Act
        Func<Task> act =
            () =>
                handler.HandleAsync(
                    query);

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
    public async Task HandleAsync_RejectsEmptyContinuation()
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

        SearchDocumentsHandler handler =
            CreateHandler(
                store,
                ranker,
                codec);

        // Act
        Func<Task> act =
            () =>
                handler.HandleAsync(
                    query);

        // Assert
        await Assert.ThrowsAsync<ArgumentException>(
            act);

        store.Verify(
            x => x.SearchAsync(
                It.IsAny<SearchDocumentsQuery>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        codec.Verify(
            x => x.Decode(
                It.IsAny<SearchDocumentsQuery>(),
                It.IsAny<SearchDocumentsContinuation>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_RejectsNonPositiveLimit()
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

        SearchDocumentsHandler handler =
            CreateHandler(
                store,
                ranker,
                codec);

        // Act
        Func<Task> act =
            () =>
                handler.HandleAsync(
                    query);

        // Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            act);

        store.Verify(
            x => x.SearchAsync(
                It.IsAny<SearchDocumentsQuery>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        codec.Verify(
            x => x.Decode(
                It.IsAny<SearchDocumentsQuery>(),
                It.IsAny<SearchDocumentsContinuation>()),
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
                    cancellationToken))
            .ThrowsAsync(
                new OperationCanceledException(
                    cancellationToken));

        SearchDocumentsHandler handler =
            CreateHandler(
                store,
                ranker,
                codec);

        cancellationTokenSource.Cancel();

        // Act
        Func<Task> act =
            () =>
                handler.HandleAsync(
                    query,
                    cancellationToken);

        // Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            act);

        store.Verify(
            x => x.SearchAsync(
                query,
                cancellationToken),
            Times.Once);

        codec.Verify(
            x => x.Decode(
                It.IsAny<SearchDocumentsQuery>(),
                It.IsAny<SearchDocumentsContinuation>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenFinalPageMatchesLimit_DoesNotCreateContinuation()
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

        var store =
            new Mock<IDocumentSearchStore>();

        var ranker =
            new Mock<ISearchDocumentsRanker>();

        var codec =
            new Mock<ISearchDocumentsContinuationCodec>();

        SetupRanker(
            ranker,
            searchResults);

        store
            .Setup(
                x => x.SearchAsync(
                    It.IsAny<SearchDocumentsQuery>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                searchResults);

        SearchDocumentsHandler handler =
            CreateHandler(
                store,
                ranker,
                codec);

        // Act
        SearchDocumentsPage page =
            await handler.HandleAsync(
                new SearchDocumentsQuery(
                    "matching",
                    Limit: 2));

        // Assert
        Assert.Equal(
            2,
            page.Results.Count);

        Assert.False(
            page.HasMore);

        Assert.Null(
            page.Continuation);

        codec.Verify(
            x => x.Create(
                It.IsAny<SearchDocumentsQuery>(),
                It.IsAny<SearchDocumentsRankingKey>()),
            Times.Never);
    }

    private static SearchDocumentsHandler CreateHandler(
        Mock<IDocumentSearchStore> store,
        Mock<ISearchDocumentsRanker> ranker,
        Mock<ISearchDocumentsContinuationCodec> codec)
    {
        return new SearchDocumentsHandler(
            store.Object,
            ranker.Object,
            codec.Object,
            NullLogger<SearchDocumentsHandler>.Instance);
    }

    private static void SetupRanker(
        Mock<ISearchDocumentsRanker> ranker,
        IReadOnlyList<SearchDocumentsResult> rankedResults,
        IReadOnlyDictionary<Guid, int>? relevanceScores = null)
    {
        ranker
            .Setup(
                x => x.Rank(
                    It.IsAny<IReadOnlyList<SearchDocumentsResult>>()))
            .Returns(
                rankedResults);

        foreach (SearchDocumentsResult result in rankedResults)
        {
            int relevanceScore =
                relevanceScores is not null &&
                relevanceScores.TryGetValue(
                    result.DocumentId,
                    out int configuredScore)
                    ? configuredScore
                    : 100;

            ranker
                .Setup(
                    x => x.GetRankingKey(
                        result))
                .Returns(
                    CreateRankingKey(
                        result,
                        relevanceScore));
        }
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
