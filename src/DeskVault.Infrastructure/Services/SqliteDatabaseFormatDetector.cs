namespace DeskVault.Infrastructure.Services;

public sealed class SqliteDatabaseFormatDetector :
    IDatabaseFormatDetector
{
    private static readonly byte[] PlaintextSqliteHeader =
        "SQLite format 3\0"u8.ToArray();

    public async Task<bool> IsPlaintextSqliteAsync(
        string databasePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(databasePath))
        {
            return false;
        }

        byte[] header =
            new byte[PlaintextSqliteHeader.Length];

        await using var stream =
            new FileStream(
                databasePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: PlaintextSqliteHeader.Length,
                useAsync: true);

        int bytesRead =
            await stream.ReadAsync(
                header,
                cancellationToken);

        if (bytesRead != PlaintextSqliteHeader.Length)
        {
            return false;
        }

        return header.AsSpan()
            .SequenceEqual(
                PlaintextSqliteHeader);
    }
}
