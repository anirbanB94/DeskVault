using DeskVault.Application.Documents.Chunking;
using DeskVault.Application.Documents.Queries.SearchDocuments;
using DeskVault.Domain.Documents;
using DeskVault.Infrastructure.Persistence.Context;
using DeskVault.Infrastructure.Persistence.Entities;
using DeskVault.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

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

        IDbContextFactory<DeskVaultDbContext> factory =
            CreateFactory(connection);

        var processingStore =
            new SqliteDocumentProcessingStore(factory);

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
            new SqliteDocumentSearchStore(factory);

        IReadOnlyList<SearchDocumentsResult> results =
            await searchStore.SearchAsync("searchable");

        Assert.Single(results);

        SearchDocumentsResult result =
            results[0];

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
    public async Task SearchAsync_WhenNoChunkMatches_ReturnsEmpty()
    {
        await using SqliteConnection connection =
            CreateConnection();

        Document document =
            CreateAndPersistDocument(
                connection,
                "document.txt",
                "Test Document");

        IDbContextFactory<DeskVaultDbContext> factory =
            CreateFactory(connection);

        var processingStore =
            new SqliteDocumentProcessingStore(factory);

        await processingStore.ReplaceChunksAsync(
            document.Id,
            [
                new DocumentChunk(
                    0,
                    "This document contains ordinary content.")
            ]);

        var searchStore =
            new SqliteDocumentSearchStore(factory);

        IReadOnlyList<SearchDocumentsResult> results =
            await searchStore.SearchAsync(
                "does-not-exist");

        Assert.Empty(results);
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

        IDbContextFactory<DeskVaultDbContext> factory =
            CreateFactory(connection);

        var processingStore =
            new SqliteDocumentProcessingStore(factory);

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
            new SqliteDocumentSearchStore(factory);

        IReadOnlyList<SearchDocumentsResult> results =
            await searchStore.SearchAsync("matching");

        Assert.Equal(
            4,
            results.Count);

        Assert.Equal(
            firstDocument.Id,
            results[0].DocumentId);

        Assert.Equal(
            0,
            results[0].ChunkOrder);

        Assert.Equal(
            firstDocument.Id,
            results[1].DocumentId);

        Assert.Equal(
            1,
            results[1].ChunkOrder);

        Assert.Equal(
            secondDocument.Id,
            results[2].DocumentId);

        Assert.Equal(
            0,
            results[2].ChunkOrder);

        Assert.Equal(
            secondDocument.Id,
            results[3].DocumentId);

        Assert.Equal(
            1,
            results[3].ChunkOrder);
    }

    [Fact]
    public async Task SearchAsync_WhenSearchTextIsWhitespace_ThrowsArgumentException()
    {
        await using SqliteConnection connection =
            CreateConnection();

        IDbContextFactory<DeskVaultDbContext> factory =
            CreateFactory(connection);

        var searchStore =
            new SqliteDocumentSearchStore(factory);

        await Assert.ThrowsAsync<ArgumentException>(
            () =>
                searchStore.SearchAsync("   "));
    }

    [Fact]
    public async Task SearchAsync_WhenCancellationRequested_ThrowsOperationCanceledException()
    {
        await using SqliteConnection connection =
            CreateConnection();

        IDbContextFactory<DeskVaultDbContext> factory =
            CreateFactory(connection);

        var searchStore =
            new SqliteDocumentSearchStore(factory);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                searchStore.SearchAsync(
                    "matching",
                    cancellationTokenSource.Token));
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
            new DeskVaultDbContext(
                new DbContextOptionsBuilder<DeskVaultDbContext>()
                    .UseSqlite(connection)
                    .Options);

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
            new DeskVaultDbContext(
                new DbContextOptionsBuilder<DeskVaultDbContext>()
                    .UseSqlite(connection)
                    .Options);

        context.Database.EnsureCreated();

        return connection;
    }

    private static IDbContextFactory<DeskVaultDbContext> CreateFactory(
        SqliteConnection connection)
    {
        return new TestDbContextFactory(connection);
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
            return Task.FromResult(
                CreateContext());
        }

        private DeskVaultDbContext CreateContext()
        {
            DbContextOptions<DeskVaultDbContext> options =
                new DbContextOptionsBuilder<DeskVaultDbContext>()
                    .UseSqlite(_connection)
                    .Options;

            return new DeskVaultDbContext(options);
        }
    }
}
