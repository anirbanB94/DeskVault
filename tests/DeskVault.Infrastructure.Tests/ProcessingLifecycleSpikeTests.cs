using DeskVault.Application.Documents.Chunking;
using DeskVault.Domain.Documents;
using DeskVault.Infrastructure.Persistence.Context;
using DeskVault.Infrastructure.Persistence.Entities;
using DeskVault.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeskVault.Infrastructure.Tests;

public sealed class ProcessingLifecycleSpikeTests
{
    [Fact]
    public async Task Repository_AllowsOlderDocumentSnapshotToOverwriteNewerStatus()
    {
        await using SqliteConnection connection =
            CreateConnection();

        var repository =
            CreateRepository(connection);

        Document original =
            CreateDocument();

        await repository.AddAsync(
            original);

        Document attemptA =
            (await repository.GetByIdAsync(original.Id))!;

        Document attemptB =
            (await repository.GetByIdAsync(original.Id))!;

        attemptA.MarkProcessing();

        await repository.UpdateAsync(
            attemptA);

        attemptB.MarkProcessing();
        attemptB.MarkIndexed();
        attemptB.MarkAvailable();

        await repository.UpdateAsync(
            attemptB);

        attemptA.MarkFailed();

        await repository.UpdateAsync(
            attemptA);

        Document? finalDocument =
            await repository.GetByIdAsync(
                original.Id);

        Assert.NotNull(finalDocument);

        Assert.Equal(
            DocumentStatus.Failed,
            finalDocument.Status);
    }

    [Fact]
    public async Task ProcessingStore_AllowsOlderChunksToOverwriteNewerChunks()
    {
        await using SqliteConnection connection =
            CreateConnection();

        Document document =
            CreateDocument();

        var repository =
            CreateRepository(connection);

        await repository.AddAsync(
            document);

        var processingStore =
            CreateProcessingStore(connection);

        IReadOnlyList<DocumentChunk> newerChunks =
        [
            new DocumentChunk(
                0,
                "NEWER RESULT")
        ];

        IReadOnlyList<DocumentChunk> olderChunks =
        [
            new DocumentChunk(
                0,
                "OLDER RESULT")
        ];

        await processingStore.ReplaceChunksAsync(
            document.Id,
            0L,
            newerChunks);

        await processingStore.ReplaceChunksAsync(
            document.Id,
            0L,
            olderChunks);

        IReadOnlyList<DocumentChunkEntity> persistedChunks =
            await ReadChunksAsync(
                connection,
                document.Id);

        DocumentChunkEntity chunk =
            Assert.Single(
                persistedChunks);

        Assert.Equal(
            "OLDER RESULT",
            chunk.Text);
    }

    [Fact]
    public async Task ProcessingStore_WhenCancellationIsRequestedBeforeReplacement_DoesNotPersistChunks()
    {
        await using SqliteConnection connection =
            CreateConnection();

        Document document =
            CreateDocument();

        var repository =
            CreateRepository(connection);

        await repository.AddAsync(
            document);

        var processingStore =
            CreateProcessingStore(connection);

        using CancellationTokenSource cancellationTokenSource =
            new();

        cancellationTokenSource.Cancel();

        IReadOnlyList<DocumentChunk> chunks =
        [
            new DocumentChunk(
                0,
                "CANCELLED RESULT")
        ];

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => processingStore.ReplaceChunksAsync(
                document.Id,
                0L,
                chunks,
                cancellationTokenSource.Token));

        IReadOnlyList<DocumentChunkEntity> persistedChunks =
            await ReadChunksAsync(
                connection,
                document.Id);

        Assert.Empty(persistedChunks);
    }

    [Fact]
    public async Task ProcessingStore_RepeatedReplacement_IsDeterministic()
    {
        await using SqliteConnection connection =
            CreateConnection();

        Document document =
            CreateDocument();

        var repository =
            CreateRepository(connection);

        await repository.AddAsync(
            document);

        var processingStore =
            CreateProcessingStore(connection);

        IReadOnlyList<DocumentChunk> firstResult =
        [
            new DocumentChunk(
                0,
                "FIRST"),

            new DocumentChunk(
                1,
                "SECOND")
        ];

        IReadOnlyList<DocumentChunk> repeatedResult =
        [
            new DocumentChunk(
                0,
                "FIRST"),

            new DocumentChunk(
                1,
                "SECOND")
        ];

        await processingStore.ReplaceChunksAsync(
            document.Id,
            0L,
            firstResult);

        await processingStore.ReplaceChunksAsync(
            document.Id,
            0L,
            repeatedResult);

        IReadOnlyList<DocumentChunkEntity> persistedChunks =
            await ReadChunksAsync(
                connection,
                document.Id);

        Assert.Equal(
            2,
            persistedChunks.Count);

        Assert.Equal(
            "FIRST",
            persistedChunks[0].Text);

        Assert.Equal(
            "SECOND",
            persistedChunks[1].Text);

        Assert.Equal(
            [0, 1],
            persistedChunks.Select(chunk => chunk.Order).ToArray());
    }

    private static SqliteDocumentRepository CreateRepository(
        SqliteConnection connection)
    {
        return new SqliteDocumentRepository(
            CreateFactory(connection),
            NullLogger<SqliteDocumentRepository>.Instance);
    }

    private static SqliteDocumentProcessingStore CreateProcessingStore(
        SqliteConnection connection)
    {
        return new SqliteDocumentProcessingStore(
            CreateFactory(connection),
            NullLogger<SqliteDocumentProcessingStore>.Instance);
    }

    private static Document CreateDocument()
    {
        return Document.Create(
            Guid.NewGuid(),
            "document.txt",
            "Spike Document",
            $"spike-hash-{Guid.NewGuid():N}",
            "spike.dvault");
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

    private static async Task<IReadOnlyList<DocumentChunkEntity>> ReadChunksAsync(
        SqliteConnection connection,
        Guid documentId)
    {
        await using var context =
            CreateContext(connection);

        return await context.DocumentChunks
            .AsNoTracking()
            .Where(chunk => chunk.DocumentId == documentId)
            .OrderBy(chunk => chunk.Order)
            .ToListAsync();
    }

    private static IDbContextFactory<DeskVaultDbContext> CreateFactory(
        SqliteConnection connection)
    {
        return new TestDbContextFactory(
            connection);
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
            return ProcessingLifecycleSpikeTests.CreateContext(
                _connection);
        }
    }
}
