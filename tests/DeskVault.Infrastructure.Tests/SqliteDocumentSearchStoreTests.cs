using DeskVault.Application.Documents.Chunking;
using DeskVault.Application.Documents.Queries.SearchDocuments;
using DeskVault.Domain.Documents;
using DeskVault.Infrastructure.Persistence.Context;
using DeskVault.Infrastructure.Persistence.Entities;
using DeskVault.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeskVault.Infrastructure.Tests;

public sealed class SqliteDocumentSearchStoreTests
{
    [Fact]
    public async Task SearchAsync_WhenMatchingChunkExists_ReturnsDocumentAndChunk()
    {
        await using SqliteConnection connection =
            CreateConnection();

        Document document =
            CreateAndPersistDocument(
                connection,
                "searchable.txt",
                "Searchable Document");

        var processingStore =
            CreateProcessingStore(connection);

        await processingStore.ReplaceChunksAsync(
            document.Id,
            [
                new DocumentChunk(
                    0,
                    "This is the introduction."),

                new DocumentChunk(
                    1,
                    "This chunk contains the searchable content."),

                new DocumentChunk(
                    2,
                    "This is the conclusion.")
            ]);

        var searchStore =
            CreateSearchStore(connection);

        IReadOnlyList<SearchDocumentsResult> results =
            await searchStore.SearchAsync(
                "searchable");

        SearchDocumentsResult result =
            Assert.Single(results);

        Assert.Equal(
            document.Id,
            result.DocumentId);

        Assert.Equal(
            document.FileName,
            result.FileName);

        Assert.Equal(
            document.DisplayName,
            result.DisplayName);

        Assert.Equal(
            1,
            result.ChunkOrder);

        Assert.Equal(
            "This chunk contains the searchable content.",
            result.ChunkText);
    }

    [Fact]
    public async Task SearchAsync_WhenSearchTextDiffersOnlyByCase_ReturnsMatchingChunk()
    {
        await using SqliteConnection connection =
            CreateConnection();

        Document document =
            CreateAndPersistDocument(
                connection,
                "case-test.txt",
                "Case Test Document");

        var processingStore =
            CreateProcessingStore(connection);

        await processingStore.ReplaceChunksAsync(
            document.Id,
            [
                new DocumentChunk(
                0,
                "Security policy content.")
            ]);

        var searchStore =
            CreateSearchStore(connection);

        IReadOnlyList<SearchDocumentsResult> results =
            await searchStore.SearchAsync(
                "SECURITY");

        SearchDocumentsResult result =
            Assert.Single(results);

        Assert.Equal(
            document.Id,
            result.DocumentId);

        Assert.Equal(
            0,
            result.ChunkOrder);
    }

    [Fact]
    public async Task SearchAsync_WhenNoChunkMatches_ReturnsEmpty()
    {
        await using SqliteConnection connection =
            CreateConnection();

        Document document =
            CreateAndPersistDocument(
                connection,
                "document.txt",
                "Test Document");

        var processingStore =
            CreateProcessingStore(connection);

        await processingStore.ReplaceChunksAsync(
            document.Id,
            [
                new DocumentChunk(
                    0,
                    "This document contains ordinary content.")
            ]);

        var searchStore =
            CreateSearchStore(connection);

        IReadOnlyList<SearchDocumentsResult> results =
            await searchStore.SearchAsync(
                "does-not-exist");

        Assert.Empty(
            results);
    }

    [Fact]
    public async Task SearchAsync_ReturnsResultsInDocumentAndChunkOrder()
    {
        await using SqliteConnection connection =
            CreateConnection();

        Document firstDocument =
            CreateAndPersistDocument(
                connection,
                "first.txt",
                "Alpha Document");

        Document secondDocument =
            CreateAndPersistDocument(
                connection,
                "second.txt",
                "Beta Document");

        var processingStore =
            CreateProcessingStore(connection);

        await processingStore.ReplaceChunksAsync(
            secondDocument.Id,
            [
                new DocumentChunk(
                    1,
                    "Beta matching content second."),

                new DocumentChunk(
                    0,
                    "Beta matching content first.")
            ]);

        await processingStore.ReplaceChunksAsync(
            firstDocument.Id,
            [
                new DocumentChunk(
                    1,
                    "Alpha matching content second."),

                new DocumentChunk(
                    0,
                    "Alpha matching content first.")
            ]);

        var searchStore =
            CreateSearchStore(connection);

        IReadOnlyList<SearchDocumentsResult> results =
            await searchStore.SearchAsync(
                "matching");

        Assert.Equal(
            4,
            results.Count);

        AssertResult(
            results[0],
            firstDocument,
            0,
            "Alpha matching content first.");

        AssertResult(
            results[1],
            firstDocument,
            1,
            "Alpha matching content second.");

        AssertResult(
            results[2],
            secondDocument,
            0,
            "Beta matching content first.");

        AssertResult(
            results[3],
            secondDocument,
            1,
            "Beta matching content second.");
    }

    [Fact]
    public async Task SearchAsync_WhenSearchTextIsWhitespace_ThrowsArgumentException()
    {
        await using SqliteConnection connection =
            CreateConnection();

        var searchStore =
            CreateSearchStore(connection);

        await Assert.ThrowsAsync<ArgumentException>(
            () =>
                searchStore.SearchAsync(
                    "   "));
    }

    [Fact]
    public async Task SearchAsync_WhenCancellationRequested_ThrowsOperationCanceledException()
    {
        await using SqliteConnection connection =
            CreateConnection();

        var searchStore =
            CreateSearchStore(connection);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                searchStore.SearchAsync(
                    "matching",
                    cancellationTokenSource.Token));
    }

    private static void AssertResult(
        SearchDocumentsResult result,
        Document expectedDocument,
        int expectedChunkOrder,
        string expectedChunkText)
    {
        Assert.Equal(
            expectedDocument.Id,
            result.DocumentId);

        Assert.Equal(
            expectedDocument.FileName,
            result.FileName);

        Assert.Equal(
            expectedDocument.DisplayName,
            result.DisplayName);

        Assert.Equal(
            expectedChunkOrder,
            result.ChunkOrder);

        Assert.Equal(
            expectedChunkText,
            result.ChunkText);
    }

    private static SqliteDocumentProcessingStore CreateProcessingStore(
        SqliteConnection connection)
    {
        return new SqliteDocumentProcessingStore(
            CreateFactory(connection),
            NullLogger<SqliteDocumentProcessingStore>.Instance);
    }

    private static SqliteDocumentSearchStore CreateSearchStore(
        SqliteConnection connection)
    {
        return new SqliteDocumentSearchStore(
            CreateFactory(connection),
            NullLogger<SqliteDocumentSearchStore>.Instance);
    }

    private static Document CreateAndPersistDocument(
        SqliteConnection connection,
        string fileName,
        string displayName)
    {
        Document document =
            Document.Create(
                Guid.NewGuid(),
                fileName,
                displayName,
                $"hash-{Guid.NewGuid():N}",
                $"{Guid.NewGuid():N}.dvault");

        using var context =
            CreateContext(connection);

        context.Documents.Add(
            new DocumentEntity
            {
                Id = document.Id,
                FileName = document.FileName,
                DisplayName = document.DisplayName,
                Sha256Hash = document.Sha256Hash,
                ImportedAt = document.ImportedAt,
                Status = (int)document.Status,
                StoredFilePath = document.StoredFilePath
            });

        context.SaveChanges();

        return document;
    }

    private static SqliteConnection CreateConnection()
    {
        var connection =
            new SqliteConnection(
                "Data Source=:memory:");

        connection.Open();

        using var context =
            CreateContext(connection);

        context.Database.EnsureCreated();

        return connection;
    }

    private static DeskVaultDbContext CreateContext(
        SqliteConnection connection)
    {
        DbContextOptions<DeskVaultDbContext> options =
            new DbContextOptionsBuilder<DeskVaultDbContext>()
                .UseSqlite(connection)
                .Options;

        return new DeskVaultDbContext(
            options);
    }

    private static IDbContextFactory<DeskVaultDbContext> CreateFactory(
        SqliteConnection connection)
    {
        return new TestDbContextFactory(
            connection);
    }

    private sealed class TestDbContextFactory
        : IDbContextFactory<DeskVaultDbContext>
    {
        private readonly SqliteConnection _connection;

        public TestDbContextFactory(
            SqliteConnection connection)
        {
            _connection = connection;
        }

        public DeskVaultDbContext CreateDbContext()
        {
            return CreateContext();
        }

        public Task<DeskVaultDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                CreateContext());
        }

        private DeskVaultDbContext CreateContext()
        {
            return SqliteDocumentSearchStoreTests.CreateContext(
                _connection);
        }
    }
}
