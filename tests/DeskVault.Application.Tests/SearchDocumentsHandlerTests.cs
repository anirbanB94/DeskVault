using DeskVault.Application.Documents.Queries.SearchDocuments;
using DeskVault.Application.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;

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
                0,
                "Matching content.")
        ];

        var store =
            new TestDocumentSearchStore(expected);

        var handler =
            new SearchDocumentsHandler(
                store,
                NullLogger<SearchDocumentsHandler>.Instance);

        IReadOnlyList<SearchDocumentsResult> result =
            await handler.HandleAsync(
                new SearchDocumentsQuery("matching"));

        Assert.Equal(
            expected,
            result);
    }

    [Fact]
    public async Task HandleAsync_PropagatesCancellationToken()
    {
        using var cancellationTokenSource =
            new CancellationTokenSource();

        CancellationToken cancellationToken =
            cancellationTokenSource.Token;

        var store =
            new TestDocumentSearchStore(
                [],
                cancellationToken);

        var handler =
            new SearchDocumentsHandler(
                store,
                NullLogger<SearchDocumentsHandler>.Instance);

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () =>
                handler.HandleAsync(
                    new SearchDocumentsQuery("matching"),
                    cancellationToken));
    }

    private sealed class TestDocumentSearchStore
        : IDocumentSearchStore
    {
        private readonly IReadOnlyList<SearchDocumentsResult> _results;
        private readonly CancellationToken _expectedCancellationToken;

        public TestDocumentSearchStore(
            IReadOnlyList<SearchDocumentsResult> results,
            CancellationToken expectedCancellationToken = default)
        {
            _results = results;
            _expectedCancellationToken = expectedCancellationToken;
        }

        public Task<IReadOnlyList<SearchDocumentsResult>> SearchAsync(
            string searchText,
            CancellationToken cancellationToken = default)
        {
            Assert.Equal(
                _expectedCancellationToken,
                cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(
                    cancellationToken);
            }

            return Task.FromResult(_results);
        }
    }
}
