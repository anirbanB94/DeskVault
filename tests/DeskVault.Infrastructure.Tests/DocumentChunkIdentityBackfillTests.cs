using DeskVault.Application.Documents.Chunking;
using DeskVault.Domain.Documents;
using DeskVault.Infrastructure.Persistence;
using DeskVault.Infrastructure.Persistence.Context;
using DeskVault.Infrastructure.Persistence.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DeskVault.Infrastructure.Tests;

public sealed class DocumentChunkIdentityBackfillTests
{
    private static readonly Guid DocumentId =
        Guid.Parse(
            "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    [Fact]
    public async Task BackfillAsync_ConvertsLegacyChunksToStableIdentityAndProvenance()
    {
        // Arrange
        await using SqliteConnection connection =
            CreateConnection();

        Document document =
            CreateAndPersistDocument(
                connection);

        Guid legacyId =
            Guid.Parse(
                "11111111-1111-1111-1111-111111111111");

        const string text =
            "Legacy canonical chunk.";

        await InsertChunkAsync(
            connection,
            new DocumentChunkEntity
            {
                Id = legacyId,
                DocumentId = document.Id,
                Order = 0,
                Text = text,
                ContentHash = string.Empty,
                ProcessingGeneration = 0L
            });

        var backfill =
            CreateBackfill(connection);

        // Act
        await backfill.BackfillAsync();

        DocumentChunkEntity chunk =
            await GetSingleChunkAsync(
                connection);

        // Assert
        Assert.Equal(
            DocumentChunkIdentity.CreateLogicalId(
                document.Id,
                0),
            chunk.Id);

        Assert.NotEqual(
            legacyId,
            chunk.Id);

        Assert.Equal(
            document.Id,
            chunk.DocumentId);

        Assert.Equal(
            0,
            chunk.Order);

        Assert.Equal(
            text,
            chunk.Text);

        Assert.Equal(
            DocumentChunkIdentity.ComputeContentHash(
                text),
            chunk.ContentHash);

        Assert.Equal(
            0L,
            chunk.ProcessingGeneration);
    }

    [Fact]
    public async Task BackfillAsync_PreservesAllLegacyChunksAndTheirOrder()
    {
        // Arrange
        await using SqliteConnection connection =
            CreateConnection();

        Document document =
            CreateAndPersistDocument(
                connection);

        const string firstText =
            "First legacy chunk.";

        const string secondText =
            "Second legacy chunk.";

        await InsertChunkAsync(
            connection,
            new DocumentChunkEntity
            {
                Id = Guid.NewGuid(),
                DocumentId = document.Id,
                Order = 0,
                Text = firstText,
                ContentHash = string.Empty,
                ProcessingGeneration = 0L
            });

        await InsertChunkAsync(
            connection,
            new DocumentChunkEntity
            {
                Id = Guid.NewGuid(),
                DocumentId = document.Id,
                Order = 1,
                Text = secondText,
                ContentHash = string.Empty,
                ProcessingGeneration = 0L
            });

        var backfill =
            CreateBackfill(connection);

        // Act
        await backfill.BackfillAsync();

        List<DocumentChunkEntity> chunks =
            await GetChunksAsync(
                connection);

        // Assert
        Assert.Equal(
            2,
            chunks.Count);

        Assert.Equal(
            0,
            chunks[0].Order);

        Assert.Equal(
            firstText,
            chunks[0].Text);

        Assert.Equal(
            DocumentChunkIdentity.CreateLogicalId(
                document.Id,
                0),
            chunks[0].Id);

        Assert.Equal(
            DocumentChunkIdentity.ComputeContentHash(
                firstText),
            chunks[0].ContentHash);

        Assert.Equal(
            1,
            chunks[1].Order);

        Assert.Equal(
            secondText,
            chunks[1].Text);

        Assert.Equal(
            DocumentChunkIdentity.CreateLogicalId(
                document.Id,
                1),
            chunks[1].Id);

        Assert.Equal(
            DocumentChunkIdentity.ComputeContentHash(
                secondText),
            chunks[1].ContentHash);

        Assert.All(
            chunks,
            chunk =>
                Assert.Equal(
                    0L,
                    chunk.ProcessingGeneration));
    }

    [Fact]
    public async Task BackfillAsync_WhenRunAgain_IsIdempotent()
    {
        // Arrange
        await using SqliteConnection connection =
            CreateConnection();

        Document document =
            CreateAndPersistDocument(
                connection);

        const string text =
            "Legacy canonical chunk.";

        await InsertChunkAsync(
            connection,
            new DocumentChunkEntity
            {
                Id = Guid.NewGuid(),
                DocumentId = document.Id,
                Order = 0,
                Text = text,
                ContentHash = string.Empty,
                ProcessingGeneration = 0L
            });

        var backfill =
            CreateBackfill(connection);

        await backfill.BackfillAsync();

        DocumentChunkEntity firstChunk =
            await GetSingleChunkAsync(
                connection);

        // Act
        await backfill.BackfillAsync();

        DocumentChunkEntity secondChunk =
            await GetSingleChunkAsync(
                connection);

        // Assert
        Assert.Equal(
            firstChunk.Id,
            secondChunk.Id);

        Assert.Equal(
            firstChunk.DocumentId,
            secondChunk.DocumentId);

        Assert.Equal(
            firstChunk.Order,
            secondChunk.Order);

        Assert.Equal(
            firstChunk.Text,
            secondChunk.Text);

        Assert.Equal(
            firstChunk.ContentHash,
            secondChunk.ContentHash);

        Assert.Equal(
            firstChunk.ProcessingGeneration,
            secondChunk.ProcessingGeneration);
    }

    [Fact]
    public async Task BackfillAsync_WhenThereAreNoChunks_DoesNothing()
    {
        // Arrange
        await using SqliteConnection connection =
            CreateConnection();

        CreateAndPersistDocument(
            connection);

        var backfill =
            CreateBackfill(connection);

        // Act
        await backfill.BackfillAsync();

        List<DocumentChunkEntity> chunks =
            await GetChunksAsync(
                connection);

        // Assert
        Assert.Empty(
            chunks);
    }

    [Fact]
    public async Task BackfillAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        await using SqliteConnection connection =
            CreateConnection();

        Document document =
            CreateAndPersistDocument(
                connection);

        await InsertChunkAsync(
            connection,
            new DocumentChunkEntity
            {
                Id = Guid.NewGuid(),
                DocumentId = document.Id,
                Order = 0,
                Text = "Legacy chunk.",
                ContentHash = string.Empty,
                ProcessingGeneration = 0L
            });

        var backfill =
            CreateBackfill(connection);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        // Act
        Func<Task> act =
            () =>
                backfill.BackfillAsync(
                    cancellationTokenSource.Token);

        // Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            act);
    }

    private static DocumentChunkIdentityBackfill CreateBackfill(
        SqliteConnection connection)
    {
        return new DocumentChunkIdentityBackfill(
            new TestDbContextFactory(
                connection));
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

    private static async Task InsertChunkAsync(
        SqliteConnection connection,
        DocumentChunkEntity chunk)
    {
        await using DeskVaultDbContext context =
            CreateContext(connection);

        context.DocumentChunks.Add(
            chunk);

        await context.SaveChangesAsync();
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
            return DocumentChunkIdentityBackfillTests
                .CreateContext(
                    _connection);
        }
    }
}
