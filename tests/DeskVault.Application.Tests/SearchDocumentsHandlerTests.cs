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
                0,
                "Matching content.")
        ];

        var store =
            new Mock<IDocumentSearchStore>();

        store
            .Setup(x => x.SearchAsync(
                "matching",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        SearchDocumentsHandler handler =
            CreateHandler(store);

        IReadOnlyList<SearchDocumentsResult> result =
            await handler.HandleAsync(
                new SearchDocumentsQuery("matching"));

        Assert.Equal(
            expected,
            result);

        store.Verify(
            x => x.SearchAsync(
                "matching",
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

        store
            .Setup(x => x.SearchAsync(
                It.IsAny<string>(),
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
                    new SearchDocumentsQuery("matching"),
                    cancellationToken));

        store.Verify(
            x => x.SearchAsync(
                "matching",
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
