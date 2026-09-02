using SQLitePCL;
using System.Security.Cryptography;
using System.Text;

namespace DeskVault.Infrastructure.Services;

public sealed class SqliteDatabaseEncryptionMigrator :
    IDatabaseEncryptionMigrator
{
    public Task MigrateAsync(
        string databasePath,
        byte[] databaseKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ArgumentException.ThrowIfNullOrWhiteSpace(
            databasePath);

        ArgumentNullException.ThrowIfNull(
            databaseKey);

        if (!File.Exists(databasePath))
        {
            throw new FileNotFoundException(
                "The database to migrate was not found.",
                databasePath);
        }

        cancellationToken.ThrowIfCancellationRequested();

        string databasePassword =
            Convert.ToBase64String(
                databaseKey);

        byte[] passwordBytes =
            Encoding.UTF8.GetBytes(
                databasePassword);

        sqlite3? database = null;

        try
        {
            int openResult =
                raw.sqlite3_open(
                    databasePath,
                    out database);

            if (openResult != raw.SQLITE_OK)
            {
                throw CreateSqliteException(
                    database,
                    openResult);
            }

            cancellationToken.ThrowIfCancellationRequested();

            int rekeyResult =
                raw.sqlite3_rekey(
                    database,
                    passwordBytes);

            if (rekeyResult != raw.SQLITE_OK)
            {
                throw CreateSqliteException(
                    database,
                    rekeyResult);
            }

            raw.sqlite3_close(
                database);

            database = null;

            VerifyEncryptedDatabase(
                databasePath,
                passwordBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                passwordBytes);

            if (database is not null)
            {
                raw.sqlite3_close(
                    database);
            }
        }

        return Task.CompletedTask;
    }

    private static void VerifyEncryptedDatabase(
        string databasePath,
        byte[] passwordBytes)
    {
        sqlite3? database = null;

        try
        {
            int openResult =
                raw.sqlite3_open(
                    databasePath,
                    out database);

            if (openResult != raw.SQLITE_OK)
            {
                throw CreateSqliteException(
                    database,
                    openResult);
            }

            int keyResult =
                raw.sqlite3_key(
                    database,
                    passwordBytes);

            if (keyResult != raw.SQLITE_OK)
            {
                throw CreateSqliteException(
                    database,
                    keyResult);
            }

            int verificationResult =
                ExecuteVerificationQuery(
                    database);

            if (verificationResult != raw.SQLITE_OK)
            {
                throw CreateSqliteException(
                    database,
                    verificationResult);
            }
        }
        finally
        {
            if (database is not null)
            {
                raw.sqlite3_close(
                    database);
            }
        }
    }

    private static int ExecuteVerificationQuery(
        sqlite3 database)
    {
        sqlite3_stmt? statement = null;

        try
        {
            int prepareResult =
                raw.sqlite3_prepare_v2(
                    database,
                    "PRAGMA schema_version;",
                    out statement);

            if (prepareResult != raw.SQLITE_OK)
            {
                return prepareResult;
            }

            return raw.sqlite3_step(
                       statement) == raw.SQLITE_ROW
                ? raw.SQLITE_OK
                : raw.sqlite3_errcode(
                    database);
        }
        finally
        {
            if (statement is not null)
            {
                raw.sqlite3_finalize(
                    statement);
            }
        }
    }

    private static Exception CreateSqliteException(
        sqlite3? database,
        int resultCode)
    {
        string message =
            database is null
                ? $"SQLite operation failed with result code {resultCode}."
                : raw.sqlite3_errmsg(database).utf8_to_string();

        return new InvalidOperationException(
            $"SQLite database encryption migration failed. Result code: {resultCode}. Error: {message}");
    }
}
