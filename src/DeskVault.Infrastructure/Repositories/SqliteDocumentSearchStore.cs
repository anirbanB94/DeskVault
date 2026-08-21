using DeskVault.Application.Documents.Queries.SearchDocuments;
using DeskVault.Application.Interfaces;
using DeskVault.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace DeskVault.Infrastructure.Repositories;

public sealed class SqliteDocumentSearchStore
    : IDocumentSearchStore
{
    private readonly IDbContextFactory<DeskVaultDbContext> _dbContextFactory;

    public SqliteDocumentSearchStore(
        IDbContextFactory<DeskVaultDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<IReadOnlyList<SearchDocumentsResult>> SearchAsync(
        string searchText,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(searchText);

        cancellationToken.ThrowIfCancellationRequested();

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
                        chunk.Text.Contains(
                            normalizedSearchText))
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

        return matches
            .Select(
                result =>
                    new SearchDocumentsResult(
                        result.DocumentId,
                        result.FileName,
                        result.DisplayName,
                        result.ChunkOrder,
                        result.ChunkText))
            .ToList();
    }
}
