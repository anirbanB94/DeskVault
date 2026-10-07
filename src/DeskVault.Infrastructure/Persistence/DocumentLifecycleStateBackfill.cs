using DeskVault.Domain.Documents;
using DeskVault.Infrastructure.Persistence.Context;
using DeskVault.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeskVault.Infrastructure.Persistence;

public sealed class DocumentLifecycleStateBackfill
{
    private readonly IDbContextFactory<DeskVaultDbContext> _dbContextFactory;

    public DocumentLifecycleStateBackfill(
        IDbContextFactory<DeskVaultDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task BackfillAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var dbContext =
            await _dbContextFactory.CreateDbContextAsync(
                cancellationToken);

        List<DocumentEntity> documents =
            await dbContext.Documents
                .OrderBy(document => document.Id)
                .ToListAsync(
                    cancellationToken);

        if (documents.Count == 0)
        {
            return;
        }

        List<DocumentKnowledgeAvailabilityEntity>
            availabilityEntities =
                await dbContext.DocumentKnowledgeAvailabilities
                    .AsNoTracking()
                    .OrderBy(
                        availability =>
                            availability.DocumentId)
                    .ThenBy(
                        availability =>
                            availability.Representation)
                    .ToListAsync(
                        cancellationToken);

        Dictionary<Guid, List<DocumentKnowledgeAvailabilityEntity>>
            availabilityByDocument =
                availabilityEntities
                    .GroupBy(
                        availability =>
                            availability.DocumentId)
                    .ToDictionary(
                        group =>
                            group.Key,
                        group =>
                            group.ToList());

        bool hasChanges = false;

        foreach (DocumentEntity document in documents)
        {
            cancellationToken.ThrowIfCancellationRequested();

            List<DocumentKnowledgeAvailabilityEntity>
                existingAvailability =
                    availabilityByDocument.TryGetValue(
                        document.Id,
                        out List<DocumentKnowledgeAvailabilityEntity>?
                            availability)
                        ? availability
                        : [];

            ValidateExistingAvailability(
                document,
                existingAvailability);

            bool keywordSearchExists =
                existingAvailability.Any(
                    availability =>
                        availability.Representation ==
                        (int)DocumentKnowledgeRepresentationKind.KeywordSearch);

            if (keywordSearchExists)
            {
                continue;
            }

            LegacyStateProjection projection =
                ResolveLegacyState(
                    document.Status,
                    document.LastSuccessfulProcessingGeneration);

            document.LifecycleState =
                (int)projection.LifecycleState;

            document.ProcessingState =
                (int)projection.ProcessingState;

            DocumentKnowledgeAvailabilityEntity
                knowledgeAvailability =
                    new()
                    {
                        DocumentId =
                            document.Id,
                        Representation =
                            (int)
                                DocumentKnowledgeRepresentationKind
                                    .KeywordSearch,
                        State =
                            (int)projection.KnowledgeState,
                        LastAvailableProcessingGeneration =
                            projection.LastAvailableProcessingGeneration
                    };

            dbContext.DocumentKnowledgeAvailabilities.Add(
                knowledgeAvailability);

            hasChanges = true;
        }

        if (!hasChanges)
        {
            return;
        }

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        await dbContext.SaveChangesAsync(
            cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        await transaction.CommitAsync(
            CancellationToken.None);
    }

    private static LegacyStateProjection ResolveLegacyState(
        int statusValue,
        long lastSuccessfulProcessingGeneration)
    {
        if (!Enum.IsDefined(
                typeof(DocumentStatus),
                statusValue))
        {
            throw new InvalidOperationException(
                $"Legacy document status value '{statusValue}' is invalid.");
        }

        DocumentStatus status =
            (DocumentStatus)statusValue;

        DocumentLifecycleState lifecycleState =
            status switch
            {
                DocumentStatus.Archived =>
                    DocumentLifecycleState.Archived,

                DocumentStatus.Deleted =>
                    DocumentLifecycleState.Deleted,

                _ =>
                    DocumentLifecycleState.Active
            };

        DocumentProcessingState processingState =
            status switch
            {
                DocumentStatus.Processing =>
                    DocumentProcessingState.Processing,

                DocumentStatus.Failed =>
                    DocumentProcessingState.Failed,

                DocumentStatus.Indexed or
                DocumentStatus.Available
                    when lastSuccessfulProcessingGeneration >= 0L =>
                    DocumentProcessingState.Succeeded,

                DocumentStatus.Archived or
                DocumentStatus.Deleted
                    when lastSuccessfulProcessingGeneration > 0L =>
                    DocumentProcessingState.Succeeded,

                _ =>
                    DocumentProcessingState.NeverProcessed
            };

        if (status is
            DocumentStatus.Indexed or
            DocumentStatus.Available)
        {
            return new LegacyStateProjection(
                lifecycleState,
                DocumentProcessingState.Succeeded,
                DocumentKnowledgeAvailabilityState.Available,
                lastSuccessfulProcessingGeneration);
        }

        if (status != DocumentStatus.Deleted &&
            lastSuccessfulProcessingGeneration > 0L)
        {
            return new LegacyStateProjection(
                lifecycleState,
                processingState,
                DocumentKnowledgeAvailabilityState.Available,
                lastSuccessfulProcessingGeneration);
        }

        return new LegacyStateProjection(
            lifecycleState,
            processingState,
            DocumentKnowledgeAvailabilityState.Unavailable,
            null);
    }

    private static void ValidateExistingAvailability(
        DocumentEntity document,
        IReadOnlyList<DocumentKnowledgeAvailabilityEntity>
            availabilityEntities)
    {
        foreach (
            DocumentKnowledgeAvailabilityEntity availability
            in availabilityEntities)
        {
            if (!Enum.IsDefined(
                    typeof(DocumentKnowledgeRepresentationKind),
                    availability.Representation))
            {
                throw new InvalidOperationException(
                    $"Knowledge representation value '{availability.Representation}' for document '{document.Id}' is invalid.");
            }

            if (!Enum.IsDefined(
                    typeof(DocumentKnowledgeAvailabilityState),
                    availability.State))
            {
                throw new InvalidOperationException(
                    $"Knowledge availability state value '{availability.State}' for document '{document.Id}' is invalid.");
            }

            DocumentKnowledgeAvailabilityState state =
                (DocumentKnowledgeAvailabilityState)
                    availability.State;

            if (availability.LastAvailableProcessingGeneration is <
                0L)
            {
                throw new InvalidOperationException(
                    $"Knowledge availability generation for document '{document.Id}' cannot be negative.");
            }

            if (state is
                DocumentKnowledgeAvailabilityState.Available or
                DocumentKnowledgeAvailabilityState.Stale)
            {
                if (availability.LastAvailableProcessingGeneration
                    is null)
                {
                    throw new InvalidOperationException(
                        $"Available or stale knowledge for document '{document.Id}' must reference a processing generation.");
                }

                if (availability.LastAvailableProcessingGeneration >
                    document.LastSuccessfulProcessingGeneration)
                {
                    throw new InvalidOperationException(
                        $"Knowledge availability generation '{availability.LastAvailableProcessingGeneration}' for document '{document.Id}' cannot be greater than last successful processing generation '{document.LastSuccessfulProcessingGeneration}'.");
                }
            }
        }
    }

    private sealed record LegacyStateProjection(
        DocumentLifecycleState LifecycleState,
        DocumentProcessingState ProcessingState,
        DocumentKnowledgeAvailabilityState KnowledgeState,
        long? LastAvailableProcessingGeneration);
}
