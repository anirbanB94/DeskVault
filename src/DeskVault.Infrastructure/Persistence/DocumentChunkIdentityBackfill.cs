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

        bool alreadyBackfilled =
            chunks.All(
                chunk =>
                    chunk.Id ==
                    DocumentChunkIdentity.CreateLogicalId(
                        chunk.DocumentId,
                        chunk.Order) &&
                    chunk.ContentHash ==
                    DocumentChunkIdentity.ComputeContentHash(
                        chunk.Text) &&
                    chunk.ProcessingGeneration == 0L);

        if (alreadyBackfilled)
        {
            return;
        }

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        await dbContext.DocumentChunks
            .ExecuteDeleteAsync(
                cancellationToken);

        foreach (DocumentChunkEntity chunk in chunks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await dbContext.DocumentChunks.AddAsync(
                new DocumentChunkEntity
                {
                    Id =
                        DocumentChunkIdentity.CreateLogicalId(
                            chunk.DocumentId,
                            chunk.Order),
                    DocumentId = chunk.DocumentId,
                    Order = chunk.Order,
                    Text = chunk.Text,
                    ContentHash =
                        DocumentChunkIdentity.ComputeContentHash(
                            chunk.Text),
                    ProcessingGeneration = 0L
                },
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);
    }
}
