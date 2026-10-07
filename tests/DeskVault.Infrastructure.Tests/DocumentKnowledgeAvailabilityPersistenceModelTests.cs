using DeskVault.Infrastructure.Persistence.Context;
using DeskVault.Infrastructure.Persistence.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DeskVault.Infrastructure.Tests;

public sealed class DocumentKnowledgeAvailabilityPersistenceModelTests
{
    [Fact]
    public async Task KnowledgeAvailabilityEntity_WhenPersisted_RoundTripsRepresentationStateAndHistoricalGeneration()
    {
        await using SqliteConnection connection =
            new(
                "Data Source=:memory:");

        await connection.OpenAsync();

        DbContextOptions<DeskVaultDbContext> options =
            new DbContextOptionsBuilder<DeskVaultDbContext>()
                .UseSqlite(connection)
                .Options;

        await using (DeskVaultDbContext setupContext =
            new(options))
        {
            await setupContext.Database.EnsureCreatedAsync();

            Guid documentId =
                Guid.NewGuid();

            await setupContext.Documents.AddAsync(
                new DocumentEntity
                {
                    Id = documentId,
                    FileName = "historical.txt",
                    DisplayName = "Historical Document",
                    Sha256Hash =
                        $"hash-{Guid.NewGuid():N}",
                    ImportedAt = DateTime.UtcNow,
                    Status = 3,
                    LifecycleState = 0,
                    ProcessingState = 2,
                    StoredFilePath = "historical.dvault",
                    ProcessingGeneration = 0L,
                    LastSuccessfulProcessingGeneration = 0L,
                    LastSuccessfulProcessingRuleVersion = null
                });

            await setupContext.DocumentKnowledgeAvailabilities.AddAsync(
                new DocumentKnowledgeAvailabilityEntity
                {
                    DocumentId = documentId,
                    Representation = 0,
                    State = 1,
                    LastAvailableProcessingGeneration = 0L
                });

            await setupContext.SaveChangesAsync();
        }

        await using (DeskVaultDbContext verificationContext =
            new(options))
        {
            DocumentKnowledgeAvailabilityEntity availability =
                await verificationContext
                    .DocumentKnowledgeAvailabilities
                    .AsNoTracking()
                    .SingleAsync();

            Assert.Equal(
                0,
                availability.Representation);

            Assert.Equal(
                1,
                availability.State);

            Assert.Equal(
                0L,
                availability.LastAvailableProcessingGeneration);
        }
    }

    [Fact]
    public async Task KnowledgeAvailabilityEntity_WhenGenerationIsNegative_RejectsPersistence()
    {
        await using SqliteConnection connection =
            new(
                "Data Source=:memory:");

        await connection.OpenAsync();

        DbContextOptions<DeskVaultDbContext> options =
            new DbContextOptionsBuilder<DeskVaultDbContext>()
                .UseSqlite(connection)
                .Options;

        await using DeskVaultDbContext context =
            new(options);

        await context.Database.EnsureCreatedAsync();

        Guid documentId =
            Guid.NewGuid();

        await context.Documents.AddAsync(
            new DocumentEntity
            {
                Id = documentId,
                FileName = "invalid.txt",
                DisplayName = "Invalid Document",
                Sha256Hash =
                    $"hash-{Guid.NewGuid():N}",
                ImportedAt = DateTime.UtcNow,
                Status = 0,
                LifecycleState = 0,
                ProcessingState = 0,
                StoredFilePath = "invalid.dvault",
                ProcessingGeneration = 0L,
                LastSuccessfulProcessingGeneration = 0L
            });

        await context.SaveChangesAsync();

        await context.DocumentKnowledgeAvailabilities.AddAsync(
            new DocumentKnowledgeAvailabilityEntity
            {
                DocumentId = documentId,
                Representation = 0,
                State = 1,
                LastAvailableProcessingGeneration = -1L
            });

        await Assert.ThrowsAsync<DbUpdateException>(
            () =>
                context.SaveChangesAsync());
    }
}
