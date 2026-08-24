using DeskVault.Application.Documents.Chunking;
using DeskVault.Application.Interfaces;
using DeskVault.Infrastructure.Persistence.Context;
using DeskVault.Infrastructure.Persistence.Entities;
using DeskVault.Shared.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DeskVault.Infrastructure.Repositories;

public sealed class SqliteDocumentProcessingStore
    : IDocumentProcessingStore
{
    private readonly IDbContextFactory<DeskVaultDbContext> _dbContextFactory;
    private readonly ILogger<SqliteDocumentProcessingStore> _logger;

    public SqliteDocumentProcessingStore(
        IDbContextFactory<DeskVaultDbContext> dbContextFactory,
        ILogger<SqliteDocumentProcessingStore> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task ReplaceChunksAsync(
        Guid documentId,
        IReadOnlyList<DocumentChunk> chunks,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chunks);

        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation(
            LogMessages.DocumentChunkReplacementStarted);

        try
        {
            await using var dbContext =
                await _dbContextFactory.CreateDbContextAsync(
                    cancellationToken);

            await using var transaction =
                await dbContext.Database.BeginTransactionAsync(
                    cancellationToken);

            await dbContext.DocumentChunks
                .Where(
                    chunk => chunk.DocumentId == documentId)
                .ExecuteDeleteAsync(
                    cancellationToken);

            foreach (DocumentChunk chunk in chunks)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await dbContext.DocumentChunks.AddAsync(
                    new DocumentChunkEntity
                    {
                        Id = Guid.NewGuid(),
                        DocumentId = documentId,
                        Order = chunk.Order,
                        Text = chunk.Text
                    },
                    cancellationToken);
            }

            await dbContext.SaveChangesAsync(
                cancellationToken);

            await transaction.CommitAsync(
                cancellationToken);

            _logger.LogInformation(
                LogMessages.DocumentChunkReplacementCompleted,
                chunks.Count);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                LogMessages.DocumentChunkReplacementFailed);

            throw;
        }
    }
}
