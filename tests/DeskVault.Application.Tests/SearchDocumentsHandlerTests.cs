using DeskVault.Application.Documents.Queries.SearchDocuments;
using DeskVault.Application.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DeskVault.Application.Tests;

public sealed class SearchDocumentsHandlerTests
{
    [Fact]
    public async Task HandleAsync_ReturnsSearchResultsFromStore()
    {
        IReadOnlyList<SearchDocumentsResult> expected =
        [
            new SearchDocumentsResult(
                Guid.NewGuid(),
                "document.txt",
                "Test Document",
                [
                    new SearchMatch(
                        SearchMatchSource.ProcessedContent,
                        SearchMatchKind.Exact,
                        "Matching content.")
                ],
                1)
        ];

        var store =
            new Mock<IDocumentSearchStore>();

        SearchDocumentsQuery query =
            new("matching");

        store
            .Setup(x => x.SearchAsync(
                query,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        SearchDocumentsHandler handler =
            CreateHandler(store);

        IReadOnlyList<SearchDocumentsResult> result =
            await handler.HandleAsync(
                query);

        Assert.Equal(
            expected,
            result);

        store.Verify(
            x => x.SearchAsync(
                query,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_PropagatesCancellationToken()
    {
        using var cancellationTokenSource =
            new CancellationTokenSource();

        CancellationToken cancellationToken =
            cancellationTokenSource.Token;

        var store =
            new Mock<IDocumentSearchStore>();

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
            CreateHandler(store);

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () =>
                handler.HandleAsync(
                    query,
                    cancellationToken));

        store.Verify(
            x => x.SearchAsync(
                query,
                cancellationToken),
            Times.Once);
    }

    private static SearchDocumentsHandler CreateHandler(
        Mock<IDocumentSearchStore> store)
    {
        return new SearchDocumentsHandler(
            store.Object,
            NullLogger<SearchDocumentsHandler>.Instance);
    }
}
