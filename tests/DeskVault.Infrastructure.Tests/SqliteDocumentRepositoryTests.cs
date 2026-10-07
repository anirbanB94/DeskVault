using DeskVault.Application.Documents.Commands.ImportDocument;
using DeskVault.Domain.Documents;
using DeskVault.Infrastructure.Persistence.Context;
using DeskVault.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeskVault.Infrastructure.Tests;

public sealed class SqliteDocumentRepositoryTests
{
    [Fact]
    public async Task AddAsync_WhenDocumentIsValid_PersistsDocument()
    {
        await using SqliteConnection connection =
            CreateConnection();

        var repository =
            CreateRepository(connection);

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
            document.ImportedAt,
            result.ImportedAt);

        Assert.Equal(
            document.Status,
            result.Status);
    }

    [Fact]
    public async Task AddAsync_WhenDocumentHasProcessingGeneration_PersistsAndRestoresGeneration()
    {
        await using SqliteConnection connection =
            CreateConnection();

        var repository =
            CreateRepository(connection);

        Document document =
            Document.Restore(
                Guid.NewGuid(),
                "document.txt",
                "Test Document",
                $"sha256-test-hash-{Guid.NewGuid():N}",
                "document.dvault",
                DateTime.UtcNow,
                DocumentStatus.Processing,
                7);

        await repository.AddAsync(
            document);

        Document? result =
            await repository.GetByIdAsync(
                document.Id);

        Assert.NotNull(result);

        Assert.Equal(
            7L,
            result.ProcessingGeneration);
    }

    [Fact]
    public async Task AddAsync_WhenDocumentHasLastSuccessfulProcessingGeneration_PersistsAndRestoresGeneration()
    {
        await using SqliteConnection connection =
            CreateConnection();

        var repository =
            CreateRepository(connection);

        Document document =
            Document.Restore(
                Guid.NewGuid(),
                "document.txt",
                "Test Document",
                $"sha256-test-hash-{Guid.NewGuid():N}",
                "document.dvault",
                DateTime.UtcNow,
                DocumentStatus.Available,
                7,
                5);

        await repository.AddAsync(
            document);

        Document? result =
            await repository.GetByIdAsync(
                document.Id);

        Assert.NotNull(result);

        Assert.Equal(
            7L,
            result.ProcessingGeneration);

        Assert.Equal(
            5L,
            result.LastSuccessfulProcessingGeneration);
    }

    [Fact]
    public async Task AddAsync_WhenDocumentHasLastSuccessfulProcessingRuleVersion_PersistsAndRestoresVersion()
    {
        await using SqliteConnection connection =
            CreateConnection();

        var repository =
            CreateRepository(connection);

        const string expectedVersion =
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        Document document =
            Document.Restore(
                Guid.NewGuid(),
                "document.txt",
                "Test Document",
                $"sha256-test-hash-{Guid.NewGuid():N}",
                "document.dvault",
                DateTime.UtcNow,
                DocumentStatus.Available,
                7,
                5,
                expectedVersion);

        await repository.AddAsync(
            document);

        Document? result =
            await repository.GetByIdAsync(
                document.Id);

        Assert.NotNull(result);

        Assert.Equal(
            7L,
            result.ProcessingGeneration);

        Assert.Equal(
            5L,
            result.LastSuccessfulProcessingGeneration);

        Assert.Equal(
            expectedVersion,
            result.LastSuccessfulProcessingRuleVersion);
    }

    [Fact]
    public async Task AddAsync_WhenDocumentHashAlreadyExists_ThrowsDocumentHashConflictException()
    {
        await using SqliteConnection connection =
            CreateConnection();

        var repository =
            CreateRepository(connection);

        const string duplicateHash =
            "duplicate-sha256-hash";

        Document firstDocument =
            Document.Create(
                Guid.NewGuid(),
                "first.txt",
                "First Document",
                duplicateHash,
                "first.dvault");

        Document secondDocument =
            Document.Create(
                Guid.NewGuid(),
                "second.txt",
                "Second Document",
                duplicateHash,
                "second.dvault");

        await repository.AddAsync(
            firstDocument);

        DocumentHashConflictException exception =
            await Assert.ThrowsAsync<DocumentHashConflictException>(
                () =>
                    repository.AddAsync(
                        secondDocument));

        Assert.IsType<DbUpdateException>(
            exception.InnerException);

        Assert.IsType<SqliteException>(
            exception.InnerException!.InnerException);

        IReadOnlyList<Document> documents =
            await repository.GetAllAsync();

        Assert.Single(documents);

        Assert.Equal(
            firstDocument.Id,
            documents[0].Id);

        Assert.Equal(
            duplicateHash,
            documents[0].Sha256Hash);
    }

    [Fact]
    public async Task AddAsync_WhenDocumentIdAlreadyExists_PropagatesPersistenceConflict()
    {
        await using SqliteConnection connection =
            CreateConnection();

        var repository =
            CreateRepository(connection);

        Guid documentId =
            Guid.NewGuid();

        Document firstDocument =
            Document.Create(
                documentId,
                "first.txt",
                "First Document",
                "first-hash",
                "first.dvault");

        Document secondDocument =
            Document.Create(
                documentId,
                "second.txt",
                "Second Document",
                "second-hash",
                "second.dvault");

        await repository.AddAsync(
            firstDocument);

        DbUpdateException exception =
            await Assert.ThrowsAsync<DbUpdateException>(
                () =>
                    repository.AddAsync(
                        secondDocument));

        Assert.IsType<SqliteException>(
            exception.InnerException);
    }

    [Fact]
    public async Task ExistsByHashAsync_WhenHashExists_ReturnsTrue()
    {
        await using SqliteConnection connection =
            CreateConnection();

        var repository =
            CreateRepository(connection);

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
        await using SqliteConnection connection =
            CreateConnection();

        var repository =
            CreateRepository(connection);

        bool exists =
            await repository.ExistsByHashAsync(
                "missing-hash");

        Assert.False(
            exists);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsDocumentsInDescendingImportOrder()
    {
        await using SqliteConnection connection =
            CreateConnection();

        var repository =
            CreateRepository(connection);

        DateTime importedAt =
            DateTime.UtcNow;

        Document older =
            Document.Restore(
                Guid.NewGuid(),
                "older.txt",
                "Older Document",
                "hash-older",
                "older.dvault",
                importedAt.AddMinutes(-5),
                DocumentStatus.Imported);

        Document newer =
            Document.Restore(
                Guid.NewGuid(),
                "newer.txt",
                "Newer Document",
                "hash-newer",
                "newer.dvault",
                importedAt,
                DocumentStatus.Imported);

        await repository.AddAsync(
            older);

        await repository.AddAsync(
            newer);

        IReadOnlyList<Document> documents =
            await repository.GetAllAsync();

        Assert.Equal(
            2,
            documents.Count);

        Assert.Equal(
            newer.Id,
            documents[0].Id);

        Assert.Equal(
            older.Id,
            documents[1].Id);
    }

    [Fact]
    public async Task DeleteAsync_WhenDocumentExists_RemovesDocument()
    {
        await using SqliteConnection connection =
            CreateConnection();

        var repository =
            CreateRepository(connection);

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
        await using SqliteConnection connection =
            CreateConnection();

        var repository =
            CreateRepository(connection);

        await repository.DeleteAsync(
            Guid.NewGuid());
    }

    [Fact]
    public async Task UpdateAsync_WhenDocumentStatusChanges_PersistsUpdatedStatus()
    {
        await using SqliteConnection connection =
            CreateConnection();

        var repository =
            CreateRepository(connection);

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
    public async Task AddAsync_WhenDocumentHasSeparatedLifecycleState_PersistsAndRestoresCompleteState()
    {
        await using SqliteConnection connection =
            CreateConnection();

        var repository =
            CreateRepository(connection);

        const long processingGeneration = 7L;
        const long lastSuccessfulProcessingGeneration = 5L;

        Document document =
            Document.Restore(
                Guid.NewGuid(),
                "document.txt",
                "Test Document",
                $"sha256-test-hash-{Guid.NewGuid():N}",
                "document.dvault",
                DateTime.UtcNow,
                DocumentStatus.Available,
                processingGeneration,
                lastSuccessfulProcessingGeneration,
                "processing-v1",
                DocumentLifecycleState.Archived,
                DocumentProcessingState.Succeeded,
                [
                    new DocumentKnowledgeAvailability(
                        DocumentKnowledgeRepresentationKind.KeywordSearch,
                        DocumentKnowledgeAvailabilityState.Available,
                        lastSuccessfulProcessingGeneration)
                ]);

        await repository.AddAsync(
            document);

        Document? result =
            await repository.GetByIdAsync(
                document.Id);

        Assert.NotNull(result);

        Assert.Equal(
            DocumentLifecycleState.Archived,
            result.LifecycleState);

        Assert.Equal(
            DocumentProcessingState.Succeeded,
            result.ProcessingState);

        DocumentKnowledgeAvailability availability =
            result.GetKnowledgeAvailability(
                DocumentKnowledgeRepresentationKind.KeywordSearch);

        Assert.Equal(
            DocumentKnowledgeAvailabilityState.Available,
            availability.State);

        Assert.Equal(
            lastSuccessfulProcessingGeneration,
            availability.LastAvailableProcessingGeneration);

        Assert.Equal(
            processingGeneration,
            result.ProcessingGeneration);

        Assert.Equal(
            lastSuccessfulProcessingGeneration,
            result.LastSuccessfulProcessingGeneration);

        Assert.Equal(
            "processing-v1",
            result.LastSuccessfulProcessingRuleVersion);
    }

    [Fact]
    public async Task UpdateAsync_WhenDocumentIsUpdated_PreservesCompleteProcessingLineage()
    {
        await using SqliteConnection connection =
            CreateConnection();

        var repository =
            CreateRepository(connection);

        const long processingGeneration = 9L;
        const long lastSuccessfulProcessingGeneration = 7L;
        const string processingRuleVersion =
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        Document document =
            Document.Restore(
                Guid.NewGuid(),
                "document.txt",
                "Test Document",
                $"sha256-test-hash-{Guid.NewGuid():N}",
                "document.dvault",
                DateTime.UtcNow,
                DocumentStatus.Available,
                processingGeneration,
                lastSuccessfulProcessingGeneration,
                processingRuleVersion,
                DocumentLifecycleState.Active,
                DocumentProcessingState.Succeeded,
                [
                    new DocumentKnowledgeAvailability(
                        DocumentKnowledgeRepresentationKind.KeywordSearch,
                        DocumentKnowledgeAvailabilityState.Available,
                        lastSuccessfulProcessingGeneration)
                ]);

        await repository.AddAsync(
            document);

        document.MarkAvailable();

        await repository.UpdateAsync(
            document);

        Document? result =
            await repository.GetByIdAsync(
                document.Id);

        Assert.NotNull(result);

        Assert.Equal(
            processingGeneration,
            result.ProcessingGeneration);

        Assert.Equal(
            lastSuccessfulProcessingGeneration,
            result.LastSuccessfulProcessingGeneration);

        Assert.Equal(
            processingRuleVersion,
            result.LastSuccessfulProcessingRuleVersion);
    }

    [Fact]
    public async Task UpdateAsync_WhenKnowledgeAvailabilityChanges_DoesNotChangeProcessingOutcome()
    {
        await using SqliteConnection connection =
            CreateConnection();

        var repository =
            CreateRepository(connection);

        const long processingGeneration = 6L;

        Document document =
            Document.Restore(
                Guid.NewGuid(),
                "document.txt",
                "Test Document",
                $"sha256-test-hash-{Guid.NewGuid():N}",
                "document.dvault",
                DateTime.UtcNow,
                DocumentStatus.Available,
                processingGeneration,
                processingGeneration,
                "processing-v1",
                DocumentLifecycleState.Active,
                DocumentProcessingState.Succeeded,
                [
                    new DocumentKnowledgeAvailability(
                        DocumentKnowledgeRepresentationKind.KeywordSearch,
                        DocumentKnowledgeAvailabilityState.Available,
                        processingGeneration)
                ]);

        await repository.AddAsync(
            document);

        document.SetKnowledgeAvailability(
            DocumentKnowledgeRepresentationKind.KeywordSearch,
            DocumentKnowledgeAvailabilityState.Stale,
            processingGeneration);

        await repository.UpdateAsync(
            document);

        Document? result =
            await repository.GetByIdAsync(
                document.Id);

        Assert.NotNull(result);

        Assert.Equal(
            DocumentProcessingState.Succeeded,
            result.ProcessingState);

        DocumentKnowledgeAvailability availability =
            result.GetKnowledgeAvailability(
                DocumentKnowledgeRepresentationKind.KeywordSearch);

        Assert.Equal(
            DocumentKnowledgeAvailabilityState.Stale,
            availability.State);

        Assert.Equal(
            processingGeneration,
            availability.LastAvailableProcessingGeneration);
    }

    [Fact]
    public async Task UpdateAsync_WhenProcessingStateChanges_DoesNotChangeKnowledgeAvailability()
    {
        await using SqliteConnection connection =
            CreateConnection();

        var repository =
            CreateRepository(connection);

        const long processingGeneration = 8L;
        const long lastSuccessfulProcessingGeneration = 7L;

        Document document =
            Document.Restore(
                Guid.NewGuid(),
                "document.txt",
                "Test Document",
                $"sha256-test-hash-{Guid.NewGuid():N}",
                "document.dvault",
                DateTime.UtcNow,
                DocumentStatus.Available,
                processingGeneration,
                lastSuccessfulProcessingGeneration,
                "processing-v1",
                DocumentLifecycleState.Active,
                DocumentProcessingState.Processing,
                [
                    new DocumentKnowledgeAvailability(
                        DocumentKnowledgeRepresentationKind.KeywordSearch,
                        DocumentKnowledgeAvailabilityState.Available,
                        lastSuccessfulProcessingGeneration)
                ]);

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
            DocumentProcessingState.Processing,
            result.ProcessingState);

        DocumentKnowledgeAvailability availability =
            result.GetKnowledgeAvailability(
                DocumentKnowledgeRepresentationKind.KeywordSearch);

        Assert.Equal(
            DocumentKnowledgeAvailabilityState.Available,
            availability.State);

        Assert.Equal(
            lastSuccessfulProcessingGeneration,
            availability.LastAvailableProcessingGeneration);
    }

    [Fact]
    public async Task UpdateAsync_WhenDocumentIsUpdated_PersistsAllProcessingLineageColumns()
    {
        await using SqliteConnection connection =
            CreateConnection();

        var repository =
            CreateRepository(connection);

        const long processingGeneration = 11L;
        const long lastSuccessfulProcessingGeneration = 9L;
        const string processingRuleVersion =
            "abcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcd";

        Document document =
            Document.Restore(
                Guid.NewGuid(),
                "document.txt",
                "Test Document",
                $"sha256-test-hash-{Guid.NewGuid():N}",
                "document.dvault",
                DateTime.UtcNow,
                DocumentStatus.Available,
                processingGeneration,
                lastSuccessfulProcessingGeneration,
                processingRuleVersion,
                DocumentLifecycleState.Active,
                DocumentProcessingState.Succeeded,
                [
                    new DocumentKnowledgeAvailability(
                        DocumentKnowledgeRepresentationKind.KeywordSearch,
                        DocumentKnowledgeAvailabilityState.Available,
                        lastSuccessfulProcessingGeneration)
                ]);

        await repository.AddAsync(
            document);

        document.MarkAvailable();

        await repository.UpdateAsync(
            document);

        await using DeskVaultDbContext context =
            CreateContext(connection);

        var persisted =
            await context.Documents
                .AsNoTracking()
                .Where(
                    entity =>
                        entity.Id == document.Id)
                .Select(
                    entity =>
                        new
                        {
                            entity.ProcessingGeneration,
                            entity.LastSuccessfulProcessingGeneration,
                            entity.LastSuccessfulProcessingRuleVersion
                        })
                .SingleAsync();

        Assert.Equal(
            processingGeneration,
            persisted.ProcessingGeneration);

        Assert.Equal(
            lastSuccessfulProcessingGeneration,
            persisted.LastSuccessfulProcessingGeneration);

        Assert.Equal(
            processingRuleVersion,
            persisted.LastSuccessfulProcessingRuleVersion);
    }

    [Fact]
    public async Task GetByIdAsync_WhenCancellationIsRequested_ThrowsOperationCanceledException()
    {
        await using SqliteConnection connection =
            CreateConnection();

        var repository =
            CreateRepository(connection);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                repository.GetByIdAsync(
                    Guid.NewGuid(),
                    cancellationTokenSource.Token));
    }

    [Fact]
    public async Task AddAsync_WhenCancellationIsRequested_ThrowsOperationCanceledException()
    {
        await using SqliteConnection connection =
            CreateConnection();

        var repository =
            CreateRepository(connection);

        Document document =
            CreateDocument();

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                repository.AddAsync(
                    document,
                    cancellationTokenSource.Token));
    }

    private static SqliteDocumentRepository CreateRepository(
        SqliteConnection connection)
    {
        return new SqliteDocumentRepository(
            CreateFactory(connection),
            NullLogger<SqliteDocumentRepository>.Instance);
    }

    private static Document CreateDocument()
    {
        return Document.Create(
            Guid.NewGuid(),
            "document.txt",
            "Test Document",
            $"sha256-test-hash-{Guid.NewGuid():N}",
            "document.dvault");
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
            return SqliteDocumentRepositoryTests.CreateContext(
                _connection);
        }
    }
}
