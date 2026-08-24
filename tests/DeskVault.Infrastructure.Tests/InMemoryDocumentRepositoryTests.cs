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

        await repository.AddAsync(
            document);

        Document? result =
            await repository.GetByIdAsync(
                document.Id);

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

        await repository.AddAsync(
            document);

        bool exists =
            await repository.ExistsByHashAsync(
                document.Sha256Hash);

        Assert.True(
            exists);
    }

    [Fact]
    public async Task ExistsByHashAsync_WhenHashDoesNotExist_ReturnsFalse()
    {
        var repository =
            CreateRepository();

        bool exists =
            await repository.ExistsByHashAsync(
                "missing-hash");

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

        await repository.AddAsync(
            first);

        await repository.AddAsync(
            second);

        IReadOnlyList<Document> documents =
            await repository.GetAllAsync();

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

        await repository.AddAsync(
            document);

        document.MarkProcessing();

        await repository.UpdateAsync(
            document);

        Document? result =
            await repository.GetByIdAsync(
                document.Id);

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
                repository.UpdateAsync(
                    document));
    }

    [Fact]
    public async Task DeleteAsync_WhenDocumentExists_RemovesDocument()
    {
        var repository =
            CreateRepository();

        Document document =
            CreateDocument();

        await repository.AddAsync(
            document);

        await repository.DeleteAsync(
            document.Id);

        Document? result =
            await repository.GetByIdAsync(
                document.Id);

        Assert.Null(
            result);
    }

    [Fact]
    public async Task DeleteAsync_WhenDocumentDoesNotExist_DoesNotThrow()
    {
        var repository =
            CreateRepository();

        await repository.DeleteAsync(
            Guid.NewGuid());
    }

    [Fact]
    public async Task UpdateAsync_WhenCancellationIsRequested_ThrowsOperationCanceledException()
    {
        var repository =
            CreateRepository();

        Document document =
            CreateDocument();

        await repository.AddAsync(
            document);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                repository.UpdateAsync(
                    document,
                    cancellationTokenSource.Token));
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
