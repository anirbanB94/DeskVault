using DeskVault.Application.Interfaces;
using DeskVault.Domain.Documents;
using DeskVault.Infrastructure.Persistence.Context;
using DeskVault.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeskVault.Infrastructure.Repositories;

public sealed class SqliteDocumentRepository
    : IDocumentRepository
{
    private readonly IDbContextFactory<DeskVaultDbContext> _dbContextFactory;

    public SqliteDocumentRepository(
        IDbContextFactory<DeskVaultDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
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
            StoredFilePath = document.StoredFilePath
        };

        await dbContext.Documents.AddAsync(
            entity,
            cancellationToken);

        await dbContext.SaveChangesAsync(
            cancellationToken);
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

        return entity is null
            ? null
            : ToDomain(entity);
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
            .ToListAsync(cancellationToken);

        return entities
            .Select(ToDomain)
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
    }

    private static Document ToDomain(
        DocumentEntity entity)
    {
        return Document.Restore(
            entity.Id,
            entity.FileName,
            entity.DisplayName,
            entity.Sha256Hash,
            entity.StoredFilePath,
            entity.ImportedAt,
            (DocumentStatus)entity.Status);
    }
}
