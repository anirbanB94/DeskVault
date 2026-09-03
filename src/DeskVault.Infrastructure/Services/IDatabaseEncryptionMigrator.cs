namespace DeskVault.Infrastructure.Services;

public interface IDatabaseEncryptionMigrator
{
    Task MigrateAsync(
        string databasePath,
        byte[] databaseKey,
        CancellationToken cancellationToken = default);
}
