using DeskVault.Application.Documents.Chunking;
using DeskVault.Domain.Documents;
using DeskVault.Infrastructure.Persistence.Context;
using DeskVault.Infrastructure.Persistence.Entities;
using DeskVault.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeskVault.Infrastructure.Tests;

public sealed class SqliteDocumentProcessingStoreTests
{
    [Fact]
    public async Task ReplaceChunksAsync_PersistsChunks()
    {
        await using SqliteConnection connection =
            CreateConnection();

        Document document =
            CreateAndPersistDocument(connection);

        IDbContextFactory<DeskVaultDbContext> factory =
            CreateFactory(connection);

        var store =
            new SqliteDocumentProcessingStore(factory, NullLogger<SqliteDocumentProcessingStore>.Instance);

        await store.ReplaceChunksAsync(
            document.Id,
            [
                new DocumentChunk(
                    0,
                    "First chunk."),

                new DocumentChunk(
                    1,
                    "Second chunk.")
            ]);

        List<DocumentChunkEntity> chunks =
            await GetChunksAsync(connection);

        Assert.Equal(
            2,
            chunks.Count);

        Assert.Equal(
            document.Id,
            chunks[0].DocumentId);

        Assert.Equal(
            0,
            chunks[0].Order);

        Assert.Equal(
            "First chunk.",
            chunks[0].Text);

        Assert.Equal(
            document.Id,
            chunks[1].DocumentId);

        Assert.Equal(
            1,
            chunks[1].Order);

        Assert.Equal(
            "Second chunk.",
            chunks[1].Text);
    }

    [Fact]
    public async Task ReplaceChunksAsync_WhenCalledAgain_ReplacesPreviousChunks()
    {
        await using SqliteConnection connection =
            CreateConnection();

        Document document =
            CreateAndPersistDocument(connection);

        IDbContextFactory<DeskVaultDbContext> factory =
            CreateFactory(connection);

        var store =
            new SqliteDocumentProcessingStore(factory, NullLogger<SqliteDocumentProcessingStore>.Instance);

        await store.ReplaceChunksAsync(
            document.Id,
            [
                new DocumentChunk(
                    0,
                    "Old first chunk."),

                new DocumentChunk(
                    1,
                    "Old second chunk.")
            ]);

        await store.ReplaceChunksAsync(
            document.Id,
            [
                new DocumentChunk(
                    0,
                    "New first chunk."),

                new DocumentChunk(
                    1,
                    "New second chunk."),

                new DocumentChunk(
                    2,
                    "New third chunk.")
            ]);

        List<DocumentChunkEntity> chunks =
            await GetChunksAsync(connection);

        Assert.Equal(
            3,
            chunks.Count);

        Assert.Equal(
            0,
            chunks[0].Order);

        Assert.Equal(
            "New first chunk.",
            chunks[0].Text);

        Assert.Equal(
            1,
            chunks[1].Order);

        Assert.Equal(
            "New second chunk.",
            chunks[1].Text);

        Assert.Equal(
            2,
            chunks[2].Order);

        Assert.Equal(
            "New third chunk.",
            chunks[2].Text);

        Assert.DoesNotContain(
            chunks,
            chunk =>
                chunk.Text == "Old first chunk.");

        Assert.DoesNotContain(
            chunks,
            chunk =>
                chunk.Text == "Old second chunk.");
    }


    [Fact]
    public async Task ReplaceChunksAsync_WhenReplacementFails_RollsBackToPreviousChunks()
    {
        await using SqliteConnection connection =
            CreateConnection();

        Document document =
            CreateAndPersistDocument(connection);

        IDbContextFactory<DeskVaultDbContext> factory =
            CreateFactory(connection);

        var store =
            new SqliteDocumentProcessingStore(factory, NullLogger<SqliteDocumentProcessingStore>.Instance);

        await store.ReplaceChunksAsync(
            document.Id,
            [
                new DocumentChunk(
                    0,
                    "Original first chunk."),

                new DocumentChunk(
                    1,
                    "Original second chunk.")
            ]);

        await Assert.ThrowsAnyAsync<Exception>(
            () =>
                store.ReplaceChunksAsync(
                    document.Id,
                    [
                        new DocumentChunk(
                            0,
                            "Replacement chunk."),

                        new DocumentChunk(
                            0,
                            "Duplicate order chunk.")
                    ]));

        List<DocumentChunkEntity> chunks =
            await GetChunksAsync(connection);

        Assert.Equal(
            2,
            chunks.Count);

        Assert.Equal(
            0,
            chunks[0].Order);

        Assert.Equal(
            "Original first chunk.",
            chunks[0].Text);

        Assert.Equal(
            1,
            chunks[1].Order);

        Assert.Equal(
            "Original second chunk.",
            chunks[1].Text);
    }
    [Fact]
    public async Task ReplaceChunksAsync_PreservesChunkOrderAndText()
    {
        await using SqliteConnection connection =
            CreateConnection();

        Document document =
            CreateAndPersistDocument(connection);

        IDbContextFactory<DeskVaultDbContext> factory =
            CreateFactory(connection);

        var store =
            new SqliteDocumentProcessingStore(factory, NullLogger<SqliteDocumentProcessingStore>.Instance);

        IReadOnlyList<DocumentChunk> expected =
        [
            new DocumentChunk(
                0,
                "Introduction."),

            new DocumentChunk(
                1,
                "Architecture and design."),

            new DocumentChunk(
                2,
                "Implementation details.")
        ];

        await store.ReplaceChunksAsync(
            document.Id,
            expected);

        List<DocumentChunkEntity> chunks =
            await GetChunksAsync(connection);

        Assert.Equal(
            expected.Count,
            chunks.Count);

        for (int index = 0;
             index < expected.Count;
             index++)
        {
            Assert.Equal(
                document.Id,
                chunks[index].DocumentId);

            Assert.Equal(
                expected[index].Order,
                chunks[index].Order);

            Assert.Equal(
                expected[index].Text,
                chunks[index].Text);
        }
    }

    [Fact]
    public async Task ReplaceChunksAsync_WhenChunksAreEmpty_RemovesExistingChunks()
    {
        await using SqliteConnection connection =
            CreateConnection();

        Document document =
            CreateAndPersistDocument(connection);

        IDbContextFactory<DeskVaultDbContext> factory =
            CreateFactory(connection);

        var store =
            new SqliteDocumentProcessingStore(factory, NullLogger<SqliteDocumentProcessingStore>.Instance);

        await store.ReplaceChunksAsync(
            document.Id,
            [
                new DocumentChunk(
                    0,
                    "Existing chunk.")
            ]);

        await store.ReplaceChunksAsync(
            document.Id,
            []);

        List<DocumentChunkEntity> chunks =
            await GetChunksAsync(connection);

        Assert.Empty(chunks);
    }

    [Fact]
    public async Task ReplaceChunksAsync_WhenCancellationRequested_ThrowsOperationCanceledException()
    {
        await using SqliteConnection connection =
            CreateConnection();

        Document document =
            CreateAndPersistDocument(connection);

        IDbContextFactory<DeskVaultDbContext> factory =
            CreateFactory(connection);

        var store =
            new SqliteDocumentProcessingStore(factory, NullLogger<SqliteDocumentProcessingStore>.Instance);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                store.ReplaceChunksAsync(
                    document.Id,
                    [
                        new DocumentChunk(
                            0,
                            "Cancelled chunk.")
                    ],
                    cancellationTokenSource.Token));
    }

    [Fact]
    public async Task DeleteDocument_WhenChunksExist_CascadesToChunks()
    {
        await using SqliteConnection connection =
            CreateConnection();

        Document document =
            CreateAndPersistDocument(connection);

        IDbContextFactory<DeskVaultDbContext> factory =
            CreateFactory(connection);

        var processingStore =
            new SqliteDocumentProcessingStore(factory, NullLogger<SqliteDocumentProcessingStore>.Instance);

        var documentRepository =
            new SqliteDocumentRepository(factory, NullLogger<SqliteDocumentRepository>.Instance);

        await processingStore.ReplaceChunksAsync(
            document.Id,
            [
                new DocumentChunk(
                    0,
                    "First chunk."),

                new DocumentChunk(
                    1,
                    "Second chunk.")
            ]);

        List<DocumentChunkEntity> beforeDelete =
            await GetChunksAsync(connection);

        Assert.Equal(
            2,
            beforeDelete.Count);

        await documentRepository.DeleteAsync(
            document.Id);

        List<DocumentChunkEntity> afterDelete =
            await GetChunksAsync(connection);

        Assert.Empty(afterDelete);
    }

    private static Document CreateAndPersistDocument(
        SqliteConnection connection)
    {
        Document document =
            Document.Create(
                Guid.NewGuid(),
                "document.txt",
                "Test Document",
                $"hash-{Guid.NewGuid():N}",
                "document.dvault");

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

    private static async Task<List<DocumentChunkEntity>> GetChunksAsync(
        SqliteConnection connection)
    {
        using var context =
            new DeskVaultDbContext(
                new DbContextOptionsBuilder<DeskVaultDbContext>()
                    .UseSqlite(connection)
                    .Options);

        return await context.DocumentChunks
            .AsNoTracking()
            .OrderBy(
                chunk => chunk.Order)
            .ToListAsync();
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
