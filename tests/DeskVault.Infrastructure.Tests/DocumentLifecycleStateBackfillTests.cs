using DeskVault.Domain.Documents;
using DeskVault.Infrastructure.Persistence;
using DeskVault.Infrastructure.Persistence.Context;
using DeskVault.Infrastructure.Persistence.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DeskVault.Infrastructure.Tests;

public sealed class DocumentLifecycleStateBackfillTests
{
    private static readonly Guid ImportedDocumentId =
        Guid.Parse(
            "11111111-1111-1111-1111-111111111111");

    private static readonly Guid AvailableDocumentId =
        Guid.Parse(
            "22222222-2222-2222-2222-222222222222");

    private static readonly Guid ProcessingDocumentId =
        Guid.Parse(
            "33333333-3333-3333-3333-333333333333");

    private static readonly Guid FailedDocumentId =
        Guid.Parse(
            "44444444-4444-4444-4444-444444444444");

    private static readonly Guid ArchivedDocumentId =
        Guid.Parse(
            "55555555-5555-5555-5555-555555555555");

    private static readonly Guid DeletedDocumentId =
        Guid.Parse(
            "66666666-6666-6666-6666-666666666666");

    [Fact]
    public async Task BackfillAsync_MapsLegacyAvailableAndIndexedGenerationZeroToSucceededWithHistoricalKeywordAvailability()
    {
        await using SqliteConnection connection =
            CreateConnection();

        await InsertLegacyDocumentAsync(
            connection,
            AvailableDocumentId,
            DocumentStatus.Available,
            processingGeneration: 0L,
            lastSuccessfulProcessingGeneration: 0L);

        await InsertLegacyDocumentAsync(
            connection,
            ImportedDocumentId,
            DocumentStatus.Indexed,
            processingGeneration: 0L,
            lastSuccessfulProcessingGeneration: 0L);

        DocumentLifecycleStateBackfill backfill =
            CreateBackfill(
                connection);

        await backfill.BackfillAsync();

        DocumentEntity availableDocument =
            await GetDocumentAsync(
                connection,
                AvailableDocumentId);

        Assert.Equal(
            DocumentLifecycleState.Active,
            (DocumentLifecycleState)
                availableDocument.LifecycleState);

        Assert.Equal(
            DocumentProcessingState.Succeeded,
            (DocumentProcessingState)
                availableDocument.ProcessingState);

        Assert.Equal(
            0L,
            availableDocument.ProcessingGeneration);

        Assert.Equal(
            0L,
            availableDocument.LastSuccessfulProcessingGeneration);

        DocumentKnowledgeAvailabilityEntity availableKnowledge =
            await GetKnowledgeAvailabilityAsync(
                connection,
                AvailableDocumentId);

        Assert.Equal(
            DocumentKnowledgeRepresentationKind.KeywordSearch,
            (DocumentKnowledgeRepresentationKind)
                availableKnowledge.Representation);

        Assert.Equal(
            DocumentKnowledgeAvailabilityState.Available,
            (DocumentKnowledgeAvailabilityState)
                availableKnowledge.State);

        Assert.Equal(
            0L,
            availableKnowledge.LastAvailableProcessingGeneration);

        DocumentEntity indexedDocument =
            await GetDocumentAsync(
                connection,
                ImportedDocumentId);

        Assert.Equal(
            DocumentProcessingState.Succeeded,
            (DocumentProcessingState)
                indexedDocument.ProcessingState);

        DocumentKnowledgeAvailabilityEntity indexedKnowledge =
            await GetKnowledgeAvailabilityAsync(
                connection,
                ImportedDocumentId);

        Assert.Equal(
            DocumentKnowledgeAvailabilityState.Available,
            (DocumentKnowledgeAvailabilityState)
                indexedKnowledge.State);

        Assert.Equal(
            0L,
            indexedKnowledge.LastAvailableProcessingGeneration);
    }

    [Fact]
    public async Task BackfillAsync_PreservesLegacyProcessingFailureArchiveDeleteAndImportedSemantics()
    {
        await using SqliteConnection connection =
            CreateConnection();

        await InsertLegacyDocumentAsync(
            connection,
            ImportedDocumentId,
            DocumentStatus.Imported,
            processingGeneration: 0L,
            lastSuccessfulProcessingGeneration: 0L);

        await InsertLegacyDocumentAsync(
            connection,
            ProcessingDocumentId,
            DocumentStatus.Processing,
            processingGeneration: 5L,
            lastSuccessfulProcessingGeneration: 3L);

        await InsertLegacyDocumentAsync(
            connection,
            FailedDocumentId,
            DocumentStatus.Failed,
            processingGeneration: 6L,
            lastSuccessfulProcessingGeneration: 4L);

        await InsertLegacyDocumentAsync(
            connection,
            ArchivedDocumentId,
            DocumentStatus.Archived,
            processingGeneration: 2L,
            lastSuccessfulProcessingGeneration: 0L);

        await InsertLegacyDocumentAsync(
            connection,
            DeletedDocumentId,
            DocumentStatus.Deleted,
            processingGeneration: 7L,
            lastSuccessfulProcessingGeneration: 6L);

        DocumentLifecycleStateBackfill backfill =
            CreateBackfill(
                connection);

        await backfill.BackfillAsync();

        DocumentEntity importedDocument =
            await GetDocumentAsync(
                connection,
                ImportedDocumentId);

        Assert.Equal(
            DocumentLifecycleState.Active,
            (DocumentLifecycleState)
                importedDocument.LifecycleState);

        Assert.Equal(
            DocumentProcessingState.NeverProcessed,
            (DocumentProcessingState)
                importedDocument.ProcessingState);

        DocumentKnowledgeAvailabilityEntity importedKnowledge =
            await GetKnowledgeAvailabilityAsync(
                connection,
                ImportedDocumentId);

        Assert.Equal(
            DocumentKnowledgeAvailabilityState.Unavailable,
            (DocumentKnowledgeAvailabilityState)
                importedKnowledge.State);

        Assert.Null(
            importedKnowledge.LastAvailableProcessingGeneration);

        DocumentEntity processingDocument =
            await GetDocumentAsync(
                connection,
                ProcessingDocumentId);

        Assert.Equal(
            DocumentLifecycleState.Active,
            (DocumentLifecycleState)
                processingDocument.LifecycleState);

        Assert.Equal(
            DocumentProcessingState.Processing,
            (DocumentProcessingState)
                processingDocument.ProcessingState);

        Assert.Equal(
            5L,
            processingDocument.ProcessingGeneration);

        Assert.Equal(
            3L,
            processingDocument.LastSuccessfulProcessingGeneration);

        DocumentKnowledgeAvailabilityEntity processingKnowledge =
            await GetKnowledgeAvailabilityAsync(
                connection,
                ProcessingDocumentId);

        Assert.Equal(
            DocumentKnowledgeAvailabilityState.Available,
            (DocumentKnowledgeAvailabilityState)
                processingKnowledge.State);

        Assert.Equal(
            3L,
            processingKnowledge.LastAvailableProcessingGeneration);

        DocumentEntity failedDocument =
            await GetDocumentAsync(
                connection,
                FailedDocumentId);

        Assert.Equal(
            DocumentProcessingState.Failed,
            (DocumentProcessingState)
                failedDocument.ProcessingState);

        DocumentKnowledgeAvailabilityEntity failedKnowledge =
            await GetKnowledgeAvailabilityAsync(
                connection,
                FailedDocumentId);

        Assert.Equal(
            DocumentKnowledgeAvailabilityState.Available,
            (DocumentKnowledgeAvailabilityState)
                failedKnowledge.State);

        Assert.Equal(
            4L,
            failedKnowledge.LastAvailableProcessingGeneration);

        DocumentEntity archivedDocument =
            await GetDocumentAsync(
                connection,
                ArchivedDocumentId);

        Assert.Equal(
            DocumentLifecycleState.Archived,
            (DocumentLifecycleState)
                archivedDocument.LifecycleState);

        Assert.Equal(
            DocumentProcessingState.NeverProcessed,
            (DocumentProcessingState)
                archivedDocument.ProcessingState);

        DocumentKnowledgeAvailabilityEntity archivedKnowledge =
            await GetKnowledgeAvailabilityAsync(
                connection,
                ArchivedDocumentId);

        Assert.Equal(
            DocumentKnowledgeAvailabilityState.Unavailable,
            (DocumentKnowledgeAvailabilityState)
                archivedKnowledge.State);

        DocumentEntity deletedDocument =
            await GetDocumentAsync(
                connection,
                DeletedDocumentId);

        Assert.Equal(
            DocumentLifecycleState.Deleted,
            (DocumentLifecycleState)
                deletedDocument.LifecycleState);

        Assert.Equal(
            DocumentProcessingState.Succeeded,
            (DocumentProcessingState)
                deletedDocument.ProcessingState);

        DocumentKnowledgeAvailabilityEntity deletedKnowledge =
            await GetKnowledgeAvailabilityAsync(
                connection,
                DeletedDocumentId);

        Assert.Equal(
            DocumentKnowledgeAvailabilityState.Unavailable,
            (DocumentKnowledgeAvailabilityState)
                deletedKnowledge.State);

        Assert.Null(
            deletedKnowledge.LastAvailableProcessingGeneration);
    }

    [Fact]
    public async Task BackfillAsync_WhenRunAgain_IsIdempotent()
    {
        await using SqliteConnection connection =
            CreateConnection();

        await InsertLegacyDocumentAsync(
            connection,
            AvailableDocumentId,
            DocumentStatus.Available,
            processingGeneration: 0L,
            lastSuccessfulProcessingGeneration: 0L);

        DocumentLifecycleStateBackfill backfill =
            CreateBackfill(
                connection);

        await backfill.BackfillAsync();

        DocumentEntity firstDocument =
            await GetDocumentAsync(
                connection,
                AvailableDocumentId);

        DocumentKnowledgeAvailabilityEntity firstKnowledge =
            await GetKnowledgeAvailabilityAsync(
                connection,
                AvailableDocumentId);

        await backfill.BackfillAsync();

        DocumentEntity secondDocument =
            await GetDocumentAsync(
                connection,
                AvailableDocumentId);

        DocumentKnowledgeAvailabilityEntity secondKnowledge =
            await GetKnowledgeAvailabilityAsync(
                connection,
                AvailableDocumentId);

        Assert.Equal(
            firstDocument.LifecycleState,
            secondDocument.LifecycleState);

        Assert.Equal(
            firstDocument.ProcessingState,
            secondDocument.ProcessingState);

        Assert.Equal(
            firstDocument.ProcessingGeneration,
            secondDocument.ProcessingGeneration);

        Assert.Equal(
            firstDocument.LastSuccessfulProcessingGeneration,
            secondDocument.LastSuccessfulProcessingGeneration);

        Assert.Equal(
            firstKnowledge.State,
            secondKnowledge.State);

        Assert.Equal(
            firstKnowledge.LastAvailableProcessingGeneration,
            secondKnowledge.LastAvailableProcessingGeneration);
    }

    [Fact]
    public async Task BackfillAsync_WhenKeywordSearchAvailabilityAlreadyExists_PreservesIndependentStateAndKnowledge()
    {
        await using SqliteConnection connection =
            CreateConnection();

        await InsertLegacyDocumentAsync(
            connection,
            AvailableDocumentId,
            DocumentStatus.Processing,
            processingGeneration: 8L,
            lastSuccessfulProcessingGeneration: 7L,
            lifecycleState:
                DocumentLifecycleState.Active,
            processingState:
                DocumentProcessingState.Processing);

        await InsertKnowledgeAvailabilityAsync(
            connection,
            new DocumentKnowledgeAvailabilityEntity
            {
                DocumentId =
                    AvailableDocumentId,
                Representation =
                    (int)
                        DocumentKnowledgeRepresentationKind
                            .KeywordSearch,
                State =
                    (int)
                        DocumentKnowledgeAvailabilityState
                            .Available,
                LastAvailableProcessingGeneration =
                    7L
            });

        DocumentLifecycleStateBackfill backfill =
            CreateBackfill(
                connection);

        await backfill.BackfillAsync();

        DocumentEntity document =
            await GetDocumentAsync(
                connection,
                AvailableDocumentId);

        Assert.Equal(
            DocumentLifecycleState.Active,
            (DocumentLifecycleState)
                document.LifecycleState);

        Assert.Equal(
            DocumentProcessingState.Processing,
            (DocumentProcessingState)
                document.ProcessingState);

        Assert.Equal(
            8L,
            document.ProcessingGeneration);

        Assert.Equal(
            7L,
            document.LastSuccessfulProcessingGeneration);

        DocumentKnowledgeAvailabilityEntity knowledge =
            await GetKnowledgeAvailabilityAsync(
                connection,
                AvailableDocumentId);

        Assert.Equal(
            DocumentKnowledgeAvailabilityState.Available,
            (DocumentKnowledgeAvailabilityState)
                knowledge.State);

        Assert.Equal(
            7L,
            knowledge.LastAvailableProcessingGeneration);
    }

    [Fact]
    public async Task BackfillAsync_WhenKnowledgeGenerationExceedsLastSuccessfulProcessingGeneration_FailsWithoutMutatingDocument()
    {
        await using SqliteConnection connection =
            CreateConnection();

        await InsertLegacyDocumentAsync(
            connection,
            AvailableDocumentId,
            DocumentStatus.Processing,
            processingGeneration: 5L,
            lastSuccessfulProcessingGeneration: 4L);

        await InsertKnowledgeAvailabilityAsync(
            connection,
            new DocumentKnowledgeAvailabilityEntity
            {
                DocumentId =
                    AvailableDocumentId,
                Representation =
                    (int)
                        DocumentKnowledgeRepresentationKind
                            .KeywordSearch,
                State =
                    (int)
                        DocumentKnowledgeAvailabilityState
                            .Available,
                LastAvailableProcessingGeneration =
                    5L
            });

        DocumentLifecycleStateBackfill backfill =
            CreateBackfill(
                connection);

        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () =>
                    backfill.BackfillAsync());

        Assert.Contains(
            "cannot be greater than last successful processing generation",
            exception.Message,
            StringComparison.Ordinal);

        DocumentEntity document =
            await GetDocumentAsync(
                connection,
                AvailableDocumentId);

        Assert.Equal(
            0,
            document.LifecycleState);

        Assert.Equal(
            0,
            document.ProcessingState);

        Assert.Equal(
            5L,
            document.ProcessingGeneration);

        Assert.Equal(
            4L,
            document.LastSuccessfulProcessingGeneration);
    }

    [Fact]
    public async Task BackfillAsync_WhenInterruptedMidBackfill_RollsBackAndCanRetry()
    {
        await using SqliteConnection connection =
            CreateConnection();

        await InsertLegacyDocumentAsync(
            connection,
            ImportedDocumentId,
            DocumentStatus.Available,
            processingGeneration: 1L,
            lastSuccessfulProcessingGeneration: 1L);

        await InsertLegacyDocumentAsync(
            connection,
            AvailableDocumentId,
            DocumentStatus.Available,
            processingGeneration: 2L,
            lastSuccessfulProcessingGeneration: 2L);

        await CreateBackfillInterruptionTriggerAsync(
            connection);

        DocumentLifecycleStateBackfill backfill =
            CreateBackfill(
                connection);

        DbUpdateException exception =
            await Assert.ThrowsAsync<DbUpdateException>(
                () =>
                    backfill.BackfillAsync());

        Assert.IsType<SqliteException>(
            exception.InnerException);

        Assert.Contains(
            "Simulated lifecycle backfill interruption",
            exception.InnerException!.Message,
            StringComparison.Ordinal);

        DocumentEntity firstDocument =
            await GetDocumentAsync(
                connection,
                ImportedDocumentId);

        DocumentEntity secondDocument =
            await GetDocumentAsync(
                connection,
                AvailableDocumentId);

        Assert.Equal(
            0,
            firstDocument.LifecycleState);

        Assert.Equal(
            0,
            firstDocument.ProcessingState);

        Assert.Equal(
            0,
            secondDocument.LifecycleState);

        Assert.Equal(
            0,
            secondDocument.ProcessingState);

        Assert.Empty(
            await GetKnowledgeAvailabilitiesAsync(
                connection));

        await DropBackfillInterruptionTriggerAsync(
            connection);

        await backfill.BackfillAsync();

        DocumentEntity retriedFirstDocument =
            await GetDocumentAsync(
                connection,
                ImportedDocumentId);

        Assert.Equal(
            DocumentProcessingState.Succeeded,
            (DocumentProcessingState)
                retriedFirstDocument.ProcessingState);

        DocumentKnowledgeAvailabilityEntity firstKnowledge =
            await GetKnowledgeAvailabilityAsync(
                connection,
                ImportedDocumentId);

        Assert.Equal(
            1L,
            firstKnowledge.LastAvailableProcessingGeneration);

        DocumentEntity retriedSecondDocument =
            await GetDocumentAsync(
                connection,
                AvailableDocumentId);

        Assert.Equal(
            DocumentProcessingState.Succeeded,
            (DocumentProcessingState)
                retriedSecondDocument.ProcessingState);

        DocumentKnowledgeAvailabilityEntity secondKnowledge =
            await GetKnowledgeAvailabilityAsync(
                connection,
                AvailableDocumentId);

        Assert.Equal(
            2L,
            secondKnowledge.LastAvailableProcessingGeneration);
    }

    [Fact]
    public async Task BackfillAsync_WhenCancelledBeforeWork_ThrowsOperationCanceledException()
    {
        await using SqliteConnection connection =
            CreateConnection();

        await InsertLegacyDocumentAsync(
            connection,
            ImportedDocumentId,
            DocumentStatus.Available,
            processingGeneration: 1L,
            lastSuccessfulProcessingGeneration: 1L);

        DocumentLifecycleStateBackfill backfill =
            CreateBackfill(
                connection);

        using CancellationTokenSource cancellationTokenSource =
            new();

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () =>
                backfill.BackfillAsync(
                    cancellationTokenSource.Token));
    }

    private static DocumentLifecycleStateBackfill CreateBackfill(
        SqliteConnection connection)
    {
        return new DocumentLifecycleStateBackfill(
            new TestDbContextFactory(
                connection));
    }

    private static async Task InsertLegacyDocumentAsync(
        SqliteConnection connection,
        Guid documentId,
        DocumentStatus status,
        long processingGeneration,
        long lastSuccessfulProcessingGeneration,
        DocumentLifecycleState lifecycleState =
            DocumentLifecycleState.Active,
        DocumentProcessingState processingState =
            DocumentProcessingState.NeverProcessed)
    {
        await using DeskVaultDbContext context =
            CreateContext(
                connection);

        context.Documents.Add(
            new DocumentEntity
            {
                Id =
                    documentId,
                FileName =
                    $"{documentId:N}.txt",
                DisplayName =
                    "Legacy Document",
                Sha256Hash =
                    documentId
                        .ToString()
                        .Replace(
                            "-",
                            string.Empty)
                        .PadRight(
                            64,
                            'a')
                        [..64],
                ImportedAt =
                    new DateTime(
                        2026,
                        8,
                        1,
                        10,
                        0,
                        0,
                        DateTimeKind.Utc),
                Status =
                    (int)status,
                StoredFilePath =
                    $"Documents/{documentId:N}.dvault",
                ProcessingGeneration =
                    processingGeneration,
                LastSuccessfulProcessingGeneration =
                    lastSuccessfulProcessingGeneration,
                LastSuccessfulProcessingRuleVersion =
                    null,
                LifecycleState =
                    (int)lifecycleState,
                ProcessingState =
                    (int)processingState
            });

        await context.SaveChangesAsync();
    }

    private static async Task InsertKnowledgeAvailabilityAsync(
        SqliteConnection connection,
        DocumentKnowledgeAvailabilityEntity availability)
    {
        await using DeskVaultDbContext context =
            CreateContext(
                connection);

        context.DocumentKnowledgeAvailabilities.Add(
            availability);

        await context.SaveChangesAsync();
    }

    private static async Task<DocumentEntity> GetDocumentAsync(
        SqliteConnection connection,
        Guid documentId)
    {
        await using DeskVaultDbContext context =
            CreateContext(
                connection);

        return await context.Documents
            .AsNoTracking()
            .SingleAsync(
                document =>
                    document.Id ==
                    documentId);
    }

    private static async Task<DocumentKnowledgeAvailabilityEntity>
        GetKnowledgeAvailabilityAsync(
            SqliteConnection connection,
            Guid documentId)
    {
        await using DeskVaultDbContext context =
            CreateContext(
                connection);

        return await context.DocumentKnowledgeAvailabilities
            .AsNoTracking()
            .SingleAsync(
                availability =>
                    availability.DocumentId ==
                    documentId &&
                    availability.Representation ==
                    (int)
                        DocumentKnowledgeRepresentationKind
                            .KeywordSearch);
    }

    private static async Task<
        List<DocumentKnowledgeAvailabilityEntity>>
        GetKnowledgeAvailabilitiesAsync(
            SqliteConnection connection)
    {
        await using DeskVaultDbContext context =
            CreateContext(
                connection);

        return await context.DocumentKnowledgeAvailabilities
            .AsNoTracking()
            .OrderBy(
                availability =>
                    availability.DocumentId)
            .ToListAsync();
    }

    private static async Task CreateBackfillInterruptionTriggerAsync(
        SqliteConnection connection)
    {
        await using DeskVaultDbContext context =
            CreateContext(
                connection);

        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TRIGGER Test_Lifecycle_Backfill_Interrupt
            AFTER INSERT ON DocumentKnowledgeAvailabilities
            WHEN NEW."LastAvailableProcessingGeneration" = 2
            BEGIN
                SELECT RAISE(
                    ABORT,
                    'Simulated lifecycle backfill interruption');
            END;
            """);
    }

    private static async Task DropBackfillInterruptionTriggerAsync(
        SqliteConnection connection)
    {
        await using DeskVaultDbContext context =
            CreateContext(
                connection);

        await context.Database.ExecuteSqlRawAsync(
            """
            DROP TRIGGER Test_Lifecycle_Backfill_Interrupt;
            """);
    }

    private static SqliteConnection CreateConnection()
    {
        SqliteConnection connection =
            new(
                "Data Source=:memory:");

        connection.Open();

        using DeskVaultDbContext context =
            CreateContext(
                connection);

        context.Database.EnsureCreated();

        return connection;
    }

    private static DeskVaultDbContext CreateContext(
        SqliteConnection connection)
    {
        DbContextOptions<DeskVaultDbContext> options =
            new DbContextOptionsBuilder<DeskVaultDbContext>()
                .UseSqlite(
                    connection)
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
            _connection =
                connection;
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
            return DocumentLifecycleStateBackfillTests
                .CreateContext(
                    _connection);
        }
    }
}
