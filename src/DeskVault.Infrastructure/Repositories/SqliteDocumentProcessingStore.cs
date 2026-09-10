using DeskVault.Application.Documents.Chunking;
using DeskVault.Application.Documents.Processing;
using DeskVault.Application.Interfaces;
using DeskVault.Domain.Documents;
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

    public async Task<long> AcquireProcessingGenerationAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var dbContext =
            await _dbContextFactory.CreateDbContextAsync(
                cancellationToken);

        await using var command =
            dbContext.Database.GetDbConnection().CreateCommand();

        command.CommandText =
            """
            UPDATE Documents
            SET ProcessingGeneration = ProcessingGeneration + 1
            WHERE Id = $documentId
            RETURNING ProcessingGeneration;
            """;

        var parameter =
            command.CreateParameter();

        parameter.ParameterName = "$documentId";
        parameter.Value = documentId;

        command.Parameters.Add(
            parameter);

        if (command.Connection!.State !=
            System.Data.ConnectionState.Open)
        {
            await command.Connection.OpenAsync(
                cancellationToken);
        }

        object? result =
            await command.ExecuteScalarAsync(
                cancellationToken);

        if (result is null ||
            result is DBNull)
        {
            throw new InvalidOperationException(
                $"Document '{documentId}' was not found.");
        }

        return Convert.ToInt64(
            result,
            System.Globalization.CultureInfo.InvariantCulture);
    }

    public async Task PublishProcessingStateAsync(
        Guid documentId,
        long processingGeneration,
        DocumentStatus status,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var dbContext =
            await _dbContextFactory.CreateDbContextAsync(
                cancellationToken);

        int rowsAffected =
            await dbContext.Documents
                .Where(
                    document =>
                        document.Id == documentId &&
                        document.ProcessingGeneration ==
                        processingGeneration)
                .ExecuteUpdateAsync(
                    setters =>
                        setters.SetProperty(
                            document => document.Status,
                            (int)status),
                    cancellationToken);

        if (rowsAffected != 0)
        {
            return;
        }

        DocumentEntity? document =
            await dbContext.Documents
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    entity => entity.Id == documentId,
                    cancellationToken);

        if (document is null)
        {
            throw new InvalidOperationException(
                $"Document '{documentId}' was not found.");
        }

        throw new StaleProcessingGenerationException(
            documentId,
            processingGeneration,
            document.ProcessingGeneration);
    }

    public async Task PublishSuccessfulProcessingAsync(
        Guid documentId,
        long processingGeneration,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var dbContext =
            await _dbContextFactory.CreateDbContextAsync(
                cancellationToken);

        int rowsAffected =
            await dbContext.Documents
                .Where(
                    document =>
                        document.Id == documentId &&
                        document.ProcessingGeneration ==
                        processingGeneration)
                .ExecuteUpdateAsync(
                    setters =>
                        setters
                            .SetProperty(
                                document => document.Status,
                                (int)DocumentStatus.Available)
                            .SetProperty(
                                document =>
                                    document.LastSuccessfulProcessingGeneration,
                                processingGeneration),
                    cancellationToken);

        if (rowsAffected != 0)
        {
            return;
        }

        DocumentEntity? document =
            await dbContext.Documents
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    entity => entity.Id == documentId,
                    cancellationToken);

        if (document is null)
        {
            throw new InvalidOperationException(
                $"Document '{documentId}' was not found.");
        }

        throw new StaleProcessingGenerationException(
            documentId,
            processingGeneration,
            document.ProcessingGeneration);
    }

    public async Task RecoverCancelledProcessingAsync(
        Guid documentId,
        long processingGeneration,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var dbContext =
            await _dbContextFactory.CreateDbContextAsync(
                cancellationToken);

        DocumentEntity? document =
            await dbContext.Documents
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    entity => entity.Id == documentId,
                    cancellationToken);

        if (document is null)
        {
            throw new InvalidOperationException(
                $"Document '{documentId}' was not found.");
        }

        if (document.ProcessingGeneration !=
            processingGeneration)
        {
            throw new StaleProcessingGenerationException(
                documentId,
                processingGeneration,
                document.ProcessingGeneration);
        }

        DocumentStatus recoveryStatus =
            document.LastSuccessfulProcessingGeneration > 0L
                ? DocumentStatus.Available
                : DocumentStatus.Imported;

        int rowsAffected =
            await dbContext.Documents
                .Where(
                    entity =>
                        entity.Id == documentId &&
                        entity.ProcessingGeneration ==
                        processingGeneration)
                .ExecuteUpdateAsync(
                    setters =>
                        setters.SetProperty(
                            entity => entity.Status,
                            (int)recoveryStatus),
                    cancellationToken);

        if (rowsAffected != 0)
        {
            return;
        }

        DocumentEntity? currentDocument =
            await dbContext.Documents
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    entity => entity.Id == documentId,
                    cancellationToken);

        if (currentDocument is null)
        {
            throw new InvalidOperationException(
                $"Document '{documentId}' was not found.");
        }

        throw new StaleProcessingGenerationException(
            documentId,
            processingGeneration,
            currentDocument.ProcessingGeneration);
    }

    public async Task ReplaceChunksAsync(
        Guid documentId,
        long processingGeneration,
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

            DocumentEntity? document =
                await dbContext.Documents
                    .AsNoTracking()
                    .SingleOrDefaultAsync(
                        entity => entity.Id == documentId,
                        cancellationToken);

            if (document is null)
            {
                throw new InvalidOperationException(
                    $"Document '{documentId}' was not found.");
            }

            if (document.ProcessingGeneration !=
                processingGeneration)
            {
                throw new StaleProcessingGenerationException(
                    documentId,
                    processingGeneration,
                    document.ProcessingGeneration);
            }

            await dbContext.DocumentChunks
                .Where(
                    chunk => chunk.DocumentId == documentId)
                .ExecuteDeleteAsync(
                    cancellationToken);

            foreach (DocumentChunk chunk in chunks)
            {
                cancellationToken.ThrowIfCancellationRequested();

                Guid logicalId =
                    DocumentChunkIdentity.CreateLogicalId(
                        documentId,
                        chunk.Order);

                string contentHash =
                    DocumentChunkIdentity.ComputeContentHash(
                        chunk.Text);

                await dbContext.DocumentChunks.AddAsync(
                    new DocumentChunkEntity
                    {
                        Id = logicalId,
                        DocumentId = documentId,
                        Order = chunk.Order,
                        Text = chunk.Text,
                        ContentHash = contentHash,
                        ProcessingGeneration =
                            processingGeneration
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
