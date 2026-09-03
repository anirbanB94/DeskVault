using System.Security.Cryptography;
using System.Text;
using DeskVault.Application;
using DeskVault.Application.Documents.Queries.SearchDocuments;
using DeskVault.Application.Interfaces;
using DeskVault.Domain.Documents;
using DeskVault.Infrastructure;
using DeskVault.Infrastructure.Persistence;
using DeskVault.Infrastructure.Persistence.Context;
using DeskVault.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SQLitePCL;

namespace DeskVault.Infrastructure.Tests;

public sealed class PlaintextDatabaseMigrationIntegrationTests
{
    [Fact]
    public async Task PlaintextDatabase_WhenInitializedThroughProductionInfrastructurePath_IsMigratedAndPreserved()
    {
        SQLitePCL.Batteries_V2.Init();

        string rootDirectory =
            CreateTemporaryDirectory();

        byte[] databaseKey =
            RandomNumberGenerator.GetBytes(32);

        Guid documentId =
            Guid.NewGuid();

        Guid firstChunkId =
            Guid.NewGuid();

        Guid secondChunkId =
            Guid.NewGuid();

        DateTime importedAt =
            new DateTime(
                2026,
                8,
                1,
                10,
                30,
                0,
                DateTimeKind.Utc);

        string databasePath =
            Path.Combine(
                rootDirectory,
                "DeskVault.db");

        try
        {
            CreatePlaintextDeskVaultDatabase(
                databasePath,
                documentId,
                firstChunkId,
                secondChunkId,
                importedAt);

            Assert.True(
                IsPlaintextSqliteDatabase(
                    databasePath));

            AssertPlaintextDocumentExists(
                databasePath,
                documentId);

            Assert.False(
                File.Exists(
                    databasePath + ".plaintext"));

            ServiceProvider serviceProvider =
                BuildServiceProvider(
                    rootDirectory,
                    databaseKey);

            await using (serviceProvider)
            {
                var initializer =
                    serviceProvider.GetRequiredService<DatabaseInitializer>();

                await initializer.InitializeAsync();
            }

            Assert.False(
                IsPlaintextSqliteDatabase(
                    databasePath));

            Assert.True(
                File.Exists(
                    databasePath));

            Assert.False(
                File.Exists(
                    databasePath + ".plaintext"));

            AssertEncryptedDocumentExists(
                databasePath,
                documentId,
                databaseKey);

            ServiceProvider verificationServiceProvider =
                BuildServiceProvider(
                    rootDirectory,
                    databaseKey);

            await using (verificationServiceProvider)
            {
                var initializer =
                    verificationServiceProvider.GetRequiredService<DatabaseInitializer>();

                await initializer.InitializeAsync();

                Assert.False(
                    IsPlaintextSqliteDatabase(
                        databasePath));

                var repository =
                    verificationServiceProvider.GetRequiredService<IDocumentRepository>();

                var searchHandler =
                    verificationServiceProvider.GetRequiredService<SearchDocumentsHandler>();

                IReadOnlyList<Document> allDocuments =
                    await repository.GetAllAsync();

                Document allDocumentsMatch =
                    Assert.Single(
                        allDocuments);

                Assert.Equal(
                    documentId,
                    allDocumentsMatch.Id);

                Document? document =
                    await repository.GetByIdAsync(
                        documentId);

                Assert.NotNull(
                    document);

                Assert.Equal(
                    documentId,
                    document.Id);

                Assert.Equal(
                    "migration-test.txt",
                    document.FileName);

                Assert.Equal(
                    "Plaintext Migration Test",
                    document.DisplayName);

                Assert.Equal(
                    "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                    document.Sha256Hash);

                Assert.Equal(
                    importedAt,
                    document.ImportedAt);

                Assert.Equal(
                    DocumentStatus.Available,
                    document.Status);

                Assert.Equal(
                    Path.Combine(
                        rootDirectory,
                        "Documents",
                        "migration-test.dvault"),
                    document.StoredFilePath);

                IReadOnlyList<SearchDocumentsResult> searchResults =
                    await searchHandler.HandleAsync(
                        new SearchDocumentsQuery(
                            "plaintext migration"));

                SearchDocumentsResult matchingResult =
                    Assert.Single(
                        searchResults,
                        result =>
                            result.DocumentId == documentId);

                Assert.Equal(
                    "migration-test.txt",
                    matchingResult.FileName);

                Assert.Equal(
                    "Plaintext Migration Test",
                    matchingResult.DisplayName);

                Assert.Contains(
                    "plaintext migration",
                    matchingResult.ChunkText,
                    StringComparison.OrdinalIgnoreCase);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                databaseKey);

            DeleteTemporaryDirectory(
                rootDirectory);
        }
    }

    private static void CreatePlaintextDeskVaultDatabase(
        string databasePath,
        Guid documentId,
        Guid firstChunkId,
        Guid secondChunkId,
        DateTime importedAt)
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

            string documentIdValue =
                documentId
                    .ToString()
                    .ToUpperInvariant();

            string firstChunkIdValue =
                firstChunkId
                    .ToString()
                    .ToUpperInvariant();

            string secondChunkIdValue =
                secondChunkId
                    .ToString()
                    .ToUpperInvariant();

            string importedAtValue =
                importedAt.ToString(
                    "O");

            string storedFilePath =
                Path.Combine(
                    Path.GetDirectoryName(
                        databasePath)!,
                    "Documents",
                    "migration-test.dvault");

            string sql =
                $"""
                CREATE TABLE "__EFMigrationsHistory" (
                    "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                    "ProductVersion" TEXT NOT NULL
                );

                CREATE TABLE "Documents" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_Documents" PRIMARY KEY,
                    "FileName" TEXT NOT NULL,
                    "DisplayName" TEXT NOT NULL,
                    "Sha256Hash" TEXT NOT NULL,
                    "ImportedAt" TEXT NOT NULL,
                    "Status" INTEGER NOT NULL,
                    "StoredFilePath" TEXT NOT NULL
                );

                CREATE TABLE "DocumentChunks" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_DocumentChunks" PRIMARY KEY,
                    "DocumentId" TEXT NOT NULL,
                    "Order" INTEGER NOT NULL,
                    "Text" TEXT NOT NULL,
                    CONSTRAINT "FK_DocumentChunks_Documents_DocumentId"
                        FOREIGN KEY ("DocumentId")
                        REFERENCES "Documents" ("Id")
                        ON DELETE CASCADE
                );

                CREATE INDEX "IX_DocumentChunks_DocumentId"
                    ON "DocumentChunks" ("DocumentId");

                CREATE UNIQUE INDEX "IX_DocumentChunks_DocumentId_Order"
                    ON "DocumentChunks" ("DocumentId", "Order");

                CREATE INDEX "IX_Documents_ImportedAt"
                    ON "Documents" ("ImportedAt");

                CREATE UNIQUE INDEX "IX_Documents_Sha256Hash"
                    ON "Documents" ("Sha256Hash");

                INSERT INTO "__EFMigrationsHistory" (
                    "MigrationId",
                    "ProductVersion")
                VALUES (
                    '20260821094306_InitialCreate',
                    '10.0.11');

                INSERT INTO "Documents" (
                    "Id",
                    "FileName",
                    "DisplayName",
                    "Sha256Hash",
                    "ImportedAt",
                    "Status",
                    "StoredFilePath")
                VALUES (
                    '{documentIdValue}',
                    'migration-test.txt',
                    'Plaintext Migration Test',
                    'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa',
                    '{importedAtValue}',
                    {(int)DocumentStatus.Available},
                    '{storedFilePath.Replace("'", "''")}');

                INSERT INTO "DocumentChunks" (
                    "Id",
                    "DocumentId",
                    "Order",
                    "Text")
                VALUES (
                    '{firstChunkIdValue}',
                    '{documentIdValue}',
                    0,
                    'The plaintext migration test contains searchable content.');

                INSERT INTO "DocumentChunks" (
                    "Id",
                    "DocumentId",
                    "Order",
                    "Text")
                VALUES (
                    '{secondChunkIdValue}',
                    '{documentIdValue}',
                    1,
                    'This second chunk verifies that chunk ordering and content survive migration.');
                """;

            int createResult =
                raw.sqlite3_exec(
                    database,
                    sql);

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

    private static void AssertPlaintextDocumentExists(
        string databasePath,
        Guid documentId)
    {
        sqlite3? database = null;
        sqlite3_stmt? statement = null;

        try
        {
            int openResult =
                raw.sqlite3_open(
                    databasePath,
                    out database);

            Assert.Equal(
                raw.SQLITE_OK,
                openResult);

            int prepareResult =
                raw.sqlite3_prepare_v2(
                    database,
                    "SELECT \"Id\" FROM \"Documents\";",
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

            string storedId =
                raw.sqlite3_column_text(
                    statement,
                    0)
                    .utf8_to_string();

            Assert.Equal(
                documentId.ToString().ToUpperInvariant(),
                storedId);
        }
        finally
        {
            if (statement is not null)
            {
                raw.sqlite3_finalize(
                    statement);
            }

            if (database is not null)
            {
                raw.sqlite3_close(
                    database);
            }
        }
    }

    private static void AssertEncryptedDocumentExists(
        string databasePath,
        Guid documentId,
        byte[] databaseKey)
    {
        sqlite3? database = null;
        sqlite3_stmt? statement = null;

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

            int prepareResult =
                raw.sqlite3_prepare_v2(
                    database,
                    "SELECT \"Id\" FROM \"Documents\";",
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

            string storedId =
                raw.sqlite3_column_text(
                    statement,
                    0)
                    .utf8_to_string();

            Assert.Equal(
                documentId.ToString().ToUpperInvariant(),
                storedId);
        }
        finally
        {
            if (statement is not null)
            {
                raw.sqlite3_finalize(
                    statement);
            }

            if (database is not null)
            {
                raw.sqlite3_close(
                    database);
            }
        }
    }

    private static ServiceProvider BuildServiceProvider(
        string rootDirectory,
        byte[] databaseKey)
    {
        var services =
            new ServiceCollection();

        services.AddLogging();

        IConfiguration configuration =
            new ConfigurationBuilder()
                .Build();

        services.AddSingleton(
            new DeskVaultDataPaths(
                rootDirectory));

        services.AddApplication();

        services.AddInfrastructure(
            configuration);

        services.AddSingleton<IDatabaseEncryptionKeyService>(
            new TestDatabaseEncryptionKeyService(
                databaseKey));

        return services.BuildServiceProvider();
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

    private static string CreateTemporaryDirectory()
    {
        string directory =
            Path.Combine(
                Path.GetTempPath(),
                "DeskVaultPlaintextMigrationTests",
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

    private sealed class TestDatabaseEncryptionKeyService
        : IDatabaseEncryptionKeyService
    {
        private readonly byte[] _key;

        public TestDatabaseEncryptionKeyService(
            byte[] key)
        {
            _key =
                key.ToArray();
        }

        public Task<byte[]> GetOrCreateKeyAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                _key.ToArray());
        }
    }
}
