using DeskVault.Application.Documents.Chunking;
using DeskVault.Infrastructure.Persistence.Context;
using DeskVault.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeskVault.Infrastructure.Persistence;

public sealed class DocumentChunkIdentityBackfill
{
    private readonly IDbContextFactory<DeskVaultDbContext> _dbContextFactory;

    public DocumentChunkIdentityBackfill(
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

        List<DocumentChunkEntity> chunks =
            await dbContext.DocumentChunks
                .AsNoTracking()
                .OrderBy(chunk => chunk.DocumentId)
                .ThenBy(chunk => chunk.Order)
                .ToListAsync(cancellationToken);

        if (chunks.Count == 0)
        {
            return;
        }

        List<DocumentChunkEntity> legacyChunks =
            chunks
                .Where(IsLegacyChunk)
                .ToList();

        if (legacyChunks.Count == 0)
        {
            return;
        }

        HashSet<Guid> legacyIds =
            legacyChunks
                .Select(chunk => chunk.Id)
                .ToHashSet();

        HashSet<Guid> targetIds =
            new();

        HashSet<Guid> existingIds =
            chunks
                .Where(chunk => !legacyIds.Contains(chunk.Id))
                .Select(chunk => chunk.Id)
                .ToHashSet();

        foreach (DocumentChunkEntity chunk in legacyChunks)
        {
            Guid logicalId =
                DocumentChunkIdentity.CreateLogicalId(
                    chunk.DocumentId,
                    chunk.Order);

            if (!targetIds.Add(logicalId))
            {
                throw new InvalidOperationException(
                    $"Multiple legacy chunks resolve to logical chunk identity '{logicalId}'.");
            }

            if (existingIds.Contains(logicalId))
            {
                throw new InvalidOperationException(
                    $"Legacy chunk '{chunk.Id}' conflicts with existing canonical chunk identity '{logicalId}'.");
            }
        }

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        await dbContext.DocumentChunks
            .Where(
                chunk =>
                    legacyIds.Contains(chunk.Id))
            .ExecuteDeleteAsync(
                cancellationToken);

        foreach (DocumentChunkEntity chunk in legacyChunks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await dbContext.DocumentChunks.AddAsync(
                new DocumentChunkEntity
                {
                    Id =
                        DocumentChunkIdentity.CreateLogicalId(
                            chunk.DocumentId,
                            chunk.Order),
                    DocumentId =
                        chunk.DocumentId,
                    Order =
                        chunk.Order,
                    Text =
                        chunk.Text,
                    ContentHash =
                        DocumentChunkIdentity.ComputeContentHash(
                            chunk.Text),
                    ProcessingGeneration = 0L,
                    SourceLocationStartLine = null,
                    SourceLocationEndLine = null
                },
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(
            cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        await transaction.CommitAsync(
            CancellationToken.None);
    }

    private static bool IsLegacyChunk(
        DocumentChunkEntity chunk)
    {
        if (chunk.ProcessingGeneration != 0L)
        {
            return false;
        }

        Guid logicalId =
            DocumentChunkIdentity.CreateLogicalId(
                chunk.DocumentId,
                chunk.Order);

        string contentHash =
            DocumentChunkIdentity.ComputeContentHash(
                chunk.Text);

        return chunk.Id != logicalId ||
               chunk.ContentHash != contentHash;
    }
}
