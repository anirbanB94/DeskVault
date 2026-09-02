using System.Text;
using DeskVault.Infrastructure.Services;

namespace DeskVault.Infrastructure.Tests;

public sealed class SqliteDatabaseFormatDetectorTests
{
    [Fact]
    public async Task IsPlaintextSqliteAsync_WhenDatabaseHasPlaintextHeader_ReturnsTrue()
    {
        string rootDirectory =
            CreateTemporaryDirectory();

        try
        {
            string databasePath =
                Path.Combine(
                    rootDirectory,
                    "DeskVault.db");

            await File.WriteAllBytesAsync(
                databasePath,
                "SQLite format 3\0"u8.ToArray());

            var detector =
                new SqliteDatabaseFormatDetector();

            bool result =
                await detector.IsPlaintextSqliteAsync(
                    databasePath);

            Assert.True(result);
        }
        finally
        {
            DeleteTemporaryDirectory(
                rootDirectory);
        }
    }

    [Fact]
    public async Task IsPlaintextSqliteAsync_WhenDatabaseHasEncryptedHeader_ReturnsFalse()
    {
        string rootDirectory =
            CreateTemporaryDirectory();

        try
        {
            string databasePath =
                Path.Combine(
                    rootDirectory,
                    "DeskVault.db");

            byte[] encryptedHeader =
                Convert.FromHexString(
                    "9FF1BF14E94BB43B10544519F71E62E7");

            await File.WriteAllBytesAsync(
                databasePath,
                encryptedHeader);

            var detector =
                new SqliteDatabaseFormatDetector();

            bool result =
                await detector.IsPlaintextSqliteAsync(
                    databasePath);

            Assert.False(result);
        }
        finally
        {
            DeleteTemporaryDirectory(
                rootDirectory);
        }
    }

    [Fact]
    public async Task IsPlaintextSqliteAsync_WhenDatabaseDoesNotExist_ReturnsFalse()
    {
        string rootDirectory =
            CreateTemporaryDirectory();

        try
        {
            string databasePath =
                Path.Combine(
                    rootDirectory,
                    "DeskVault.db");

            var detector =
                new SqliteDatabaseFormatDetector();

            bool result =
                await detector.IsPlaintextSqliteAsync(
                    databasePath);

            Assert.False(result);
        }
        finally
        {
            DeleteTemporaryDirectory(
                rootDirectory);
        }
    }

    [Fact]
    public async Task IsPlaintextSqliteAsync_WhenFileIsTooShort_ReturnsFalse()
    {
        string rootDirectory =
            CreateTemporaryDirectory();

        try
        {
            string databasePath =
                Path.Combine(
                    rootDirectory,
                    "DeskVault.db");

            await File.WriteAllBytesAsync(
                databasePath,
                Encoding.ASCII.GetBytes(
                    "SQLite format"));

            var detector =
                new SqliteDatabaseFormatDetector();

            bool result =
                await detector.IsPlaintextSqliteAsync(
                    databasePath);

            Assert.False(result);
        }
        finally
        {
            DeleteTemporaryDirectory(
                rootDirectory);
        }
    }

    [Fact]
    public async Task IsPlaintextSqliteAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        string rootDirectory =
            CreateTemporaryDirectory();

        try
        {
            string databasePath =
                Path.Combine(
                    rootDirectory,
                    "DeskVault.db");

            await File.WriteAllBytesAsync(
                databasePath,
                "SQLite format 3\0"u8.ToArray());

            var detector =
                new SqliteDatabaseFormatDetector();

            using var cancellationTokenSource =
                new CancellationTokenSource();

            cancellationTokenSource.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () =>
                    detector.IsPlaintextSqliteAsync(
                        databasePath,
                        cancellationTokenSource.Token));
        }
        finally
        {
            DeleteTemporaryDirectory(
                rootDirectory);
        }
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
        if (Directory.Exists(directory))
        {
            Directory.Delete(
                directory,
                recursive: true);
        }
    }
}
