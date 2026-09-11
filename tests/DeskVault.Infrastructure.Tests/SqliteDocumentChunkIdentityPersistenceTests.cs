using DeskVault.Application.Documents.Chunking;
using DeskVault.Application.Documents.Processing;
using DeskVault.Domain.Documents;
using DeskVault.Infrastructure.Persistence.Context;
using DeskVault.Infrastructure.Persistence.Entities;
using DeskVault.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeskVault.Infrastructure.Tests;

public sealed class SqliteDocumentChunkIdentityPersistenceTests
{
    private static readonly Guid DocumentId =
        Guid.Parse(
            "11111111-2222-3333-4444-555555555555");

    [Fact]
    public async Task ReplaceChunksAsync_PersistsDeterministicIdentityAndProvenance()
    {
        // Arrange
        await using SqliteConnection connection =
            CreateConnection();

        Document document =
            CreateAndPersistDocument(
                connection);

        var store =
            CreateStore(connection);

        const int order = 2;
        const string text =
            "Canonical chunk text.";

        // Act
        await store.ReplaceChunksAsync(
            document.Id,
            document.ProcessingGeneration,
            [
                new DocumentChunk(
                    order,
                    text)
            ]);

        DocumentChunkEntity chunk =
            await GetSingleChunkAsync(
                connection);

        // Assert
        Assert.Equal(
            DocumentChunkIdentity.CreateLogicalId(
                document.Id,
                order),
            chunk.Id);

        Assert.Equal(
            document.Id,
            chunk.DocumentId);

        Assert.Equal(
            order,
            chunk.Order);

        Assert.Equal(
            text,
            chunk.Text);

        Assert.Equal(
            DocumentChunkIdentity.ComputeContentHash(
                text),
            chunk.ContentHash);

        Assert.Equal(
            document.ProcessingGeneration,
            chunk.ProcessingGeneration);
    }

    [Fact]
    public async Task ReplaceChunksAsync_WhenEquivalentChunksAreReprocessed_PreservesLogicalIdentity()
    {
        // Arrange
        await using SqliteConnection connection =
            CreateConnection();

        Document document =
            CreateAndPersistDocument(
                connection);

        var store =
            CreateStore(connection);

        const string text =
            "Canonical chunk text.";

        await store.ReplaceChunksAsync(
            document.Id,
            document.ProcessingGeneration,
            [
                new DocumentChunk(
                    0,
                    text)
            ]);

        DocumentChunkEntity firstChunk =
            await GetSingleChunkAsync(
                connection);

        long secondGeneration =
            await store.AcquireProcessingGenerationAsync(
                document.Id);

        // Act
        await store.ReplaceChunksAsync(
            document.Id,
            secondGeneration,
            [
                new DocumentChunk(
                    0,
                    text)
            ]);

        DocumentChunkEntity secondChunk =
            await GetSingleChunkAsync(
                connection);

        // Assert
        Assert.Equal(
            firstChunk.Id,
            secondChunk.Id);

        Assert.Equal(
            firstChunk.ContentHash,
            secondChunk.ContentHash);

        Assert.Equal(
            document.Id,
            secondChunk.DocumentId);

        Assert.Equal(
            0,
            secondChunk.Order);

        Assert.Equal(
            text,
            secondChunk.Text);

        Assert.Equal(
            secondGeneration,
            secondChunk.ProcessingGeneration);
    }

    [Fact]
    public async Task ReplaceChunksAsync_WhenChunkTextChanges_PreservesLogicalIdentityAndChangesContentIdentity()
    {
        // Arrange
        await using SqliteConnection connection =
            CreateConnection();

        Document document =
            CreateAndPersistDocument(
                connection);

        var store =
            CreateStore(connection);

        const string firstText =
            "Original canonical chunk text.";

        const string secondText =
            "Changed canonical chunk text.";

        await store.ReplaceChunksAsync(
            document.Id,
            document.ProcessingGeneration,
            [
                new DocumentChunk(
                    0,
                    firstText)
            ]);

        DocumentChunkEntity firstChunk =
            await GetSingleChunkAsync(
                connection);

        long secondGeneration =
            await store.AcquireProcessingGenerationAsync(
                document.Id);

        // Act
        await store.ReplaceChunksAsync(
            document.Id,
            secondGeneration,
            [
                new DocumentChunk(
                    0,
                    secondText)
            ]);

        DocumentChunkEntity secondChunk =
            await GetSingleChunkAsync(
                connection);

        // Assert
        Assert.Equal(
            firstChunk.Id,
            secondChunk.Id);

        Assert.NotEqual(
            firstChunk.ContentHash,
            secondChunk.ContentHash);

        Assert.Equal(
            DocumentChunkIdentity.ComputeContentHash(
                firstText),
            firstChunk.ContentHash);

        Assert.Equal(
            DocumentChunkIdentity.ComputeContentHash(
                secondText),
            secondChunk.ContentHash);

        Assert.Equal(
            secondText,
            secondChunk.Text);

        Assert.Equal(
            secondGeneration,
            secondChunk.ProcessingGeneration);
    }

    [Fact]
    public async Task ReplaceChunksAsync_WhenChunkTextMovesToAnotherOrder_UsesOrderBasedLogicalIdentity()
    {
        // Arrange
        await using SqliteConnection connection =
            CreateConnection();

        Document document =
            CreateAndPersistDocument(
                connection);

        var store =
            CreateStore(connection);

        const string firstText =
            "First chunk.";

        const string secondText =
            "Second chunk.";

        await store.ReplaceChunksAsync(
            document.Id,
            document.ProcessingGeneration,
            [
                new DocumentChunk(
                    0,
                    firstText),
                new DocumentChunk(
                    1,
                    secondText)
            ]);

        List<DocumentChunkEntity> firstChunks =
            await GetChunksAsync(
                connection);

        DocumentChunkEntity firstOrderZero =
            Assert.Single(
                firstChunks,
                chunk => chunk.Order == 0);

        DocumentChunkEntity firstOrderOne =
            Assert.Single(
                firstChunks,
                chunk => chunk.Order == 1);

        long secondGeneration =
            await store.AcquireProcessingGenerationAsync(
                document.Id);

        // Act
        await store.ReplaceChunksAsync(
            document.Id,
            secondGeneration,
            [
                new DocumentChunk(
                    0,
                    secondText),
                new DocumentChunk(
                    1,
                    firstText)
            ]);

        List<DocumentChunkEntity> secondChunks =
            await GetChunksAsync(
                connection);

        // Assert
        DocumentChunkEntity secondOrderZero =
            Assert.Single(
                secondChunks,
                chunk => chunk.Order == 0);

        DocumentChunkEntity secondOrderOne =
            Assert.Single(
                secondChunks,
                chunk => chunk.Order == 1);

        Assert.Equal(
            firstOrderZero.Id,
            secondOrderZero.Id);

        Assert.Equal(
            firstOrderOne.Id,
            secondOrderOne.Id);

        Assert.Equal(
            DocumentChunkIdentity.CreateLogicalId(
                document.Id,
                0),
            secondOrderZero.Id);

        Assert.Equal(
            DocumentChunkIdentity.CreateLogicalId(
                document.Id,
                1),
            secondOrderOne.Id);

        Assert.Equal(
            secondText,
            secondOrderZero.Text);

        Assert.Equal(
            firstText,
            secondOrderOne.Text);

        Assert.Equal(
            DocumentChunkIdentity.ComputeContentHash(
                secondText),
            secondOrderZero.ContentHash);

        Assert.Equal(
            DocumentChunkIdentity.ComputeContentHash(
                firstText),
            secondOrderOne.ContentHash);

        Assert.Equal(
            secondGeneration,
            secondOrderZero.ProcessingGeneration);

        Assert.Equal(
            secondGeneration,
            secondOrderOne.ProcessingGeneration);
    }

    [Fact]
    public async Task ReplaceChunksAsync_PersistsEachChunkWithItsOwnGenerationAndContentHash()
    {
        // Arrange
        await using SqliteConnection connection =
            CreateConnection();

        Document document =
            CreateAndPersistDocument(
                connection);

        var store =
            CreateStore(connection);

        const string firstText =
            "First canonical chunk.";

        const string secondText =
            "Second canonical chunk.";

        long generation =
            document.ProcessingGeneration;

        // Act
        await store.ReplaceChunksAsync(
            document.Id,
            generation,
            [
                new DocumentChunk(
                    0,
                    firstText),
                new DocumentChunk(
                    1,
                    secondText)
            ]);

        List<DocumentChunkEntity> chunks =
            await GetChunksAsync(
                connection);

        // Assert
        Assert.Equal(
            2,
            chunks.Count);

        DocumentChunkEntity firstChunk =
            Assert.Single(
                chunks,
                chunk => chunk.Order == 0);

        DocumentChunkEntity secondChunk =
            Assert.Single(
                chunks,
                chunk => chunk.Order == 1);

        Assert.Equal(
            DocumentChunkIdentity.CreateLogicalId(
                document.Id,
                0),
            firstChunk.Id);

        Assert.Equal(
            DocumentChunkIdentity.CreateLogicalId(
                document.Id,
                1),
            secondChunk.Id);

        Assert.Equal(
            DocumentChunkIdentity.ComputeContentHash(
                firstText),
            firstChunk.ContentHash);

        Assert.Equal(
            DocumentChunkIdentity.ComputeContentHash(
                secondText),
            secondChunk.ContentHash);

        Assert.Equal(
            generation,
            firstChunk.ProcessingGeneration);

        Assert.Equal(
            generation,
            secondChunk.ProcessingGeneration);
    }

    private static SqliteDocumentProcessingStore CreateStore(
        SqliteConnection connection)
    {
        return new SqliteDocumentProcessingStore(
            CreateFactory(connection),
            NullLogger<SqliteDocumentProcessingStore>.Instance);
    }

    private static Document CreateAndPersistDocument(
        SqliteConnection connection)
    {
        Document document =
            Document.Create(
                DocumentId,
                "document.txt",
                "Test Document",
                "hash-test",
                "document.dvault");

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
                StoredFilePath = document.StoredFilePath,
                ProcessingGeneration =
                    document.ProcessingGeneration,
                LastSuccessfulProcessingGeneration =
                    document.LastSuccessfulProcessingGeneration
            });

        context.SaveChanges();

        return document;
    }

    private static async Task<DocumentChunkEntity> GetSingleChunkAsync(
        SqliteConnection connection)
    {
        List<DocumentChunkEntity> chunks =
            await GetChunksAsync(
                connection);

        return Assert.Single(
            chunks);
    }

    private static async Task<List<DocumentChunkEntity>> GetChunksAsync(
        SqliteConnection connection)
    {
        await using DeskVaultDbContext context =
            CreateContext(connection);

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
            CreateContext(connection);

        context.Database.EnsureCreated();

        return connection;
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
            return SqliteDocumentChunkIdentityPersistenceTests
                .CreateContext(
                    _connection);
        }
    }
}
