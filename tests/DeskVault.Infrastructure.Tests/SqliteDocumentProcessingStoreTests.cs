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

public sealed class SqliteDocumentProcessingStoreTests
{
    [Fact]
    public async Task AcquireProcessingGenerationAsync_WhenDocumentExists_ReturnsFirstGeneration()
    {
        await using SqliteConnection connection =
            CreateConnection();

        Document document =
            CreateAndPersistDocument(connection);

        var store =
            CreateStore(connection);

        long generation =
            await store.AcquireProcessingGenerationAsync(
                document.Id);

        Assert.Equal(
            1L,
            generation);

        long persistedGeneration =
            await GetProcessingGenerationAsync(
                connection,
                document.Id);

        Assert.Equal(
            1L,
            persistedGeneration);
    }

    [Fact]
    public async Task AcquireProcessingGenerationAsync_WhenCalledAgain_ReturnsNextGeneration()
    {
        await using SqliteConnection connection =
            CreateConnection();

        Document document =
            CreateAndPersistDocument(connection);

        var store =
            CreateStore(connection);

        long firstGeneration =
            await store.AcquireProcessingGenerationAsync(
                document.Id);

        long secondGeneration =
            await store.AcquireProcessingGenerationAsync(
                document.Id);

        Assert.Equal(
            1L,
            firstGeneration);

        Assert.Equal(
            2L,
            secondGeneration);

        long persistedGeneration =
            await GetProcessingGenerationAsync(
                connection,
                document.Id);

        Assert.Equal(
            2L,
            persistedGeneration);
    }

    [Fact]
    public async Task AcquireProcessingGenerationAsync_WhenDocumentDoesNotExist_ThrowsInvalidOperationException()
    {
        await using SqliteConnection connection =
            CreateConnection();

        var store =
            CreateStore(connection);

        Guid documentId =
            Guid.NewGuid();

        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () =>
                    store.AcquireProcessingGenerationAsync(
                        documentId));

        Assert.Contains(
            documentId.ToString(),
            exception.Message);
    }

    [Fact]
    public async Task AcquireProcessingGenerationAsync_WhenCancellationRequested_ThrowsOperationCanceledException()
    {
        await using SqliteConnection connection =
            CreateConnection();

        Document document =
            CreateAndPersistDocument(connection);

        var store =
            CreateStore(connection);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                store.AcquireProcessingGenerationAsync(
                    document.Id,
                    cancellationTokenSource.Token));

        long persistedGeneration =
            await GetProcessingGenerationAsync(
                connection,
                document.Id);

        Assert.Equal(
            0L,
            persistedGeneration);
    }

    [Fact]
    public async Task AcquireProcessingGenerationAsync_WhenCalledConcurrently_ReturnsUniqueSequentialGenerations()
    {
        string databaseName =
            $"DeskVaultProcessingGeneration-{Guid.NewGuid():N}";

        await using SqliteConnection setupConnection =
            CreateSharedMemoryConnection(
                databaseName);

        CreateDatabase(
            setupConnection);

        Document document =
            CreateAndPersistDocument(
                setupConnection);

        await using SqliteConnection firstConnection =
            CreateSharedMemoryConnection(
                databaseName);

        await using SqliteConnection secondConnection =
            CreateSharedMemoryConnection(
                databaseName);

        var firstStore =
            CreateStore(firstConnection);

        var secondStore =
            CreateStore(secondConnection);

        Task<long> firstTask =
            firstStore.AcquireProcessingGenerationAsync(
                document.Id);

        Task<long> secondTask =
            secondStore.AcquireProcessingGenerationAsync(
                document.Id);

        long[] generations =
            await Task.WhenAll(
                firstTask,
                secondTask);

        Assert.Equal(
            2,
            generations.Length);

        Assert.Contains(
            1L,
            generations);

        Assert.Contains(
            2L,
            generations);

        long persistedGeneration =
            await GetProcessingGenerationAsync(
                setupConnection,
                document.Id);

        Assert.Equal(
            2L,
            persistedGeneration);
    }

    [Fact]
    public async Task PublishProcessingStateAsync_WhenGenerationIsCurrent_PersistsState()
    {
        await using SqliteConnection connection =
            CreateConnection();

        Document document =
            CreateAndPersistDocument(connection);

        var store =
            CreateStore(connection);

        long generation =
            await store.AcquireProcessingGenerationAsync(
                document.Id);

        await store.PublishProcessingStateAsync(
            document.Id,
            generation,
            DocumentStatus.Available);

        DocumentStatus persistedStatus =
            await GetDocumentStatusAsync(
                connection,
                document.Id);

        Assert.Equal(
            DocumentStatus.Available,
            persistedStatus);
    }

    [Fact]
    public async Task PublishProcessingStateAsync_WhenGenerationIsStale_ThrowsAndPreservesExistingState()
    {
        await using SqliteConnection connection =
            CreateConnection();

        Document document =
            CreateAndPersistDocument(connection);

        var store =
            CreateStore(connection);

        long firstGeneration =
            await store.AcquireProcessingGenerationAsync(
                document.Id);

        await store.PublishProcessingStateAsync(
            document.Id,
            firstGeneration,
            DocumentStatus.Processing);

        long secondGeneration =
            await store.AcquireProcessingGenerationAsync(
                document.Id);

        Assert.Equal(
            2L,
            secondGeneration);

        await store.PublishProcessingStateAsync(
            document.Id,
            secondGeneration,
            DocumentStatus.Available);

        StaleProcessingGenerationException exception =
            await Assert.ThrowsAsync<StaleProcessingGenerationException>(
                () =>
                    store.PublishProcessingStateAsync(
                        document.Id,
                        firstGeneration,
                        DocumentStatus.Failed));

        Assert.Equal(
            document.Id,
            exception.DocumentId);

        Assert.Equal(
            firstGeneration,
            exception.ProcessingGeneration);

        Assert.Equal(
            secondGeneration,
            exception.CurrentGeneration);

        DocumentStatus persistedStatus =
            await GetDocumentStatusAsync(
                connection,
                document.Id);

        Assert.Equal(
            DocumentStatus.Available,
            persistedStatus);
    }

    [Fact]
    public async Task PublishProcessingStateAsync_WhenDocumentDoesNotExist_ThrowsInvalidOperationException()
    {
        await using SqliteConnection connection =
            CreateConnection();

        var store =
            CreateStore(connection);

        Guid documentId =
            Guid.NewGuid();

        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () =>
                    store.PublishProcessingStateAsync(
                        documentId,
                        1L,
                        DocumentStatus.Processing));

        Assert.Contains(
            documentId.ToString(),
            exception.Message);
    }

    [Fact]
    public async Task PublishProcessingStateAsync_WhenCancellationRequested_DoesNotChangeState()
    {
        await using SqliteConnection connection =
            CreateConnection();

        Document document =
            CreateAndPersistDocument(connection);

        var store =
            CreateStore(connection);

        long generation =
            await store.AcquireProcessingGenerationAsync(
                document.Id);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                store.PublishProcessingStateAsync(
                    document.Id,
                    generation,
                    DocumentStatus.Available,
                    cancellationTokenSource.Token));

        DocumentStatus persistedStatus =
            await GetDocumentStatusAsync(
                connection,
                document.Id);

        Assert.Equal(
            DocumentStatus.Imported,
            persistedStatus);
    }

    [Fact]
    public async Task PublishSuccessfulProcessingAsync_WhenGenerationIsCurrent_PublishesAvailableAndRecordsSuccessfulGeneration()
    {
        await using SqliteConnection connection =
            CreateConnection();

        Document document =
            CreateAndPersistDocument(connection);

        var store =
            CreateStore(connection);

        long generation =
            await store.AcquireProcessingGenerationAsync(
                document.Id);

        await store.PublishSuccessfulProcessingAsync(
            document.Id,
            generation);

        DocumentStatus persistedStatus =
            await GetDocumentStatusAsync(
                connection,
                document.Id);

        long persistedSuccessfulGeneration =
            await GetLastSuccessfulProcessingGenerationAsync(
                connection,
                document.Id);

        Assert.Equal(
            DocumentStatus.Available,
            persistedStatus);

        Assert.Equal(
            generation,
            persistedSuccessfulGeneration);
    }

    [Fact]
    public async Task PublishSuccessfulProcessingAsync_WhenGenerationIsStale_ThrowsAndPreservesExistingSuccessfulGeneration()
    {
        await using SqliteConnection connection =
            CreateConnection();

        Document document =
            CreateAndPersistDocument(connection);

        var store =
            CreateStore(connection);

        long firstGeneration =
            await store.AcquireProcessingGenerationAsync(
                document.Id);

        await store.PublishSuccessfulProcessingAsync(
            document.Id,
            firstGeneration);

        long secondGeneration =
            await store.AcquireProcessingGenerationAsync(
                document.Id);

        Assert.Equal(
            2L,
            secondGeneration);

        StaleProcessingGenerationException exception =
            await Assert.ThrowsAsync<StaleProcessingGenerationException>(
                () =>
                    store.PublishSuccessfulProcessingAsync(
                        document.Id,
                        firstGeneration));

        Assert.Equal(
            document.Id,
            exception.DocumentId);

        Assert.Equal(
            firstGeneration,
            exception.ProcessingGeneration);

        Assert.Equal(
            secondGeneration,
            exception.CurrentGeneration);

        DocumentStatus persistedStatus =
            await GetDocumentStatusAsync(
                connection,
                document.Id);

        long persistedSuccessfulGeneration =
            await GetLastSuccessfulProcessingGenerationAsync(
                connection,
                document.Id);

        Assert.Equal(
            DocumentStatus.Available,
            persistedStatus);

        Assert.Equal(
            firstGeneration,
            persistedSuccessfulGeneration);
    }

    [Fact]
    public async Task PublishSuccessfulProcessingAsync_WhenCancellationRequested_DoesNotChangeStateOrSuccessfulGeneration()
    {
        await using SqliteConnection connection =
            CreateConnection();

        Document document =
            CreateAndPersistDocument(connection);

        var store =
            CreateStore(connection);

        long generation =
            await store.AcquireProcessingGenerationAsync(
                document.Id);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                store.PublishSuccessfulProcessingAsync(
                    document.Id,
                    generation,
                    cancellationTokenSource.Token));

        DocumentStatus persistedStatus =
            await GetDocumentStatusAsync(
                connection,
                document.Id);

        long persistedSuccessfulGeneration =
            await GetLastSuccessfulProcessingGenerationAsync(
                connection,
                document.Id);

        Assert.Equal(
            DocumentStatus.Imported,
            persistedStatus);

        Assert.Equal(
            0L,
            persistedSuccessfulGeneration);
    }

    [Fact]
    public async Task RecoverCancelledProcessingAsync_WhenNoSuccessfulProcessingExists_RestoresImported()
    {
        await using SqliteConnection connection =
            CreateConnection();

        Document document =
            CreateAndPersistDocument(connection);

        var store =
            CreateStore(connection);

        long generation =
            await store.AcquireProcessingGenerationAsync(
                document.Id);

        await store.PublishProcessingStateAsync(
            document.Id,
            generation,
            DocumentStatus.Processing);

        await store.RecoverCancelledProcessingAsync(
            document.Id,
            generation,
            CancellationToken.None);

        DocumentStatus persistedStatus =
            await GetDocumentStatusAsync(
                connection,
                document.Id);

        long persistedSuccessfulGeneration =
            await GetLastSuccessfulProcessingGenerationAsync(
                connection,
                document.Id);

        Assert.Equal(
            DocumentStatus.Imported,
            persistedStatus);

        Assert.Equal(
            0L,
            persistedSuccessfulGeneration);
    }

    [Fact]
    public async Task RecoverCancelledProcessingAsync_WhenSuccessfulProcessingExists_RestoresAvailableAndPreservesSuccessfulGeneration()
    {
        await using SqliteConnection connection =
            CreateConnection();

        Document document =
            CreateAndPersistDocument(connection);

        var store =
            CreateStore(connection);

        long firstGeneration =
            await store.AcquireProcessingGenerationAsync(
                document.Id);

        await store.PublishSuccessfulProcessingAsync(
            document.Id,
            firstGeneration);

        long secondGeneration =
            await store.AcquireProcessingGenerationAsync(
                document.Id);

        await store.PublishProcessingStateAsync(
            document.Id,
            secondGeneration,
            DocumentStatus.Processing);

        await store.RecoverCancelledProcessingAsync(
            document.Id,
            secondGeneration,
            CancellationToken.None);

        DocumentStatus persistedStatus =
            await GetDocumentStatusAsync(
                connection,
                document.Id);

        long persistedGeneration =
            await GetProcessingGenerationAsync(
                connection,
                document.Id);

        long persistedSuccessfulGeneration =
            await GetLastSuccessfulProcessingGenerationAsync(
                connection,
                document.Id);

        Assert.Equal(
            DocumentStatus.Available,
            persistedStatus);

        Assert.Equal(
            secondGeneration,
            persistedGeneration);

        Assert.Equal(
            firstGeneration,
            persistedSuccessfulGeneration);
    }

    [Fact]
    public async Task RecoverCancelledProcessingAsync_WhenGenerationIsStale_ThrowsAndPreservesCurrentState()
    {
        await using SqliteConnection connection =
            CreateConnection();

        Document document =
            CreateAndPersistDocument(connection);

        var store =
            CreateStore(connection);

        long firstGeneration =
            await store.AcquireProcessingGenerationAsync(
                document.Id);

        await store.PublishProcessingStateAsync(
            document.Id,
            firstGeneration,
            DocumentStatus.Processing);

        long secondGeneration =
            await store.AcquireProcessingGenerationAsync(
                document.Id);

        await store.PublishProcessingStateAsync(
            document.Id,
            secondGeneration,
            DocumentStatus.Processing);

        StaleProcessingGenerationException exception =
            await Assert.ThrowsAsync<StaleProcessingGenerationException>(
                () =>
                    store.RecoverCancelledProcessingAsync(
                        document.Id,
                        firstGeneration,
                        CancellationToken.None));

        Assert.Equal(
            document.Id,
            exception.DocumentId);

        Assert.Equal(
            firstGeneration,
            exception.ProcessingGeneration);

        Assert.Equal(
            secondGeneration,
            exception.CurrentGeneration);

        DocumentStatus persistedStatus =
            await GetDocumentStatusAsync(
                connection,
                document.Id);

        long persistedGeneration =
            await GetProcessingGenerationAsync(
                connection,
                document.Id);

        Assert.Equal(
            DocumentStatus.Processing,
            persistedStatus);

        Assert.Equal(
            secondGeneration,
            persistedGeneration);
    }

    [Fact]
    public async Task RecoverCancelledProcessingAsync_WhenCancellationRequested_DoesNotChangeState()
    {
        await using SqliteConnection connection =
            CreateConnection();

        Document document =
            CreateAndPersistDocument(connection);

        var store =
            CreateStore(connection);

        long generation =
            await store.AcquireProcessingGenerationAsync(
                document.Id);

        await store.PublishProcessingStateAsync(
            document.Id,
            generation,
            DocumentStatus.Processing);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                store.RecoverCancelledProcessingAsync(
                    document.Id,
                    generation,
                    cancellationTokenSource.Token));

        DocumentStatus persistedStatus =
            await GetDocumentStatusAsync(
                connection,
                document.Id);

        long persistedSuccessfulGeneration =
            await GetLastSuccessfulProcessingGenerationAsync(
                connection,
                document.Id);

        Assert.Equal(
            DocumentStatus.Processing,
            persistedStatus);

        Assert.Equal(
            0L,
            persistedSuccessfulGeneration);
    }

    [Fact]
    public async Task ReplaceChunksAsync_PersistsChunks()
    {
        await using SqliteConnection connection =
            CreateConnection();

        Document document =
            CreateAndPersistDocument(connection);

        var store =
            CreateStore(connection);

        await store.ReplaceChunksAsync(
            document.Id,
            document.ProcessingGeneration,
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
    public async Task ReplaceChunksAsync_WhenGenerationIsStale_ThrowsAndPreservesExistingChunks()
    {
        await using SqliteConnection connection =
            CreateConnection();

        Document document =
            CreateAndPersistDocument(connection);

        var store =
            CreateStore(connection);

        await store.AcquireProcessingGenerationAsync(
            document.Id);

        await store.ReplaceChunksAsync(
            document.Id,
            1L,
            [
                new DocumentChunk(
                0,
                "CURRENT RESULT")
            ]);

        long newerGeneration =
            await store.AcquireProcessingGenerationAsync(
                document.Id);

        Assert.Equal(
            2L,
            newerGeneration);

        await Assert.ThrowsAsync<StaleProcessingGenerationException>(
            () =>
                store.ReplaceChunksAsync(
                    document.Id,
                    1L,
                    [
                        new DocumentChunk(
                        0,
                        "STALE RESULT")
                    ]));

        List<DocumentChunkEntity> chunks =
            await GetChunksAsync(connection);

        DocumentChunkEntity chunk =
            Assert.Single(chunks);

        Assert.Equal(
            "CURRENT RESULT",
            chunk.Text);
    }

    [Fact]
    public async Task ReplaceChunksAsync_WhenCalledAgain_ReplacesPreviousChunks()
    {
        await using SqliteConnection connection =
            CreateConnection();

        Document document =
            CreateAndPersistDocument(connection);

        var store =
            CreateStore(connection);

        await store.ReplaceChunksAsync(
            document.Id,
            document.ProcessingGeneration,
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
            document.ProcessingGeneration,
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

        var store =
            CreateStore(connection);

        await store.ReplaceChunksAsync(
            document.Id,
            document.ProcessingGeneration,
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
                    document.ProcessingGeneration,
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

        var store =
            CreateStore(connection);

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
            document.ProcessingGeneration,
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

        var store =
            CreateStore(connection);

        await store.ReplaceChunksAsync(
            document.Id,
            document.ProcessingGeneration,
            [
                new DocumentChunk(
                    0,
                    "Existing chunk.")
            ]);

        await store.ReplaceChunksAsync(
            document.Id,
            document.ProcessingGeneration,
            []);

        List<DocumentChunkEntity> chunks =
            await GetChunksAsync(connection);

        Assert.Empty(
            chunks);
    }

    [Fact]
    public async Task ReplaceChunksAsync_WhenCancellationRequested_ThrowsOperationCanceledException()
    {
        await using SqliteConnection connection =
            CreateConnection();

        Document document =
            CreateAndPersistDocument(connection);

        var store =
            CreateStore(connection);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                store.ReplaceChunksAsync(
                    document.Id,
                    document.ProcessingGeneration,
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

        var processingStore =
            CreateStore(connection);

        var documentRepository =
            new SqliteDocumentRepository(
                CreateFactory(connection),
                NullLogger<SqliteDocumentRepository>.Instance);

        await processingStore.ReplaceChunksAsync(
            document.Id,
            document.ProcessingGeneration,
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

        Assert.Empty(
            afterDelete);
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
                Guid.NewGuid(),
                "document.txt",
                "Test Document",
                $"hash-{Guid.NewGuid():N}",
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
                ProcessingGeneration = document.ProcessingGeneration,
                LastSuccessfulProcessingGeneration =
                    document.LastSuccessfulProcessingGeneration
            });

        context.SaveChanges();

        return document;
    }

    private static async Task<long> GetProcessingGenerationAsync(
        SqliteConnection connection,
        Guid documentId)
    {
        await using DeskVaultDbContext context =
            CreateContext(connection);

        return await context.Documents
            .Where(
                document => document.Id == documentId)
            .Select(
                document => document.ProcessingGeneration)
            .SingleAsync();
    }

    private static async Task<long> GetLastSuccessfulProcessingGenerationAsync(
        SqliteConnection connection,
        Guid documentId)
    {
        await using DeskVaultDbContext context =
            CreateContext(connection);

        return await context.Documents
            .Where(
                document => document.Id == documentId)
            .Select(
                document => document.LastSuccessfulProcessingGeneration)
            .SingleAsync();
    }

    private static async Task<DocumentStatus> GetDocumentStatusAsync(
        SqliteConnection connection,
        Guid documentId)
    {
        await using DeskVaultDbContext context =
            CreateContext(connection);

        int status =
            await context.Documents
                .Where(
                    document => document.Id == documentId)
                .Select(
                    document => document.Status)
                .SingleAsync();

        return (DocumentStatus)status;
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

    private static SqliteConnection CreateSharedMemoryConnection(
        string databaseName)
    {
        var connection =
            new SqliteConnection(
                $"Data Source=file:{databaseName}?mode=memory&cache=shared");

        connection.Open();

        return connection;
    }

    private static void CreateDatabase(
        SqliteConnection connection)
    {
        using var context =
            CreateContext(connection);

        context.Database.EnsureCreated();
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
            return SqliteDocumentProcessingStoreTests.CreateContext(
                _connection);
        }
    }
}
