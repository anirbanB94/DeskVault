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
        IReadOnlyList<SearchDocumentsResult> result =
            await handler.HandleAsync(query);

        // Assert
        Assert.Equal(
            rankedResults,
            result);

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
