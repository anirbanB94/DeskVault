using DeskVault.Application.Documents.Queries.SearchDocuments;
using DeskVault.Application.Interfaces;
using Moq;

namespace DeskVault.Application.Tests;

public sealed class SearchDocumentsHandlerTests
{
    [Fact]
    public async Task HandleAsync_DelegatesQueryToRetriever()
    {
        // Arrange
        SearchDocumentsQuery query =
            new(
                "matching",
                Limit: 2);

        SearchDocumentsPage expectedPage =
            new(
                [],
                false,
                null);

        var retriever =
            new Mock<ISearchDocumentsRetriever>();

        retriever
            .Setup(
                x => x.RetrieveAsync(
                    query,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                expectedPage);

        SearchDocumentsHandler handler =
            new(
                retriever.Object);

        // Act
        SearchDocumentsPage actualPage =
            await handler.HandleAsync(
                query);

        // Assert
        Assert.Same(
            expectedPage,
            actualPage);

        retriever.Verify(
            x => x.RetrieveAsync(
                query,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_PassesCancellationTokenToRetriever()
    {
        // Arrange
        SearchDocumentsQuery query =
            new("matching");

        using var cancellationTokenSource =
            new CancellationTokenSource();

        CancellationToken cancellationToken =
            cancellationTokenSource.Token;

        SearchDocumentsPage expectedPage =
            new(
                [],
                false,
                null);

        var retriever =
            new Mock<ISearchDocumentsRetriever>();

        retriever
            .Setup(
                x => x.RetrieveAsync(
                    query,
                    cancellationToken))
            .ReturnsAsync(
                expectedPage);

        SearchDocumentsHandler handler =
            new(
                retriever.Object);

        // Act
        SearchDocumentsPage actualPage =
            await handler.HandleAsync(
                query,
                cancellationToken);

        // Assert
        Assert.Same(
            expectedPage,
            actualPage);

        retriever.Verify(
            x => x.RetrieveAsync(
                query,
                cancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenQueryIsNull_ThrowsArgumentNullExceptionWithoutCallingRetriever()
    {
        // Arrange
        var retriever =
            new Mock<ISearchDocumentsRetriever>();

        SearchDocumentsHandler handler =
            new(
                retriever.Object);

        // Act
        Func<Task> act =
            () =>
                handler.HandleAsync(
                    null!);

        // Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            act);

        retriever.Verify(
            x => x.RetrieveAsync(
                It.IsAny<SearchDocumentsQuery>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
