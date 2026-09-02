using DeskVault.Infrastructure.Services;
using SQLitePCL;
using System.Security.Cryptography;
using System.Text;

namespace DeskVault.Infrastructure.Tests;

public sealed class SqliteDatabaseEncryptionMigratorTests
{
    [Fact]
    public async Task MigrateAsync_WhenDatabaseIsBusy_FailsAndPreservesSource()
    {
        SQLitePCL.Batteries_V2.Init();

        string rootDirectory =
            CreateTemporaryDirectory();

        try
        {
            string databasePath =
                Path.Combine(
                    rootDirectory,
                    "DeskVault.db");

            byte[] databaseKey =
                CreateDatabaseKey();

            CreatePlaintextDatabase(
                databasePath);

            sqlite3? lockingDatabase = null;

            try
            {
                int openResult =
                    raw.sqlite3_open(
                        databasePath,
                        out lockingDatabase);

                Assert.Equal(
                    raw.SQLITE_OK,
                    openResult);

                int beginResult =
                    raw.sqlite3_exec(
                        lockingDatabase,
                        "BEGIN EXCLUSIVE;");

                Assert.Equal(
                    raw.SQLITE_OK,
                    beginResult);

                var migrator =
                    new SqliteDatabaseEncryptionMigrator();

                await Assert.ThrowsAsync<InvalidOperationException>(
                    () =>
                        migrator.MigrateAsync(
                            databasePath,
                            databaseKey));

                int rollbackResult =
                    raw.sqlite3_exec(
                        lockingDatabase,
                        "ROLLBACK;");

                Assert.Equal(
                    raw.SQLITE_OK,
                    rollbackResult);
            }
            finally
            {
                if (lockingDatabase is not null)
                {
                    raw.sqlite3_close(
                        lockingDatabase);
                }
            }

            Assert.True(
                IsPlaintextSqliteDatabase(
                    databasePath));

            Assert.Equal(
                1,
                ReadPlaintextDataCount(
                    databasePath));
        }
        finally
        {
            DeleteTemporaryDirectory(
                rootDirectory);
        }
    }

    [Fact]
    public async Task MigrateAsync_WhenBusyMigrationFails_CanRetrySuccessfully()
    {
        SQLitePCL.Batteries_V2.Init();

        string rootDirectory =
            CreateTemporaryDirectory();

        try
        {
            string databasePath =
                Path.Combine(
                    rootDirectory,
                    "DeskVault.db");

            byte[] databaseKey =
                CreateDatabaseKey();

            CreatePlaintextDatabase(
                databasePath);

            sqlite3? lockingDatabase = null;

            try
            {
                int openResult =
                    raw.sqlite3_open(
                        databasePath,
                        out lockingDatabase);

                Assert.Equal(
                    raw.SQLITE_OK,
                    openResult);

                int beginResult =
                    raw.sqlite3_exec(
                        lockingDatabase,
                        "BEGIN EXCLUSIVE;");

                Assert.Equal(
                    raw.SQLITE_OK,
                    beginResult);

                var migrator =
                    new SqliteDatabaseEncryptionMigrator();

                await Assert.ThrowsAsync<InvalidOperationException>(
                    () =>
                        migrator.MigrateAsync(
                            databasePath,
                            databaseKey));
            }
            finally
            {
                if (lockingDatabase is not null)
                {
                    int rollbackResult =
                        raw.sqlite3_exec(
                            lockingDatabase,
                            "ROLLBACK;");

                    Assert.Equal(
                        raw.SQLITE_OK,
                        rollbackResult);

                    raw.sqlite3_close(
                        lockingDatabase);
                }
            }

            Assert.True(
                IsPlaintextSqliteDatabase(
                    databasePath));

            var migratorAfterFailure =
                new SqliteDatabaseEncryptionMigrator();

            await migratorAfterFailure.MigrateAsync(
                databasePath,
                databaseKey);

            Assert.False(
                IsPlaintextSqliteDatabase(
                    databasePath));

            sqlite3? database = null;

            try
            {
                int openResult =
                    raw.sqlite3_open(
                        databasePath,
                        out database);

                Assert.Equal(
                    raw.SQLITE_OK,
                    openResult);

                byte[] databasePasswordBytes =
                    Encoding.UTF8.GetBytes(
                        Convert.ToBase64String(
                            databaseKey));

                try
                {
                    int keyResult =
                        raw.sqlite3_key(
                            database,
                            databasePasswordBytes);

                    Assert.Equal(
                        raw.SQLITE_OK,
                        keyResult);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(
                        databasePasswordBytes);
                }

                int dataCount =
                    ExecuteScalarInt(
                        database,
                        "SELECT COUNT(*) FROM TestData;");

                Assert.Equal(
                    1,
                    dataCount);
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
        finally
        {
            DeleteTemporaryDirectory(
                rootDirectory);
        }
    }

    [Fact]
    public async Task MigrateAsync_WhenPlaintextDatabaseExists_EncryptsAndPreservesData()
    {
        SQLitePCL.Batteries_V2.Init();

        string rootDirectory =
            CreateTemporaryDirectory();

        try
        {
            string databasePath =
                Path.Combine(
                    rootDirectory,
                    "DeskVault.db");

            byte[] databaseKey =
                CreateDatabaseKey();

            CreatePlaintextDatabase(
                databasePath);

            var migrator =
                new SqliteDatabaseEncryptionMigrator();

            await migrator.MigrateAsync(
                databasePath,
                databaseKey);

            byte[] header =
                new byte[16];

            await using (
                var stream =
                    new FileStream(
                        databasePath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        bufferSize: 16,
                        useAsync: true))
            {
                int bytesRead =
                    await stream.ReadAsync(header);

                Assert.Equal(
                    16,
                    bytesRead);
            }

            Assert.False(
                header.AsSpan()
                    .SequenceEqual(
                        "SQLite format 3\0"u8));

            sqlite3? database = null;

            try
            {
                int openResult =
                    raw.sqlite3_open(
                        databasePath,
                        out database);

                Assert.Equal(
                    raw.SQLITE_OK,
                    openResult);

                byte[] databasePasswordBytes =
                    Encoding.UTF8.GetBytes(
                        Convert.ToBase64String(
                            databaseKey));

                try
                {
                    int keyResult =
                        raw.sqlite3_key(
                            database,
                            databasePasswordBytes);

                    Assert.Equal(
                        raw.SQLITE_OK,
                        keyResult);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(
                        databasePasswordBytes);
                }

                int queryResult =
                    ExecuteScalarInt(
                        database,
                        "SELECT COUNT(*) FROM TestData;");

                Assert.Equal(
                    1,
                    queryResult);
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
        finally
        {
            DeleteTemporaryDirectory(
                rootDirectory);
        }
    }

    private static void CreatePlaintextDatabase(
        string databasePath)
    {
        sqlite3? database = null;

        try
        {
            int openResult =
                raw.sqlite3_open(
                    databasePath,
                    out database);

            Assert.Equal(
                raw.SQLITE_OK,
                openResult);

            int createResult =
                raw.sqlite3_exec(
                    database,
                    """
                    CREATE TABLE TestData (
                        Id INTEGER NOT NULL PRIMARY KEY,
                        Value TEXT NOT NULL
                    );

                    INSERT INTO TestData (Id, Value)
                    VALUES (1, 'DeskVault');
                    """);

            Assert.Equal(
                raw.SQLITE_OK,
                createResult);
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

    private static bool IsPlaintextSqliteDatabase(
        string databasePath)
    {
        byte[] header =
            new byte[16];

        using var stream =
            new FileStream(
                databasePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);

        int bytesRead =
            stream.Read(
                header,
                0,
                header.Length);

        return bytesRead == header.Length &&
               header.AsSpan()
                   .SequenceEqual(
                       "SQLite format 3\0"u8);
    }

    private static int ReadPlaintextDataCount(
        string databasePath)
    {
        sqlite3? database = null;

        try
        {
            int openResult =
                raw.sqlite3_open(
                    databasePath,
                    out database);

            Assert.Equal(
                raw.SQLITE_OK,
                openResult);

            return ExecuteScalarInt(
                database,
                "SELECT COUNT(*) FROM TestData;");
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

    private static int ExecuteScalarInt(
        sqlite3 database,
        string sql)
    {
        sqlite3_stmt? statement = null;

        try
        {
            int prepareResult =
                raw.sqlite3_prepare_v2(
                    database,
                    sql,
                    out statement);

            Assert.Equal(
                raw.SQLITE_OK,
                prepareResult);

            int stepResult =
                raw.sqlite3_step(
                    statement);

            Assert.Equal(
                raw.SQLITE_ROW,
                stepResult);

            return raw.sqlite3_column_int(
                statement,
                0);
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

    private static byte[] CreateDatabaseKey()
    {
        return Enumerable
            .Range(1, 32)
            .Select(
                value => (byte)value)
            .ToArray();
    }

    private static string CreateTemporaryDirectory()
    {
        string directory =
            Path.Combine(
                Path.GetTempPath(),
                "DeskVaultTests",
                Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(
            directory);

        return directory;
    }

    private static void DeleteTemporaryDirectory(
        string directory)
    {
        if (Directory.Exists(
                directory))
        {
            Directory.Delete(
                directory,
                recursive: true);
        }
    }
}
