namespace DeskVault.Infrastructure.Services;

public interface IDatabaseFormatDetector
{
    Task<bool> IsPlaintextSqliteAsync(
        string databasePath,
        CancellationToken cancellationToken = default);
}
