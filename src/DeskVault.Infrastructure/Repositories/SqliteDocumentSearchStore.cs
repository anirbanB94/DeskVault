using DeskVault.Application.Documents.Queries.SearchDocuments;
using DeskVault.Application.Interfaces;
using DeskVault.Infrastructure.Persistence.Context;
using DeskVault.Shared.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DeskVault.Infrastructure.Repositories;

public sealed class SqliteDocumentSearchStore
    : IDocumentSearchStore
{
    private readonly IDbContextFactory<DeskVaultDbContext> _dbContextFactory;
    private readonly ILogger<SqliteDocumentSearchStore> _logger;

    public SqliteDocumentSearchStore(
        IDbContextFactory<DeskVaultDbContext> dbContextFactory,
        ILogger<SqliteDocumentSearchStore> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SearchDocumentsResult>> SearchAsync(
        string searchText,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(searchText);

        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation(
            LogMessages.DocumentSearchStoreStarted);

        try
        {
            await using var dbContext =
                await _dbContextFactory.CreateDbContextAsync(
                    cancellationToken);

            string normalizedSearchText =
                searchText.Trim();

            var matches =
                await dbContext.DocumentChunks
                    .AsNoTracking()
                    .Where(
                        chunk =>
                            EF.Functions.Like(
                                chunk.Text,
                                $"%{normalizedSearchText}%"))
                    .Join(
                        dbContext.Documents,
                        chunk => chunk.DocumentId,
                        document => document.Id,
                        (chunk, document) =>
                            new
                            {
                                DocumentId = document.Id,
                                FileName = document.FileName,
                                DisplayName = document.DisplayName,
                                ChunkOrder = chunk.Order,
                                ChunkText = chunk.Text
                            })
                    .OrderBy(
                        result => result.DisplayName)
                    .ThenBy(
                        result => result.ChunkOrder)
                    .ToListAsync(
                        cancellationToken);

            var results =
                matches
                    .Select(
                        result =>
                            new SearchDocumentsResult(
                                result.DocumentId,
                                result.FileName,
                                result.DisplayName,
                                result.ChunkOrder,
                                result.ChunkText))
                    .ToList();

            _logger.LogInformation(
                LogMessages.DocumentSearchStoreCompleted,
                results.Count);

            return results;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                LogMessages.DocumentSearchStoreFailed);

            throw;
        }
    }
}
