using DeskVault.Application.Interfaces;
using DeskVault.Domain.Documents;
using DeskVault.Infrastructure.Repositories;

namespace DeskVault.Infrastructure.Tests;

public sealed class InMemoryDocumentRepositoryTests
{
    [Fact]
    public async Task AddAsync_WhenDocumentIsValid_CanRetrieveDocument()
    {
        var repository =
            CreateRepository();

        Document document =
            CreateDocument();

        await repository.AddAsync(document, TestContext.Current.CancellationToken);

        Document? result =
            await repository.GetByIdAsync(document.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(result);

        Assert.Equal(
            document.Id,
            result.Id);

        Assert.Equal(
            document.FileName,
            result.FileName);

        Assert.Equal(
            document.DisplayName,
            result.DisplayName);

        Assert.Equal(
            document.Sha256Hash,
            result.Sha256Hash);

        Assert.Equal(
            document.StoredFilePath,
            result.StoredFilePath);

        Assert.Equal(
            document.Status,
            result.Status);
    }

    [Fact]
    public async Task ExistsByHashAsync_WhenHashExists_ReturnsTrue()
    {
        var repository =
            CreateRepository();

        Document document =
            CreateDocument();

        await repository.AddAsync(document, TestContext.Current.CancellationToken);

        bool exists =
            await repository.ExistsByHashAsync(document.Sha256Hash, TestContext.Current.CancellationToken);

        Assert.True(
            exists);
    }

    [Fact]
    public async Task ExistsByHashAsync_WhenHashDoesNotExist_ReturnsFalse()
    {
        var repository =
            CreateRepository();

        bool exists =
            await repository.ExistsByHashAsync("missing-hash", TestContext.Current.CancellationToken);

        Assert.False(
            exists);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllDocuments()
    {
        var repository =
            CreateRepository();

        Document first =
            CreateDocument();

        Document second =
            Document.Create(
                Guid.NewGuid(),
                "second.txt",
                "Second Document",
                "second-hash",
                "second.dvault");

        await repository.AddAsync(first, TestContext.Current.CancellationToken);

        await repository.AddAsync(second, TestContext.Current.CancellationToken);

        IReadOnlyList<Document> documents =
            await repository.GetAllAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            2,
            documents.Count);

        Assert.Contains(
            first,
            documents);

        Assert.Contains(
            second,
            documents);
    }

    [Fact]
    public async Task UpdateAsync_WhenDocumentExists_PersistsUpdatedDocument()
    {
        var repository =
            CreateRepository();

        Document document =
            CreateDocument();

        await repository.AddAsync(document, TestContext.Current.CancellationToken);

        document.MarkProcessing();

        await repository.UpdateAsync(document, TestContext.Current.CancellationToken);

        Document? result =
            await repository.GetByIdAsync(document.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(result);

        Assert.Equal(
            DocumentStatus.Processing,
            result.Status);
    }

    [Fact]
    public async Task UpdateAsync_WhenDocumentDoesNotExist_ThrowsInvalidOperationException()
    {
        var repository =
            CreateRepository();

        Document document =
            CreateDocument();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                repository.UpdateAsync(document, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteAsync_WhenDocumentExists_RemovesDocument()
    {
        var repository =
            CreateRepository();

        Document document =
            CreateDocument();

        await repository.AddAsync(document, TestContext.Current.CancellationToken);

        await repository.DeleteAsync(document.Id, TestContext.Current.CancellationToken);

        Document? result =
            await repository.GetByIdAsync(document.Id, TestContext.Current.CancellationToken);

        Assert.Null(
            result);
    }

    [Fact]
    public async Task DeleteAsync_WhenDocumentDoesNotExist_DoesNotThrow()
    {
        var repository =
            CreateRepository();

        await repository.DeleteAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UpdateAsync_WhenCancellationIsRequested_ThrowsOperationCanceledException()
    {
        var repository =
            CreateRepository();

        Document document =
            CreateDocument();

        await repository.AddAsync(document, TestContext.Current.CancellationToken);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                repository.UpdateAsync(
                    document,
                    cancellationTokenSource.Token));
    }

    [Fact]
    public async Task GetPageAsync_ReturnsRequestedPageInDeterministicImportOrder()
    {
        var repository =
            CreateRepository();

        DateTime importedAt =
            DateTime.UtcNow;

        Document older =
            Document.Restore(
                Guid.NewGuid(),
                "older.txt",
                "Older Document",
                "in-memory-hash-older",
                "older.dvault",
                importedAt.AddMinutes(-10),
                DocumentStatus.Imported);

        Document middle =
            Document.Restore(
                Guid.NewGuid(),
                "middle.txt",
                "Middle Document",
                "in-memory-hash-middle",
                "middle.dvault",
                importedAt.AddMinutes(-5),
                DocumentStatus.Imported);

        Document newer =
            Document.Restore(
                Guid.NewGuid(),
                "newer.txt",
                "Newer Document",
                "in-memory-hash-newer",
                "newer.dvault",
                importedAt,
                DocumentStatus.Imported);

        await repository.AddAsync(
            older,
            TestContext.Current.CancellationToken);

        await repository.AddAsync(
            newer,
            TestContext.Current.CancellationToken);

        await repository.AddAsync(
            middle,
            TestContext.Current.CancellationToken);

        IReadOnlyList<Document> firstPage =
            await repository.GetPageAsync(
                null,
                1,
                TestContext.Current.CancellationToken);

        Document firstDocument =
            Assert.Single(firstPage);

        IReadOnlyList<Document> secondPage =
            await repository.GetPageAsync(
                new DocumentPageCursor(
                    firstDocument.ImportedAt,
                    firstDocument.Id),
                1,
                TestContext.Current.CancellationToken);

        Document secondDocument =
            Assert.Single(secondPage);

        IReadOnlyList<Document> thirdPage =
            await repository.GetPageAsync(
                new DocumentPageCursor(
                    secondDocument.ImportedAt,
                    secondDocument.Id),
                1,
                TestContext.Current.CancellationToken);

        Document thirdDocument =
            Assert.Single(thirdPage);

        Assert.Equal(newer.Id, firstDocument.Id);
        Assert.Equal(middle.Id, secondDocument.Id);
        Assert.Equal(older.Id, thirdDocument.Id);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(257)]
    public async Task GetPageAsync_WhenPageSizeIsInvalid_ThrowsArgumentOutOfRangeException(
        int pageSize)
    {
        var repository =
            CreateRepository();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => repository.GetPageAsync(
                null,
                pageSize,
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetPageAsync_WhenCursorHasDefaultTimestamp_ThrowsArgumentException()
    {
        var repository =
            CreateRepository();

        var cursor =
            new DocumentPageCursor(
                default,
                Guid.NewGuid());

        await Assert.ThrowsAsync<ArgumentException>(
            () => repository.GetPageAsync(
                cursor,
                10,
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetPageAsync_WhenCursorHasEmptyDocumentId_ThrowsArgumentException()
    {
        var repository =
            CreateRepository();

        var cursor =
            new DocumentPageCursor(
                DateTime.UtcNow,
                Guid.Empty);

        await Assert.ThrowsAsync<ArgumentException>(
            () => repository.GetPageAsync(
                cursor,
                10,
                TestContext.Current.CancellationToken));
    }

    private static InMemoryDocumentRepository CreateRepository()
    {
        return new InMemoryDocumentRepository();
    }

    private static Document CreateDocument()
    {
        return Document.Create(
            Guid.NewGuid(),
            "document.txt",
            "Test Document",
            "sha256-test-hash",
            "document.dvault");
    }
}
