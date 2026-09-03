using System.Security.Cryptography;
using DeskVault.Infrastructure.Persistence.Context;
using DeskVault.Infrastructure.Services;
using DeskVault.Shared.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DeskVault.Infrastructure.Persistence;

public sealed class DatabaseInitializer
{
    private const string MigrationSuffix =
        ".migration";

    private const string MigrationBackupSuffix =
        ".migration-backup";

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
            bool migrationBackupExists =
                await PrepareDatabaseMigrationAsync(
                    cancellationToken);

            await using var dbContext =
                await _dbContextFactory.CreateDbContextAsync(
                    cancellationToken);

            await dbContext.Database.MigrateAsync(
                cancellationToken);

            if (migrationBackupExists)
            {
                CleanupMigrationBackup();
            }

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

    private async Task<bool> PrepareDatabaseMigrationAsync(
        CancellationToken cancellationToken)
    {
        string databasePath =
            _dataPaths.DatabasePath;

        string migrationPath =
            databasePath +
            MigrationSuffix;

        string migrationBackupPath =
            databasePath +
            MigrationBackupSuffix;

        bool databaseExists =
            File.Exists(databasePath);

        bool migrationExists =
            File.Exists(migrationPath);

        bool migrationBackupExists =
            File.Exists(migrationBackupPath);

        if (!databaseExists)
        {
            if (migrationExists ||
                migrationBackupExists)
            {
                throw new InvalidOperationException(
                    "The canonical database is missing while migration recovery artifacts exist. Database initialization cannot safely determine which database is authoritative.");
            }

            return false;
        }

        bool isPlaintextDatabase =
            await _databaseFormatDetector.IsPlaintextSqliteAsync(
                databasePath,
                cancellationToken);

        if (!isPlaintextDatabase)
        {
            if (migrationExists)
            {
                File.Delete(
                    migrationPath);
            }

            return migrationBackupExists;
        }

        if (migrationBackupExists)
        {
            bool backupIsPlaintext =
                await _databaseFormatDetector.IsPlaintextSqliteAsync(
                    migrationBackupPath,
                    cancellationToken);

            if (!backupIsPlaintext)
            {
                throw new InvalidOperationException(
                    "A migration backup exists while the canonical database is plaintext, but the backup is not a plaintext SQLite database.");
            }

            File.Delete(
                migrationBackupPath);
        }

        if (migrationExists)
        {
            File.Delete(
                migrationPath);
        }

        _logger.LogInformation(
            LogMessages.DatabasePlaintextMigrationStarted);

        cancellationToken.ThrowIfCancellationRequested();

        File.Copy(
            databasePath,
            migrationPath,
            overwrite: false);

        cancellationToken.ThrowIfCancellationRequested();

        byte[] databaseKey =
            await _databaseEncryptionKeyService.GetOrCreateKeyAsync(
                cancellationToken);

        try
        {
            await _databaseEncryptionMigrator.MigrateAsync(
                migrationPath,
                databaseKey,
                cancellationToken);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                databaseKey);
        }

        cancellationToken.ThrowIfCancellationRequested();

        bool migrationIsEncrypted =
            !await _databaseFormatDetector.IsPlaintextSqliteAsync(
                migrationPath,
                cancellationToken);

        if (!migrationIsEncrypted)
        {
            throw new InvalidOperationException(
                "Database encryption migration did not produce an encrypted database.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        File.Replace(
            migrationPath,
            databasePath,
            migrationBackupPath,
            ignoreMetadataErrors: false);

        cancellationToken.ThrowIfCancellationRequested();

        bool databaseIsEncrypted =
            !await _databaseFormatDetector.IsPlaintextSqliteAsync(
                databasePath,
                cancellationToken);

        if (!databaseIsEncrypted)
        {
            throw new InvalidOperationException(
                "Database encryption migration promotion did not produce an encrypted database.");
        }

        _logger.LogInformation(
            LogMessages.DatabasePlaintextMigrationCompleted);

        return true;
    }

    private void CleanupMigrationBackup()
    {
        string migrationBackupPath =
            _dataPaths.DatabasePath +
            MigrationBackupSuffix;

        if (!File.Exists(
                migrationBackupPath))
        {
            return;
        }

        File.Delete(
            migrationBackupPath);
    }
}
