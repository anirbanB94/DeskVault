using DeskVault.Application.Documents.Commands.ImportDocument;
using DeskVault.Application.Interfaces;
using DeskVault.Domain.Documents;
using DeskVault.Infrastructure.Persistence.Context;
using DeskVault.Infrastructure.Persistence.Entities;
using DeskVault.Shared.Resources;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DeskVault.Infrastructure.Repositories;

public sealed class SqliteDocumentRepository
    : IDocumentRepository
{
    private readonly IDbContextFactory<DeskVaultDbContext> _dbContextFactory;
    private readonly ILogger<SqliteDocumentRepository> _logger;

    public SqliteDocumentRepository(
        IDbContextFactory<DeskVaultDbContext> dbContextFactory,
        ILogger<SqliteDocumentRepository> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task<bool> ExistsByHashAsync(
        string sha256Hash,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext =
            await _dbContextFactory.CreateDbContextAsync(
                cancellationToken);

        return await dbContext.Documents
            .AnyAsync(
                document => document.Sha256Hash == sha256Hash,
                cancellationToken);
    }

    public async Task AddAsync(
        Document document,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            LogMessages.DocumentRepositoryAddStarted);

        await using var dbContext =
            await _dbContextFactory.CreateDbContextAsync(
                cancellationToken);

        var entity = new DocumentEntity
        {
            Id = document.Id,
            FileName = document.FileName,
            DisplayName = document.DisplayName,
            Sha256Hash = document.Sha256Hash,
            ImportedAt = document.ImportedAt,
            Status = (int)document.Status,
            LifecycleState = (int)document.LifecycleState,
            ProcessingState = (int)document.ProcessingState,
            StoredFilePath = document.StoredFilePath,
            ProcessingGeneration = document.ProcessingGeneration,
            LastSuccessfulProcessingGeneration = document.LastSuccessfulProcessingGeneration,
            LastSuccessfulProcessingRuleVersion = document.LastSuccessfulProcessingRuleVersion
        };

        await dbContext.Documents.AddAsync(
            entity,
            cancellationToken);

        await dbContext.DocumentKnowledgeAvailabilities.AddRangeAsync(
            document.KnowledgeAvailability.Select(
                availability =>
                    new DocumentKnowledgeAvailabilityEntity
                    {
                        DocumentId = document.Id,
                        Representation = (int)availability.Representation,
                        State = (int)availability.State,
                        LastAvailableProcessingGeneration = availability.LastAvailableProcessingGeneration
                    }),
            cancellationToken);

        try
        {
            await dbContext.SaveChangesAsync(
                cancellationToken);
        }
        catch (DbUpdateException ex) when (
            IsSha256UniqueConstraintViolation(ex))
        {
            throw new DocumentHashConflictException(
                ex);
        }

        _logger.LogInformation(
            LogMessages.DocumentRepositoryAddCompleted);
    }

    public async Task<Document?> GetByIdAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext =
            await _dbContextFactory.CreateDbContextAsync(
                cancellationToken);

        var entity = await dbContext.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(
                document => document.Id == documentId,
                cancellationToken);

        if (entity is null)
        {
            return null;
        }

        List<DocumentKnowledgeAvailabilityEntity>
            knowledgeAvailability =
                await dbContext.DocumentKnowledgeAvailabilities
                    .AsNoTracking()
                    .Where(
                        availability =>
                            availability.DocumentId ==
                            documentId)
                    .OrderBy(
                        availability =>
                            availability.Representation)
                    .ToListAsync(
                        cancellationToken);

        return ToDomain(
            entity,
            knowledgeAvailability);
    }

    public async Task<IReadOnlyList<Document>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        await using var dbContext =
            await _dbContextFactory.CreateDbContextAsync(
                cancellationToken);

        var entities = await dbContext.Documents
            .AsNoTracking()
            .OrderByDescending(
                document => document.ImportedAt)
            .ToListAsync(
                cancellationToken);

        if (entities.Count == 0)
        {
            return [];
        }

        Guid[] documentIds =
            entities
                .Select(
                    document =>
                        document.Id)
                .ToArray();

        List<DocumentKnowledgeAvailabilityEntity>
            knowledgeAvailability =
                await dbContext.DocumentKnowledgeAvailabilities
                    .AsNoTracking()
                    .Where(
                        availability =>
                            documentIds.Contains(
                                availability.DocumentId))
                    .OrderBy(
                        availability =>
                            availability.DocumentId)
                    .ThenBy(
                        availability =>
                            availability.Representation)
                    .ToListAsync(
                        cancellationToken);

        Dictionary<Guid, IReadOnlyList<DocumentKnowledgeAvailabilityEntity>>
            knowledgeByDocument =
                knowledgeAvailability
                    .GroupBy(
                        availability =>
                            availability.DocumentId)
                    .ToDictionary(
                        group =>
                            group.Key,
                        group =>
                            (IReadOnlyList<DocumentKnowledgeAvailabilityEntity>)
                            group.ToList());

        return entities
            .Select(
                entity =>
                    ToDomain(
                        entity,
                        knowledgeByDocument.TryGetValue(
                            entity.Id,
                            out IReadOnlyList<DocumentKnowledgeAvailabilityEntity>? availability)
                                ? availability
                                : []))
            .ToList();
    }

    public async Task DeleteAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext =
            await _dbContextFactory.CreateDbContextAsync(
                cancellationToken);

        var entity = await dbContext.Documents
            .FirstOrDefaultAsync(
                document => document.Id == documentId,
                cancellationToken);

        if (entity is null)
        {
            return;
        }

        dbContext.Documents.Remove(entity);

        await dbContext.SaveChangesAsync(
            cancellationToken);

        _logger.LogInformation(
            LogMessages.DocumentRepositoryDeleteCompleted);
    }

    public async Task UpdateAsync(
        Document document,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext =
            await _dbContextFactory.CreateDbContextAsync(
                cancellationToken);

        var entity = await dbContext.Documents
            .FirstOrDefaultAsync(
                existing =>
                    existing.Id == document.Id,
                cancellationToken);

        if (entity is null)
        {
            _logger.LogWarning(
                LogMessages.DocumentRepositoryUpdateNotFound);

            throw new InvalidOperationException(
                $"Document '{document.Id}' was not found.");
        }

        List<DocumentKnowledgeAvailabilityEntity>
            existingKnowledgeAvailability =
                await dbContext.DocumentKnowledgeAvailabilities
                    .Where(
                        availability =>
                            availability.DocumentId ==
                            document.Id)
                    .ToListAsync(
                        cancellationToken);

        entity.FileName = document.FileName;
        entity.DisplayName = document.DisplayName;
        entity.Sha256Hash = document.Sha256Hash;
        entity.ImportedAt = document.ImportedAt;
        entity.Status = (int)document.Status;
        entity.LifecycleState = (int)document.LifecycleState;
        entity.ProcessingState = (int)document.ProcessingState;
        entity.StoredFilePath = document.StoredFilePath;
        entity.ProcessingGeneration = document.ProcessingGeneration;
        entity.LastSuccessfulProcessingGeneration = document.LastSuccessfulProcessingGeneration;
        entity.LastSuccessfulProcessingRuleVersion = document.LastSuccessfulProcessingRuleVersion;

        Dictionary<
                DocumentKnowledgeRepresentationKind,
                DocumentKnowledgeAvailability>
            desiredKnowledgeAvailability =
                document.KnowledgeAvailability
                    .ToDictionary(
                        availability =>
                            availability.Representation);

        foreach (
            DocumentKnowledgeAvailabilityEntity existingAvailability
            in existingKnowledgeAvailability)
        {
            DocumentKnowledgeRepresentationKind representation =
                (DocumentKnowledgeRepresentationKind)
                    existingAvailability.Representation;

            if (!desiredKnowledgeAvailability.TryGetValue(
                    representation,
                    out DocumentKnowledgeAvailability? desired))
            {
                dbContext.DocumentKnowledgeAvailabilities.Remove(
                    existingAvailability);

                continue;
            }

            existingAvailability.State =
                (int)desired.State;

            existingAvailability.LastAvailableProcessingGeneration =
                desired.LastAvailableProcessingGeneration;
        }

        HashSet<DocumentKnowledgeRepresentationKind>
            existingRepresentations =
                existingKnowledgeAvailability
                    .Select(
                        availability =>
                            (DocumentKnowledgeRepresentationKind)
                                availability.Representation)
                    .ToHashSet();

        foreach (
            DocumentKnowledgeAvailability desired
            in document.KnowledgeAvailability)
        {
            if (existingRepresentations.Contains(
                    desired.Representation))
            {
                continue;
            }

            await dbContext.DocumentKnowledgeAvailabilities.AddAsync(
                new DocumentKnowledgeAvailabilityEntity
                {
                    DocumentId =
                        document.Id,
                    Representation =
                        (int)desired.Representation,
                    State =
                        (int)desired.State,
                    LastAvailableProcessingGeneration =
                        desired.LastAvailableProcessingGeneration
                },
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(
            cancellationToken);

        _logger.LogInformation(
            LogMessages.DocumentRepositoryUpdateCompleted);
    }

    private static bool IsSha256UniqueConstraintViolation(
        DbUpdateException exception)
    {
        SqliteException? sqliteException =
            exception.InnerException as SqliteException;

        return sqliteException is not null &&
               sqliteException.SqliteErrorCode == 19 &&
               sqliteException.SqliteExtendedErrorCode == 2067 &&
               sqliteException.Message.Contains(
                   "Documents.Sha256Hash",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static Document ToDomain(
        DocumentEntity entity,
        IReadOnlyList<DocumentKnowledgeAvailabilityEntity>
            knowledgeAvailability)
    {
        IReadOnlyList<DocumentKnowledgeAvailability>
            domainKnowledgeAvailability =
                knowledgeAvailability
                    .Select(
                        availability =>
                            new DocumentKnowledgeAvailability(
                                (DocumentKnowledgeRepresentationKind)
                                    availability.Representation,
                                (DocumentKnowledgeAvailabilityState)
                                    availability.State,
                                availability.LastAvailableProcessingGeneration))
                    .ToList();

        return Document.Restore(
            entity.Id,
            entity.FileName,
            entity.DisplayName,
            entity.Sha256Hash,
            entity.StoredFilePath,
            entity.ImportedAt,
            (DocumentStatus)entity.Status,
            entity.ProcessingGeneration,
            entity.LastSuccessfulProcessingGeneration,
            entity.LastSuccessfulProcessingRuleVersion,
            (DocumentLifecycleState)entity.LifecycleState,
            (DocumentProcessingState)entity.ProcessingState,
            domainKnowledgeAvailability);
    }
}
