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
            StoredFilePath = document.StoredFilePath,
            ProcessingGeneration = document.ProcessingGeneration,
            LastSuccessfulProcessingGeneration =
                document.LastSuccessfulProcessingGeneration
        };

        await dbContext.Documents.AddAsync(
            entity,
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
                existing => existing.Id == document.Id,
                cancellationToken);

        if (entity is null)
        {
            _logger.LogWarning(
                LogMessages.DocumentRepositoryUpdateNotFound);

            throw new InvalidOperationException(
                $"Document '{document.Id}' was not found.");
        }

        entity.FileName = document.FileName;
        entity.DisplayName = document.DisplayName;
        entity.Sha256Hash = document.Sha256Hash;
        entity.ImportedAt = document.ImportedAt;
        entity.Status = (int)document.Status;
        entity.StoredFilePath = document.StoredFilePath;

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
        DocumentEntity entity)
    {
        return Document.Restore(
            entity.Id,
            entity.FileName,
            entity.DisplayName,
            entity.Sha256Hash,
            entity.StoredFilePath,
            entity.ImportedAt,
            (DocumentStatus)entity.Status,
            entity.ProcessingGeneration,
            entity.LastSuccessfulProcessingGeneration);
    }
}
