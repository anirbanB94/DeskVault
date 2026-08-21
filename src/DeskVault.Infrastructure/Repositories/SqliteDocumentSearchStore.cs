using DeskVault.Application.Interfaces;
using DeskVault.Application.Documents.Queries.SearchDocuments;
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

        return await dbContext.DocumentChunks
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
                    new SearchDocumentsResult(
                        document.Id,
                        document.FileName,
                        document.DisplayName,
                        chunk.Order,
                        chunk.Text))
            .OrderBy(
                result => result.DisplayName)
            .ThenBy(
                result => result.ChunkOrder)
            .ToListAsync(
                cancellationToken);
    }
}
