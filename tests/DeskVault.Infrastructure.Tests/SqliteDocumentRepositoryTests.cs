using DeskVault.Application.Documents.Commands.ImportDocument;
using DeskVault.Application.Interfaces;
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

        await repository.AddAsync(document, TestContext.Current.CancellationToken);

        Document? result =
            await repository.GetByIdAsync(document.Id, TestContext.Current.CancellationToken);

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

        await repository.AddAsync(document, TestContext.Current.CancellationToken);

        Document? result =
            await repository.GetByIdAsync(document.Id, TestContext.Current.CancellationToken);

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

        await repository.AddAsync(document, TestContext.Current.CancellationToken);

        Document? result =
            await repository.GetByIdAsync(document.Id, TestContext.Current.CancellationToken);

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

        await repository.AddAsync(document, TestContext.Current.CancellationToken);

        Document? result =
            await repository.GetByIdAsync(document.Id, TestContext.Current.CancellationToken);

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

        await repository.AddAsync(firstDocument, TestContext.Current.CancellationToken);

        DocumentHashConflictException exception =
            await Assert.ThrowsAsync<DocumentHashConflictException>(
                () =>
                    repository.AddAsync(secondDocument, TestContext.Current.CancellationToken));

        Assert.IsType<DbUpdateException>(
            exception.InnerException);

        Assert.IsType<SqliteException>(
            exception.InnerException!.InnerException);

        IReadOnlyList<Document> documents =
            await repository.GetAllAsync(TestContext.Current.CancellationToken);

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

        await repository.AddAsync(firstDocument, TestContext.Current.CancellationToken);

        DbUpdateException exception =
            await Assert.ThrowsAsync<DbUpdateException>(
                () =>
                    repository.AddAsync(secondDocument, TestContext.Current.CancellationToken));

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

        await repository.AddAsync(document, TestContext.Current.CancellationToken);

        bool exists =
            await repository.ExistsByHashAsync(document.Sha256Hash, TestContext.Current.CancellationToken);

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
            await repository.ExistsByHashAsync("missing-hash", TestContext.Current.CancellationToken);

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

        await repository.AddAsync(older, TestContext.Current.CancellationToken);

        await repository.AddAsync(newer, TestContext.Current.CancellationToken);

        IReadOnlyList<Document> documents =
            await repository.GetAllAsync(TestContext.Current.CancellationToken);

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

        await repository.AddAsync(document, TestContext.Current.CancellationToken);

        await repository.DeleteAsync(document.Id, TestContext.Current.CancellationToken);

        Document? result =
            await repository.GetByIdAsync(document.Id, TestContext.Current.CancellationToken);

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

        await repository.DeleteAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);
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

        await repository.AddAsync(document, TestContext.Current.CancellationToken);

        document.MarkProcessing();

        await repository.UpdateAsync(document, TestContext.Current.CancellationToken);

        Document? result =
            await repository.GetByIdAsync(document.Id, TestContext.Current.CancellationToken);

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

        await repository.AddAsync(document, TestContext.Current.CancellationToken);

        Document? result =
            await repository.GetByIdAsync(document.Id, TestContext.Current.CancellationToken);

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

        await repository.AddAsync(document, TestContext.Current.CancellationToken);

        document.MarkAvailable();

        await repository.UpdateAsync(document, TestContext.Current.CancellationToken);

        Document? result =
            await repository.GetByIdAsync(document.Id, TestContext.Current.CancellationToken);

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

        await repository.AddAsync(document, TestContext.Current.CancellationToken);

        document.SetKnowledgeAvailability(
            DocumentKnowledgeRepresentationKind.KeywordSearch,
            DocumentKnowledgeAvailabilityState.Stale,
            processingGeneration);

        await repository.UpdateAsync(document, TestContext.Current.CancellationToken);

        Document? result =
            await repository.GetByIdAsync(document.Id, TestContext.Current.CancellationToken);

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

        await repository.AddAsync(document, TestContext.Current.CancellationToken);

        document.MarkProcessing();

        await repository.UpdateAsync(document, TestContext.Current.CancellationToken);

        Document? result =
            await repository.GetByIdAsync(document.Id, TestContext.Current.CancellationToken);

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

        await repository.AddAsync(document, TestContext.Current.CancellationToken);

        document.MarkAvailable();

        await repository.UpdateAsync(document, TestContext.Current.CancellationToken);

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
                .SingleAsync(cancellationToken: TestContext.Current.CancellationToken);

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

    [Fact]
    public async Task GetPageAsync_ReturnsRequestedPageInDeterministicImportOrder()
    {
        await using SqliteConnection connection =
            CreateConnection();

        var repository =
            CreateRepository(connection);

        DateTime importedAt = DateTime.UtcNow;

        Document older =
            Document.Restore(
                Guid.NewGuid(),
                "older.txt",
                "Older Document",
                "hash-older",
                "older.dvault",
                importedAt.AddMinutes(-10),
                DocumentStatus.Imported);

        Document middle =
            Document.Restore(
                Guid.NewGuid(),
                "middle.txt",
                "Middle Document",
                "hash-middle",
                "middle.dvault",
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
            older,
            TestContext.Current.CancellationToken);

        await repository.AddAsync(
            newer,
            TestContext.Current.CancellationToken);

        await repository.AddAsync(
            middle,
            TestContext.Current.CancellationToken);

        IReadOnlyList<Document> firstPage =
            await repository.GetPageAsync(
                null,
                1,
                TestContext.Current.CancellationToken);

        Document firstDocument =
            Assert.Single(firstPage);

        IReadOnlyList<Document> secondPage =
            await repository.GetPageAsync(
                new DocumentPageCursor(
                    firstDocument.ImportedAt,
                    firstDocument.Id),
                1,
                TestContext.Current.CancellationToken);

        Document secondDocument =
            Assert.Single(secondPage);

        IReadOnlyList<Document> thirdPage =
            await repository.GetPageAsync(
                new DocumentPageCursor(
                    secondDocument.ImportedAt,
                    secondDocument.Id),
                1,
                TestContext.Current.CancellationToken);

        Document thirdDocument =
            Assert.Single(thirdPage);

        Assert.Equal(newer.Id, firstDocument.Id);
        Assert.Equal(middle.Id, secondDocument.Id);
        Assert.Equal(older.Id, thirdDocument.Id);
    }

    [Fact]
    public async Task GetPageAsync_WhenImportTimestampsMatch_UsesDocumentIdAsTieBreakerAcrossPages()
    {
        await using SqliteConnection connection =
            CreateConnection();

        var repository =
            CreateRepository(connection);

        DateTime importedAt =
            new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        Guid firstId =
            Guid.Parse("00000000-0000-0000-0000-000000000001");

        Guid secondId =
            Guid.Parse("00000000-0000-0000-0000-000000000002");

        Guid thirdId =
            Guid.Parse("00000000-0000-0000-0000-000000000003");

        Document first =
            Document.Restore(
                firstId,
                "first.txt",
                "First Document",
                "sqlite-same-time-hash-1",
                "first.dvault",
                importedAt,
                DocumentStatus.Imported);

        Document second =
            Document.Restore(
                secondId,
                "second.txt",
                "Second Document",
                "sqlite-same-time-hash-2",
                "second.dvault",
                importedAt,
                DocumentStatus.Imported);

        Document third =
            Document.Restore(
                thirdId,
                "third.txt",
                "Third Document",
                "sqlite-same-time-hash-3",
                "third.dvault",
                importedAt,
                DocumentStatus.Imported);

        await repository.AddAsync(
            third,
            TestContext.Current.CancellationToken);

        await repository.AddAsync(
            first,
            TestContext.Current.CancellationToken);

        await repository.AddAsync(
            second,
            TestContext.Current.CancellationToken);

        DocumentPageCursor? cursor = null;

        foreach (Guid expectedId in new[] { firstId, secondId, thirdId })
        {
            IReadOnlyList<Document> page =
                await repository.GetPageAsync(
                    cursor,
                    1,
                    TestContext.Current.CancellationToken);

            Document actual =
                Assert.Single(page);

            Assert.Equal(expectedId, actual.Id);
            Assert.Equal(importedAt, actual.ImportedAt);

            cursor =
                new DocumentPageCursor(
                    actual.ImportedAt,
                    actual.Id);
        }

        IReadOnlyList<Document> exhaustedPage =
            await repository.GetPageAsync(
                cursor,
                1,
                TestContext.Current.CancellationToken);

        Assert.Empty(exhaustedPage);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(257)]
    public async Task GetPageAsync_WhenPageSizeIsInvalid_ThrowsArgumentOutOfRangeException(
        int pageSize)
    {
        await using SqliteConnection connection =
            CreateConnection();

        var repository =
            CreateRepository(connection);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => repository.GetPageAsync(
                null,
                pageSize,
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetPageAsync_WhenNewerDocumentIsInsertedBetweenPages_DoesNotSkipRemainingDocuments()
    {
        await using SqliteConnection connection =
            CreateConnection();

        var repository =
            CreateRepository(connection);

        DateTime importedAt = DateTime.UtcNow;

        Document older =
            Document.Restore(
                Guid.NewGuid(),
                "older.txt",
                "Older Document",
                "hash-older-insert-test",
                "older.dvault",
                importedAt.AddMinutes(-10),
                DocumentStatus.Imported);

        Document middle =
            Document.Restore(
                Guid.NewGuid(),
                "middle.txt",
                "Middle Document",
                "hash-middle-insert-test",
                "middle.dvault",
                importedAt.AddMinutes(-5),
                DocumentStatus.Imported);

        Document newer =
            Document.Restore(
                Guid.NewGuid(),
                "newer.txt",
                "Newer Document",
                "hash-newer-insert-test",
                "newer.dvault",
                importedAt,
                DocumentStatus.Imported);

        await repository.AddAsync(
            older,
            TestContext.Current.CancellationToken);

        await repository.AddAsync(
            middle,
            TestContext.Current.CancellationToken);

        await repository.AddAsync(
            newer,
            TestContext.Current.CancellationToken);

        IReadOnlyList<Document> firstPage =
            await repository.GetPageAsync(
                null,
                1,
                TestContext.Current.CancellationToken);

        Document firstDocument =
            Assert.Single(firstPage);

        Assert.Equal(newer.Id, firstDocument.Id);

        Document insertedNewer =
            Document.Restore(
                Guid.NewGuid(),
                "inserted.txt",
                "Inserted Newer Document",
                "hash-inserted-newer-test",
                "inserted.dvault",
                importedAt.AddMinutes(1),
                DocumentStatus.Imported);

        await repository.AddAsync(
            insertedNewer,
            TestContext.Current.CancellationToken);

        IReadOnlyList<Document> secondPage =
            await repository.GetPageAsync(
                new DocumentPageCursor(
                    firstDocument.ImportedAt,
                    firstDocument.Id),
                1,
                TestContext.Current.CancellationToken);

        Document secondDocument =
            Assert.Single(secondPage);

        IReadOnlyList<Document> thirdPage =
            await repository.GetPageAsync(
                new DocumentPageCursor(
                    secondDocument.ImportedAt,
                    secondDocument.Id),
                1,
                TestContext.Current.CancellationToken);

        Document thirdDocument =
            Assert.Single(thirdPage);

        Assert.Equal(middle.Id, secondDocument.Id);
        Assert.Equal(older.Id, thirdDocument.Id);
    }

    [Fact]
    public async Task GetPageAsync_WhenCursorHasDefaultTimestamp_ThrowsArgumentException()
    {
        await using SqliteConnection connection =
            CreateConnection();

        var repository =
            CreateRepository(connection);

        var cursor =
            new DocumentPageCursor(
                default,
                Guid.NewGuid());

        await Assert.ThrowsAsync<ArgumentException>(
            () => repository.GetPageAsync(
                cursor,
                10,
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetPageAsync_WhenCursorHasEmptyDocumentId_ThrowsArgumentException()
    {
        await using SqliteConnection connection =
            CreateConnection();

        var repository =
            CreateRepository(connection);

        var cursor =
            new DocumentPageCursor(
                DateTime.UtcNow,
                Guid.Empty);

        await Assert.ThrowsAsync<ArgumentException>(
            () => repository.GetPageAsync(
                cursor,
                10,
                TestContext.Current.CancellationToken));
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
