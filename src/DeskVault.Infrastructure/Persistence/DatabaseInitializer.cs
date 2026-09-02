using System.Security.Cryptography;
using DeskVault.Infrastructure.Persistence.Context;
using DeskVault.Infrastructure.Services;
using DeskVault.Shared.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DeskVault.Infrastructure.Persistence;

public sealed class DatabaseInitializer
{
    private readonly IDbContextFactory<DeskVaultDbContext> _dbContextFactory;
    private readonly DeskVaultDataPaths _dataPaths;
    private readonly IDatabaseFormatDetector _databaseFormatDetector;
    private readonly IDatabaseEncryptionKeyService _databaseEncryptionKeyService;
    private readonly IDatabaseEncryptionMigrator _databaseEncryptionMigrator;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(
        IDbContextFactory<DeskVaultDbContext> dbContextFactory,
        DeskVaultDataPaths dataPaths,
        IDatabaseFormatDetector databaseFormatDetector,
        IDatabaseEncryptionKeyService databaseEncryptionKeyService,
        IDatabaseEncryptionMigrator databaseEncryptionMigrator,
        ILogger<DatabaseInitializer> logger)
    {
        _dbContextFactory = dbContextFactory;
        _dataPaths = dataPaths;
        _databaseFormatDetector = databaseFormatDetector;
        _databaseEncryptionKeyService = databaseEncryptionKeyService;
        _databaseEncryptionMigrator = databaseEncryptionMigrator;
        _logger = logger;
    }

    public async Task InitializeAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation(
            LogMessages.DatabaseInitializationStarted);

        try
        {
            bool isPlaintextDatabase =
                await _databaseFormatDetector.IsPlaintextSqliteAsync(
                    _dataPaths.DatabasePath,
                    cancellationToken);

            if (isPlaintextDatabase)
            {
                _logger.LogInformation(
                    LogMessages.DatabasePlaintextMigrationStarted);

                byte[] databaseKey =
                    await _databaseEncryptionKeyService.GetOrCreateKeyAsync(
                        cancellationToken);

                try
                {
                    await _databaseEncryptionMigrator.MigrateAsync(
                        _dataPaths.DatabasePath,
                        databaseKey,
                        cancellationToken);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(
                        databaseKey);
                }

                _logger.LogInformation(
                    LogMessages.DatabasePlaintextMigrationCompleted);
            }

            await using var dbContext =
                await _dbContextFactory.CreateDbContextAsync(
                    cancellationToken);

            await dbContext.Database.MigrateAsync(
                cancellationToken);

            _logger.LogInformation(
                LogMessages.DatabaseInitializationCompleted);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                LogMessages.DatabaseInitializationFailed);

            throw;
        }
    }
}
