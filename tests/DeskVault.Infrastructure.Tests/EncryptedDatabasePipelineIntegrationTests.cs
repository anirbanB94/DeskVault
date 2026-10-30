using System.Security.Cryptography;
using System.Text;
using DeskVault.Application;
using DeskVault.Application.Configurations;
using DeskVault.Application.Documents.Commands.ImportDocument;
using DeskVault.Application.Documents.Queries.SearchDocuments;
using DeskVault.Application.Interfaces;
using DeskVault.Domain.Documents;
using DeskVault.Infrastructure.Persistence;
using DeskVault.Infrastructure.Persistence.Context;
using DeskVault.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DeskVault.Infrastructure.Tests;

public sealed class EncryptedDatabasePipelineIntegrationTests
{
    [Fact]
    public async Task EncryptedDatabase_WhenUsingProductionInfrastructurePath_SupportsDocumentImportProcessingAndSearch()
    {
        string rootDirectory =
            CreateTemporaryDirectory();

        byte[] databaseKey =
            RandomNumberGenerator.GetBytes(32);

        try
        {
            string sourceFilePath =
                Path.Combine(
                    rootDirectory,
                    "encrypted-integration-test.txt");

            string sourceText =
                """
                DeskVault encrypted database integration testing.

                This document contains searchable enterprise architecture content.
                """;

            await File.WriteAllTextAsync(sourceFilePath, sourceText, Encoding.UTF8, TestContext.Current.CancellationToken);

            Guid documentId;

            ServiceProvider serviceProvider =
                BuildServiceProvider(
                    rootDirectory,
                    databaseKey);

            await using (serviceProvider)
            {
                var initializer =
                    serviceProvider.GetRequiredService<DatabaseInitializer>();

                await initializer.InitializeAsync(TestContext.Current.CancellationToken);

                var importHandler =
                    serviceProvider.GetRequiredService<ImportDocumentHandler>();

                var processingService =
                    serviceProvider.GetRequiredService<IDocumentProcessingService>();

                var searchHandler =
                    serviceProvider.GetRequiredService<SearchDocumentsHandler>();

                ImportDocumentResult importResult =
                    await importHandler.HandleAsync(new ImportDocumentCommand(
                            sourceFilePath,
                            "Encrypted Database Integration Test"), TestContext.Current.CancellationToken);

                Assert.Equal(
                    ImportDocumentResultStatus.Success,
                    importResult.Status);

                Assert.NotNull(
                    importResult.DocumentId);

                documentId =
                    importResult.DocumentId.Value;

                var repository =
                    serviceProvider.GetRequiredService<IDocumentRepository>();

                Document? importedDocument =
                    await repository.GetByIdAsync(documentId, TestContext.Current.CancellationToken);

                Assert.NotNull(
                    importedDocument);

                Assert.Equal(
                    documentId,
                    importedDocument.Id);

                Assert.Equal(
                    "encrypted-integration-test.txt",
                    importedDocument.FileName);

                Assert.Equal(
                    "Encrypted Database Integration Test",
                    importedDocument.DisplayName);

                Assert.Equal(
                    DocumentStatus.Imported,
                    importedDocument.Status);

                Assert.True(
                    File.Exists(
                        importedDocument.StoredFilePath));

                Assert.EndsWith(
                    ".dvault",
                    importedDocument.StoredFilePath,
                    StringComparison.OrdinalIgnoreCase);

                await processingService.ProcessAsync(documentId, TestContext.Current.CancellationToken);

                Document? processedDocument =
                    await repository.GetByIdAsync(documentId, TestContext.Current.CancellationToken);

                Assert.NotNull(
                    processedDocument);

                Assert.Equal(
                    DocumentStatus.Available,
                    processedDocument.Status);

                SearchDocumentsPage searchPage =
                    await searchHandler.HandleAsync(new SearchDocumentsQuery(
                            "ENTERPRISE ARCHITECTURE"), TestContext.Current.CancellationToken);

                IReadOnlyList<SearchDocumentsResult> searchResults =
                    searchPage.Results;

                SearchDocumentsResult matchingResult =
                    Assert.Single(
                        searchResults,
                        result =>
                            result.DocumentId == documentId);

                Assert.Equal(
                    "encrypted-integration-test.txt",
                    matchingResult.FileName);

                Assert.Equal(
                    "Encrypted Database Integration Test",
                    matchingResult.DisplayName);

                Assert.Contains(
                    matchingResult.Matches,
                    match =>
                        match.Source == SearchMatchSource.ProcessedContent &&
                        match.Context.Contains(
                            "enterprise architecture",
                            StringComparison.OrdinalIgnoreCase));
            }

            byte[] databaseHeader =
                await ReadDatabaseHeaderAsync(
                    Path.Combine(
                        rootDirectory,
                        "DeskVault.db"));

            Assert.NotEqual(
                "SQLite format 3",
                Encoding.ASCII.GetString(
                    databaseHeader,
                    0,
                    Math.Min(
                        15,
                        databaseHeader.Length)));

            ServiceProvider secondServiceProvider =
                BuildServiceProvider(
                    rootDirectory,
                    databaseKey);

            await using (secondServiceProvider)
            {
                var repository =
                    secondServiceProvider.GetRequiredService<IDocumentRepository>();

                var searchHandler =
                    secondServiceProvider.GetRequiredService<SearchDocumentsHandler>();

                Document? restoredDocument =
                    await repository.GetByIdAsync(documentId, TestContext.Current.CancellationToken);

                Assert.NotNull(
                    restoredDocument);

                Assert.Equal(
                    documentId,
                    restoredDocument.Id);

                Assert.Equal(
                    "encrypted-integration-test.txt",
                    restoredDocument.FileName);

                Assert.Equal(
                    "Encrypted Database Integration Test",
                    restoredDocument.DisplayName);

                Assert.Equal(
                    DocumentStatus.Available,
                    restoredDocument.Status);

                Assert.True(
                    File.Exists(
                        restoredDocument.StoredFilePath));

                SearchDocumentsPage restoredSearchPage =
                    await searchHandler.HandleAsync(new SearchDocumentsQuery(
                            "encrypted database"), TestContext.Current.CancellationToken);

                IReadOnlyList<SearchDocumentsResult> restoredSearchResults =
                    restoredSearchPage.Results;

                SearchDocumentsResult restoredResult =
                    Assert.Single(
                        restoredSearchResults,
                        result =>
                            result.DocumentId == documentId);

                Assert.Equal(
                    "Encrypted Database Integration Test",
                    restoredResult.DisplayName);

                Assert.Equal(
                    2,
                    restoredResult.MatchCount);

                SearchMatch retainedMatch =
                    Assert.Single(restoredResult.Matches);

                Assert.Equal(
                    SearchMatchSource.DocumentMetadata,
                    retainedMatch.Source);

                Assert.Equal(
                    SearchMatchKind.Exact,
                    retainedMatch.Kind);

                Assert.Equal(
                    "Encrypted Database Integration Test",
                    retainedMatch.Context);

                Assert.NotNull(
                    restoredResult.RelevanceScore);

                Assert.Equal(
                    160,
                    restoredResult.RelevanceScore.Value);
            }
        }
        finally
        {
            DeleteTemporaryDirectory(
                rootDirectory);
        }
    }

    [Fact]
    public async Task EncryptedDatabase_WhenRuntimeArtifactsExist_DoNotExposePlaintextContent()
    {
        string rootDirectory =
            CreateTemporaryDirectory();

        byte[] databaseKey =
            RandomNumberGenerator.GetBytes(32);

        string plaintextMarker =
            $"DeskVault-SIDE-CAR-PLAINTEXT-MARKER-{Guid.NewGuid():N}";

        try
        {
            ServiceProvider serviceProvider =
                BuildServiceProvider(
                    rootDirectory,
                    databaseKey);

            await using (serviceProvider)
            {
                var initializer =
                    serviceProvider.GetRequiredService<DatabaseInitializer>();

                await initializer.InitializeAsync(TestContext.Current.CancellationToken);

                var repository =
                    serviceProvider.GetRequiredService<IDocumentRepository>();

                Document document =
                    Document.Create(
                        Guid.NewGuid(),
                        "sidecar-test.txt",
                        plaintextMarker,
                        "sidecar-test-hash",
                        Path.Combine(
                            rootDirectory,
                            "Documents",
                            "sidecar-test.dvault"));

                await repository.AddAsync(document, TestContext.Current.CancellationToken);

                await AssertNoPlaintextMarkerInRuntimeArtifactsAsync(
                    rootDirectory,
                    plaintextMarker);
            }

            await AssertNoPlaintextMarkerInRuntimeArtifactsAsync(
                rootDirectory,
                plaintextMarker);

            byte[] databaseHeader =
                await ReadDatabaseHeaderAsync(
                    Path.Combine(
                        rootDirectory,
                        "DeskVault.db"));

            Assert.NotEqual(
                "SQLite format 3",
                Encoding.ASCII.GetString(
                    databaseHeader,
                    0,
                    Math.Min(
                        15,
                        databaseHeader.Length)));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                databaseKey);

            DeleteTemporaryDirectory(
                rootDirectory);
        }
    }

    [Fact]
    public async Task EncryptedDatabase_WhenWalArtifactRemainsAfterConnectionClose_DoesNotExposePersistedPlaintextMarker()
    {
        // Arrange
        Pbi234RuntimeTestRoot testRoot =
            CreatePbi234RuntimeTestRoot("Sidecar");

        string rootDirectory =
            testRoot.RootDirectory;

        byte[] databaseKey =
            RandomNumberGenerator.GetBytes(32);

        string plaintextMarker =
            $"DeskVault-SIDE-CAR-PLAINTEXT-MARKER-{Guid.NewGuid():N}";

        string databasePath =
            Path.Combine(rootDirectory, "DeskVault.db");

        string walPath =
            databasePath + "-wal";

        string shmPath =
            databasePath + "-shm";

        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        string journalMode = string.Empty;
        string? persistedMarker = null;
        int checkpointConfigurationResult = -1;
        int checkpointDisabled = 0;
        bool walExistsAfterConnectionClose = false;

        try
        {
            await using (ServiceProvider serviceProvider =
                BuildServiceProvider(
                    rootDirectory,
                    databaseKey))
            {
                var initializer =
                    serviceProvider.GetRequiredService<DatabaseInitializer>();

                await initializer.InitializeAsync(cancellationToken);

                AssertPbi234RuntimeTestRootAcl(rootDirectory);

                var dbContextFactory =
                    serviceProvider.GetRequiredService<IDbContextFactory<DeskVaultDbContext>>();

                await using (DeskVaultDbContext dbContext =
                    await dbContextFactory.CreateDbContextAsync(cancellationToken))
                {
                    await dbContext.Database.OpenConnectionAsync(cancellationToken);

                    try
                    {
                        await using (var journalModeCommand =
                            dbContext.Database.GetDbConnection().CreateCommand())
                        {
                            journalModeCommand.CommandText =
                                "PRAGMA journal_mode;";

                            object? result =
                                await journalModeCommand.ExecuteScalarAsync(cancellationToken);

                            journalMode =
                                Convert.ToString(result) ?? string.Empty;
                        }

                        var sqliteConnection =
                            (Microsoft.Data.Sqlite.SqliteConnection)
                                dbContext.Database.GetDbConnection();

                        checkpointConfigurationResult =
                            SQLitePCL.raw.sqlite3_db_config(
                                sqliteConnection.Handle,
                                SQLitePCL.raw.SQLITE_DBCONFIG_NO_CKPT_ON_CLOSE,
                                1,
                                out checkpointDisabled);

                        await dbContext.Database.ExecuteSqlRawAsync(
                            "CREATE TABLE Pbi234SidecarProbe (Id INTEGER PRIMARY KEY, Marker TEXT NOT NULL);",
                            cancellationToken);

                        await dbContext.Database.ExecuteSqlInterpolatedAsync(
                            $"INSERT INTO Pbi234SidecarProbe (Marker) VALUES ({plaintextMarker});",
                            cancellationToken);

                        await using (var markerCommand =
                            dbContext.Database.GetDbConnection().CreateCommand())
                        {
                            markerCommand.CommandText =
                                "SELECT Marker FROM Pbi234SidecarProbe WHERE Id = 1;";

                            persistedMarker =
                                Convert.ToString(
                                    await markerCommand.ExecuteScalarAsync(cancellationToken));
                        }

                        // Observe the actual provider-created sidecars while
                        // the WAL connection is still open. On the current
                        // SQLite3MC Windows VFS, WAL and SHM are expected here.
                        Assert.True(
                            File.Exists(walPath),
                            $"Expected WAL '{walPath}' while the SQLite connection is open.");

                        Assert.True(
                            File.Exists(shmPath),
                            $"Expected SHM '{shmPath}' while WAL mode is active.");

                        var walRulesWhileOpen =
                            AuxiliaryArtifactAclAssertions.CaptureAccessRules(
                                walPath,
                                isDirectory: false);

                        AuxiliaryArtifactAclAssertions.AssertProtectedArtifactAcl(
                            walRulesWhileOpen,
                            "live SQLite WAL sidecar",
                            walPath);

                        var shmRulesWhileOpen =
                            AuxiliaryArtifactAclAssertions.CaptureAccessRules(
                                shmPath,
                                isDirectory: false);

                        AuxiliaryArtifactAclAssertions.AssertProtectedArtifactAcl(
                            shmRulesWhileOpen,
                            "live SQLite SHM sidecar",
                            shmPath);
                    }
                    finally
                    {
                        await dbContext.Database.CloseConnectionAsync();
                    }
                }
            }

            // Act: all SQLite connections are now disposed, so the retained
            // WAL can be inspected through the filesystem.
            walExistsAfterConnectionClose =
                File.Exists(walPath);

            // Assert
            Assert.Equal(
                "wal",
                journalMode.ToLowerInvariant());

            Assert.Equal(
                SQLitePCL.raw.SQLITE_OK,
                checkpointConfigurationResult);

            Assert.Equal(
                1,
                checkpointDisabled);

            Assert.Equal(
                plaintextMarker,
                persistedMarker);

            Assert.True(
                walExistsAfterConnectionClose,
                $"Expected '{walPath}' to remain after closing the SQLite connection with checkpoint-on-close disabled.");

            var walRules =
                AuxiliaryArtifactAclAssertions.CaptureAccessRules(
                    walPath,
                    isDirectory: false);

            AuxiliaryArtifactAclAssertions
                .AssertProtectedArtifactAcl(
                    walRules,
                    "actual SQLite WAL sidecar",
                    walPath);

            await AssertNoPlaintextMarkerInRuntimeArtifactsAsync(
                rootDirectory,
                plaintextMarker,
                walPath);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                databaseKey);

            DeletePbi234RuntimeTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task EncryptedDatabase_WhenPersistentRollbackJournalRemainsAfterCommit_DoesNotExposePersistedPlaintextMarker()
    {
        // Arrange
        SqliteProviderBootstrap.EnsureInitialized();

        Pbi234RuntimeTestRoot testRoot =
            CreatePbi234RuntimeTestRoot("RollbackJournal");

        string rootDirectory =
            testRoot.RootDirectory;

        byte[] databaseKey =
            RandomNumberGenerator.GetBytes(32);

        string databasePath =
            Path.Combine(rootDirectory, "DeskVault.db");

        string journalPath =
            databasePath + "-journal";

        string plaintextMarker =
            $"DeskVault-ROLLBACK-JOURNAL-PLAINTEXT-MARKER-{Guid.NewGuid():N}";

        string replacementMarker =
            $"DeskVault-ROLLBACK-JOURNAL-REPLACEMENT-{Guid.NewGuid():N}";

        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        string journalMode = string.Empty;
        string? markerAfterUpdate = null;
        bool journalExistsAfterConnectionClose = false;
        long journalLengthAfterConnectionClose = 0;

        try
        {
            await using (ServiceProvider serviceProvider =
                BuildServiceProvider(
                    rootDirectory,
                    databaseKey))
            {
                var initializer =
                    serviceProvider.GetRequiredService<DatabaseInitializer>();

                await initializer.InitializeAsync(cancellationToken);

                AssertPbi234RuntimeTestRootAcl(rootDirectory);

                var dbContextFactory =
                    serviceProvider.GetRequiredService<IDbContextFactory<DeskVaultDbContext>>();

                await using (DeskVaultDbContext dbContext =
                    await dbContextFactory.CreateDbContextAsync(cancellationToken))
                {
                    await dbContext.Database.OpenConnectionAsync(cancellationToken);

                    try
                    {
                        var connection =
                            dbContext.Database.GetDbConnection();

                        // PERSIST deliberately retains the rollback journal after commit.
                        await using (var command = connection.CreateCommand())
                        {
                            command.CommandText =
                                "PRAGMA journal_mode=PERSIST;";

                            journalMode =
                                Convert.ToString(
                                    await command.ExecuteScalarAsync(cancellationToken))
                                ?? string.Empty;
                        }

                        Assert.Equal(
                            "persist",
                            journalMode.ToLowerInvariant());

                        await dbContext.Database.ExecuteSqlRawAsync(
                            """
                            CREATE TABLE Pbi234RollbackJournalProbe
                            (
                                Id INTEGER PRIMARY KEY,
                                Marker TEXT NOT NULL
                            );
                            """,
                            cancellationToken);

                        // Commit a known pre-update page image containing the marker.
                        await dbContext.Database.ExecuteSqlInterpolatedAsync(
                            $"INSERT INTO Pbi234RollbackJournalProbe (Id, Marker) VALUES (1, {plaintextMarker});",
                            cancellationToken);

                        // A separate write transaction should journal the old page
                        // before replacing the committed plaintext-marker value.
                        await using (var transaction =
                            await dbContext.Database.BeginTransactionAsync(
                                cancellationToken))
                        {
                            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                                $"UPDATE Pbi234RollbackJournalProbe SET Marker = {replacementMarker} WHERE Id = 1;",
                                cancellationToken);

                            await transaction.CommitAsync(cancellationToken);
                        }

                        await using (var command = connection.CreateCommand())
                        {
                            command.CommandText =
                                "SELECT Marker FROM Pbi234RollbackJournalProbe WHERE Id = 1;";

                            markerAfterUpdate =
                                Convert.ToString(
                                    await command.ExecuteScalarAsync(cancellationToken));
                        }
                    }
                    finally
                    {
                        await dbContext.Database.CloseConnectionAsync();
                    }
                }
            }

            // Inspect only after all application SQLite handles are disposed.
            journalExistsAfterConnectionClose =
                File.Exists(journalPath);

            if (journalExistsAfterConnectionClose)
            {
                journalLengthAfterConnectionClose =
                    new FileInfo(journalPath).Length;
            }

            // Assert
            Assert.Equal(
                replacementMarker,
                markerAfterUpdate);

            Assert.True(
                journalExistsAfterConnectionClose,
                $"Expected the PERSIST rollback journal to remain at '{journalPath}' after connection closure.");

            Assert.True(
                journalLengthAfterConnectionClose > 512,
                $"Expected a populated rollback journal; actual length was {journalLengthAfterConnectionClose} bytes.");

            var journalRules =
                AuxiliaryArtifactAclAssertions.CaptureAccessRules(
                    journalPath,
                    isDirectory: false);

            AuxiliaryArtifactAclAssertions
                .AssertProtectedArtifactAcl(
                    journalRules,
                    "actual SQLite rollback journal",
                    journalPath);

            await AssertNoPlaintextMarkerInRuntimeArtifactsAsync(
                rootDirectory,
                plaintextMarker,
                journalPath);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                databaseKey);

            DeletePbi234RuntimeTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task EncryptedDatabase_WhenTemporaryTableAndSortUseConfiguredTempStore_DoNotPersistPlaintextMarker()
    {
        // Arrange
        DeskVault.Infrastructure.Persistence.SqliteProviderBootstrap.EnsureInitialized();

        Pbi234RuntimeTestRoot testRoot =
            CreatePbi234RuntimeTestRoot("TempStore");

        string rootDirectory =
            testRoot.RootDirectory;

        byte[] databaseKey =
            RandomNumberGenerator.GetBytes(32);

        string plaintextMarker =
            $"DeskVault-TEMP-STORE-PLAINTEXT-MARKER-{Guid.NewGuid():N}";

        const string firstSortValue = "AAA-TEMP-STORE-SORT-ROW";
        const string lastSortValue = "ZZZ-TEMP-STORE-SORT-ROW";

        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        int tempStore = -1;
        string[] tempStoreOptions = [];
        string[] observedValues = [];

        try
        {
            await using (ServiceProvider serviceProvider =
                BuildServiceProvider(
                    rootDirectory,
                    databaseKey))
            {
                var initializer =
                    serviceProvider.GetRequiredService<DatabaseInitializer>();

                await initializer.InitializeAsync(cancellationToken);

                AssertPbi234RuntimeTestRootAcl(rootDirectory);

                var dbContextFactory =
                    serviceProvider.GetRequiredService<IDbContextFactory<DeskVaultDbContext>>();

                await using (DeskVaultDbContext dbContext =
                    await dbContextFactory.CreateDbContextAsync(cancellationToken))
                {
                    await dbContext.Database.OpenConnectionAsync(cancellationToken);

                    try
                    {
                        var connection =
                            dbContext.Database.GetDbConnection();

                        // Inspect the actual application connection before creating
                        // temporary objects; do not override the provider's setting.
                        await using (var command = connection.CreateCommand())
                        {
                            command.CommandText = "PRAGMA temp_store;";

                            tempStore = Convert.ToInt32(
                                await command.ExecuteScalarAsync(cancellationToken));
                        }

                        var compileOptions = new List<string>();

                        await using (var command = connection.CreateCommand())
                        {
                            command.CommandText = "PRAGMA compile_options;";

                            await using var reader =
                                await command.ExecuteReaderAsync(cancellationToken);

                            while (await reader.ReadAsync(cancellationToken))
                            {
                                compileOptions.Add(reader.GetString(0));
                            }
                        }

                        tempStoreOptions = compileOptions
                            .Where(option =>
                                option.StartsWith(
                                    "TEMP_STORE=",
                                    StringComparison.OrdinalIgnoreCase))
                            .ToArray();

                        // A TEMP table is connection-scoped. The ORDER BY on Marker,
                        // which is not indexed, exercises the temporary sort path.
                        await dbContext.Database.ExecuteSqlRawAsync(
                            """
                            CREATE TEMP TABLE Pbi234TempStoreProbe
                            (
                                Id INTEGER PRIMARY KEY,
                                Marker TEXT NOT NULL
                            );
                            """,
                            cancellationToken);

                        await dbContext.Database.ExecuteSqlInterpolatedAsync(
                            $"INSERT INTO temp.Pbi234TempStoreProbe (Id, Marker) VALUES (1, {plaintextMarker});",
                            cancellationToken);

                        await dbContext.Database.ExecuteSqlInterpolatedAsync(
                            $"INSERT INTO temp.Pbi234TempStoreProbe (Id, Marker) VALUES (2, {firstSortValue});",
                            cancellationToken);

                        await dbContext.Database.ExecuteSqlInterpolatedAsync(
                            $"INSERT INTO temp.Pbi234TempStoreProbe (Id, Marker) VALUES (3, {lastSortValue});",
                            cancellationToken);

                        var values = new List<string>();

                        await using (var command = connection.CreateCommand())
                        {
                            command.CommandText =
                                """
                                SELECT Marker
                                FROM temp.Pbi234TempStoreProbe
                                ORDER BY Marker;
                                """;

                            await using var reader =
                                await command.ExecuteReaderAsync(cancellationToken);

                            while (await reader.ReadAsync(cancellationToken))
                            {
                                values.Add(reader.GetString(0));
                            }
                        }

                        observedValues = values.ToArray();
                    }
                    finally
                    {
                        await dbContext.Database.CloseConnectionAsync();
                    }
                }
            }

            // Assert the effective provider configuration and query outcome.
            Assert.True(
                tempStore is 0 or 2,
                $"Expected default or memory-backed PRAGMA temp_store; actual value was {tempStore}.");

            Assert.Single(tempStoreOptions);

            Assert.True(
                tempStoreOptions[0] is "TEMP_STORE=2" or "TEMP_STORE=3",
                $"Expected memory-backed SQLite temporary storage; actual compile option was '{tempStoreOptions[0]}'.");

            Assert.Equal(
                new[] { firstSortValue, plaintextMarker, lastSortValue },
                observedValues);

            // Inspect all files under this isolated root only after every SQLite
            // connection and the provider have been disposed.
            byte[] markerBytes =
                Encoding.UTF8.GetBytes(plaintextMarker);

            try
            {
                string[] filePaths =
                    Directory.EnumerateFiles(
                        rootDirectory,
                        "*",
                        SearchOption.AllDirectories)
                    .ToArray();

                foreach (string filePath in filePaths)
                {
                    byte[] contents =
                        await File.ReadAllBytesAsync(
                            filePath,
                            cancellationToken);

                    try
                    {
                        Assert.False(
                            ContainsByteSequence(contents, markerBytes),
                            $"Temporary-table marker was present in persisted file '{filePath}'.");
                    }
                    finally
                    {
                        CryptographicOperations.ZeroMemory(contents);
                    }
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(markerBytes);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                databaseKey);

            DeletePbi234RuntimeTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task EncryptedDatabase_WhenInitialized_ReportsProviderAndTemporaryStoreConfiguration()
    {
        // Arrange
        DeskVault.Infrastructure.Persistence.SqliteProviderBootstrap.EnsureInitialized();

        Pbi234RuntimeTestRoot testRoot =
            CreatePbi234RuntimeTestRoot("Provider");

        string rootDirectory =
            testRoot.RootDirectory;

        byte[] databaseKey =
            RandomNumberGenerator.GetBytes(32);

        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        try
        {
            await using (ServiceProvider serviceProvider =
                BuildServiceProvider(
                    rootDirectory,
                    databaseKey))
            {
                var initializer =
                    serviceProvider.GetRequiredService<DatabaseInitializer>();

                await initializer.InitializeAsync(cancellationToken);

                AssertPbi234RuntimeTestRootAcl(rootDirectory);

                var dbContextFactory =
                    serviceProvider.GetRequiredService<IDbContextFactory<DeskVaultDbContext>>();

                await using (DeskVaultDbContext dbContext =
                    await dbContextFactory.CreateDbContextAsync(cancellationToken))
                {
                    await dbContext.Database.OpenConnectionAsync(cancellationToken);

                    try
                    {
                        var connection =
                            dbContext.Database.GetDbConnection();

                        string sqliteVersion;
                        await using (var command = connection.CreateCommand())
                        {
                            command.CommandText = "SELECT sqlite_version();";

                            sqliteVersion =
                                Convert.ToString(
                                    await command.ExecuteScalarAsync(cancellationToken))
                                ?? string.Empty;
                        }

                        string sqlite3McVersion;
                        await using (var command = connection.CreateCommand())
                        {
                            command.CommandText = "SELECT sqlite3mc_version();";

                            sqlite3McVersion =
                                Convert.ToString(
                                    await command.ExecuteScalarAsync(cancellationToken))
                                ?? string.Empty;
                        }

                        string journalMode;
                        await using (var command = connection.CreateCommand())
                        {
                            command.CommandText = "PRAGMA journal_mode;";

                            journalMode =
                                Convert.ToString(
                                    await command.ExecuteScalarAsync(cancellationToken))
                                ?? string.Empty;
                        }

                        int tempStore;
                        await using (var command = connection.CreateCommand())
                        {
                            command.CommandText = "PRAGMA temp_store;";

                            tempStore = Convert.ToInt32(
                                await command.ExecuteScalarAsync(cancellationToken));
                        }

                        var compileOptions =
                            new List<string>();

                        await using (var command = connection.CreateCommand())
                        {
                            command.CommandText = "PRAGMA compile_options;";

                            await using var reader =
                                await command.ExecuteReaderAsync(cancellationToken);

                            while (await reader.ReadAsync(cancellationToken))
                            {
                                compileOptions.Add(reader.GetString(0));
                            }
                        }

                        string[] tempStoreOptions =
                            compileOptions
                                .Where(option =>
                                    option.StartsWith(
                                        "TEMP_STORE=",
                                        StringComparison.OrdinalIgnoreCase))
                                .ToArray();

                        // Assert that runtime facts match the pinned provider baseline.
                        Assert.True(
                            sqliteVersion.StartsWith(
                                "3.54.0",
                                StringComparison.Ordinal),
                            $"Expected SQLite 3.54.0 for the pinned SQLite3MC package; actual version was '{sqliteVersion}'.");

                        Assert.True(
                            sqlite3McVersion.Contains(
                                "2.6.1",
                                StringComparison.OrdinalIgnoreCase),
                            $"Expected SQLite3MC 2.6.1; actual runtime version was '{sqlite3McVersion}'.");

                        Assert.Equal(
                            "wal",
                            journalMode.ToLowerInvariant());

                        // 0 means DEFAULT. The compile-time assertion below must
                        // establish that DEFAULT resolves to memory-backed storage.
                        Assert.True(
                            tempStore is 0 or 2,
                            $"Unexpected PRAGMA temp_store value: {tempStore}.");

                        Assert.Single(tempStoreOptions);

                        Assert.True(
                            tempStoreOptions[0] is "TEMP_STORE=2" or "TEMP_STORE=3",
                            $"Expected memory-backed temporary storage; actual compile option was '{tempStoreOptions[0]}'.");
                    }
                    finally
                    {
                        await dbContext.Database.CloseConnectionAsync();
                    }
                }
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                databaseKey);

            DeletePbi234RuntimeTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task EncryptedDatabase_WhenOpenedWithWrongKey_FailsWithoutExposingKey()
    {
        string rootDirectory =
            CreateTemporaryDirectory();

        byte[] correctDatabaseKey =
            RandomNumberGenerator.GetBytes(32);

        byte[] wrongDatabaseKey =
            RandomNumberGenerator.GetBytes(32);

        try
        {
            ServiceProvider serviceProvider =
                BuildServiceProvider(
                    rootDirectory,
                    correctDatabaseKey);

            await using (serviceProvider)
            {
                var initializer =
                    serviceProvider.GetRequiredService<DatabaseInitializer>();

                await initializer.InitializeAsync(TestContext.Current.CancellationToken);
            }

            ServiceProvider wrongKeyServiceProvider =
                BuildServiceProvider(
                    rootDirectory,
                    wrongDatabaseKey);

            await using (wrongKeyServiceProvider)
            {
                var initializer =
                    wrongKeyServiceProvider.GetRequiredService<DatabaseInitializer>();

                Exception exception =
                    await Assert.ThrowsAnyAsync<Exception>(
                        () =>
                            initializer.InitializeAsync(TestContext.Current.CancellationToken));

                string exceptionText =
                    exception.ToString();

                string correctKeyText =
                    Convert.ToBase64String(
                        correctDatabaseKey);

                string wrongKeyText =
                    Convert.ToBase64String(
                        wrongDatabaseKey);

                Assert.DoesNotContain(
                    correctKeyText,
                    exceptionText,
                    StringComparison.Ordinal);

                Assert.DoesNotContain(
                    wrongKeyText,
                    exceptionText,
                    StringComparison.Ordinal);
            }
        }
        finally
        {
            DeleteTemporaryDirectory(
                rootDirectory);
        }
    }

    [Fact]
    public async Task EncryptedDatabase_WhenDatabaseKeyFileIsMissing_FailsWithoutExposingKey()
    {
        string rootDirectory =
            CreateTemporaryDirectory();

        try
        {
            ServiceProvider firstServiceProvider =
                BuildProductionServiceProvider(
                    rootDirectory);

            await using (firstServiceProvider)
            {
                var initializer =
                    firstServiceProvider.GetRequiredService<DatabaseInitializer>();

                await initializer.InitializeAsync(TestContext.Current.CancellationToken);
            }

            string databaseKeyFilePath =
                Path.Combine(
                    rootDirectory,
                    "Security",
                    "database.key");

            Assert.True(
                File.Exists(
                    databaseKeyFilePath));

            byte[] originalProtectedKey =
                await File.ReadAllBytesAsync(databaseKeyFilePath, TestContext.Current.CancellationToken);

            Assert.NotEmpty(
                originalProtectedKey);

            File.Delete(
                databaseKeyFilePath);

            Assert.False(
                File.Exists(
                    databaseKeyFilePath));

            ServiceProvider secondServiceProvider =
                BuildProductionServiceProvider(
                    rootDirectory);

            await using (secondServiceProvider)
            {
                var initializer =
                    secondServiceProvider.GetRequiredService<DatabaseInitializer>();

                Exception exception =
                    await Assert.ThrowsAnyAsync<Exception>(
                        () =>
                            initializer.InitializeAsync(TestContext.Current.CancellationToken));

                string exceptionText =
                    exception.ToString();

                Assert.DoesNotContain(
                    Convert.ToBase64String(
                        originalProtectedKey),
                    exceptionText,
                    StringComparison.Ordinal);

                Assert.False(
                    File.Exists(
                        databaseKeyFilePath));
            }
        }
        finally
        {
            DeleteTemporaryDirectory(
                rootDirectory);
        }
    }

    [Fact]
    public async Task EncryptedDatabase_WhenSearchingUnicodeCaseAndCanonicalEquivalence_ReturnsMetadataAndProcessedContentMatches()
    {
        // Arrange
        string rootDirectory =
            CreateTemporaryDirectory();

        byte[] databaseKey =
            RandomNumberGenerator.GetBytes(32);

        try
        {
            string sourceFilePath =
                Path.Combine(
                    rootDirectory,
                    "café-unicode-integration.txt");

            const string sourceText =
                "The cafe\u0301 receipt contains searchable Unicode content.";

            await File.WriteAllTextAsync(sourceFilePath, sourceText, Encoding.UTF8, TestContext.Current.CancellationToken);

            await using ServiceProvider serviceProvider =
                BuildServiceProvider(
                    rootDirectory,
                    databaseKey);

            var initializer =
                serviceProvider.GetRequiredService<DatabaseInitializer>();

            await initializer.InitializeAsync(TestContext.Current.CancellationToken);

            var importHandler =
                serviceProvider.GetRequiredService<ImportDocumentHandler>();

            var processingService =
                serviceProvider.GetRequiredService<IDocumentProcessingService>();

            var searchHandler =
                serviceProvider.GetRequiredService<SearchDocumentsHandler>();

            ImportDocumentResult importResult =
                await importHandler.HandleAsync(new ImportDocumentCommand(
                        sourceFilePath,
                        "CAFÉ Unicode Receipt"), TestContext.Current.CancellationToken);

            Assert.Equal(
                ImportDocumentResultStatus.Success,
                importResult.Status);

            Assert.NotNull(
                importResult.DocumentId);

            Guid documentId =
                importResult.DocumentId.Value;

            await processingService.ProcessAsync(documentId, TestContext.Current.CancellationToken);

            string[] searchTexts =
            [
                "café",
            "cafe\u0301",
            "CAFÉ",
            "CAFE\u0301"
            ];

            // Act
            var searchPages =
                new List<SearchDocumentsPage>();

            foreach (string searchText in searchTexts)
            {
                SearchDocumentsPage page =
                    await searchHandler.HandleAsync(new SearchDocumentsQuery(
                            searchText), TestContext.Current.CancellationToken);

                searchPages.Add(
                    page);
            }

            // Assert
            Assert.Equal(
                searchTexts.Length,
                searchPages.Count);

            foreach (SearchDocumentsPage page in searchPages)
            {
                SearchDocumentsResult result =
                    Assert.Single(
                        page.Results,
                        item => item.DocumentId == documentId);

                Assert.Equal(
                    "café-unicode-integration.txt",
                    result.FileName);

                Assert.Equal(
                    "CAFÉ Unicode Receipt",
                    result.DisplayName);

                Assert.Equal(
                    3,
                    result.MatchCount);

                SearchMatch retainedMatch =
                    Assert.Single(result.Matches);

                Assert.Equal(
                    SearchMatchSource.DocumentMetadata,
                    retainedMatch.Source);

                Assert.Equal(
                    SearchMatchKind.Exact,
                    retainedMatch.Kind);

                Assert.Equal(
                    "café-unicode-integration.txt",
                    retainedMatch.Context);

                Assert.NotNull(
                    result.RelevanceScore);

                Assert.Equal(
                    260,
                    result.RelevanceScore.Value);
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

        services.AddSingleton(
            new DocumentProcessingOptions());

        services.AddApplication();

        services.AddInfrastructure(
            configuration);

        var testDatabaseEncryptionKeyService =
            new TestDatabaseEncryptionKeyService(
                databaseKey);

        services.AddSingleton<IDatabaseEncryptionKeyService>(
            testDatabaseEncryptionKeyService);

        services.AddSingleton<IDatabaseEncryptionKeyProvisioningService>(
            testDatabaseEncryptionKeyService);

        return services.BuildServiceProvider();
    }

    private static ServiceProvider BuildProductionServiceProvider(
        string rootDirectory)
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

        services.AddSingleton(
            new DocumentProcessingOptions());

        services.AddApplication();

        services.AddInfrastructure(
            configuration);

        return services.BuildServiceProvider();
    }

    private static async Task<byte[]> ReadDatabaseHeaderAsync(
        string databasePath)
    {
        await using FileStream stream =
            new FileStream(
                databasePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);

        byte[] header =
            new byte[32];

        int bytesRead =
            await stream.ReadAsync(
                header);

        return header[..bytesRead];
    }

    private static async Task AssertNoPlaintextMarkerInRuntimeArtifactsAsync(
        string rootDirectory,
        string plaintextMarker,
        string? expectedArtifactPath = null)
    {
        byte[] markerBytes =
            Encoding.UTF8.GetBytes(
                plaintextMarker);

        string[] artifactPaths =
            Directory
                .EnumerateFiles(
                    rootDirectory,
                    "*",
                    SearchOption.AllDirectories)
                .Where(
                    IsPotentialSqliteRuntimeArtifact)
                .ToArray();

        if (expectedArtifactPath is not null)
        {
            // The WAL-specific test must prove that its sidecar was observed.
            Assert.Contains(
                artifactPaths,
                artifactPath =>
                    string.Equals(
                        artifactPath,
                        expectedArtifactPath,
                        StringComparison.OrdinalIgnoreCase));
        }

        foreach (string artifactPath in artifactPaths)
        {
            byte[] artifactContents =
                await File.ReadAllBytesAsync(
                    artifactPath,
                    TestContext.Current.CancellationToken);

            Assert.False(
                ContainsByteSequence(
                    artifactContents,
                    markerBytes),
                $"Plaintext marker was found in SQLite runtime artifact '{artifactPath}'.");
        }
    }

    private static bool ContainsByteSequence(
        byte[] source,
        byte[] sequence)
    {
        if (sequence.Length == 0)
        {
            return true;
        }

        if (source.Length < sequence.Length)
        {
            return false;
        }

        for (int sourceIndex = 0;
             sourceIndex <= source.Length - sequence.Length;
             sourceIndex++)
        {
            bool matches =
                true;

            for (int sequenceIndex = 0;
                 sequenceIndex < sequence.Length;
                 sequenceIndex++)
            {
                if (source[sourceIndex + sequenceIndex] !=
                    sequence[sequenceIndex])
                {
                    matches =
                        false;

                    break;
                }
            }

            if (matches)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPotentialSqliteRuntimeArtifact(
        string filePath)
    {
        string fileName =
            Path.GetFileName(
                filePath);

        return fileName.EndsWith(
                   "-wal",
                   StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(
                   "-shm",
                   StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(
                   "-journal",
                   StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(
                   ".tmp",
                   StringComparison.OrdinalIgnoreCase);
    }

    private sealed record Pbi234RuntimeTestRoot(
        string ApplicationRootDirectory,
        string RootDirectory,
        bool ApplicationRootExisted);

    private static Pbi234RuntimeTestRoot CreatePbi234RuntimeTestRoot(
        string scenario)
    {
        string localApplicationData =
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);

        Assert.False(
            string.IsNullOrWhiteSpace(localApplicationData),
            "LocalApplicationData must resolve to a valid directory.");

        string applicationRootDirectory =
            new DeskVaultDataPaths().RootDirectory;

        Assert.Equal(
            Path.GetFullPath(
                Path.Combine(localApplicationData, "DeskVault")),
            Path.GetFullPath(applicationRootDirectory));

        return new Pbi234RuntimeTestRoot(
            applicationRootDirectory,
            Path.Combine(
                applicationRootDirectory,
                $"PBI234-{scenario}-{Guid.NewGuid():N}"),
            Directory.Exists(applicationRootDirectory));
    }

    private static void AssertPbi234RuntimeTestRootAcl(
        string rootDirectory)
    {
        var rootRules =
            AuxiliaryArtifactAclAssertions.CaptureAccessRules(
                rootDirectory,
                isDirectory: true);

        AuxiliaryArtifactAclAssertions.AssertProtectedArtifactAcl(
            rootRules,
            "actual application-root child used for runtime artifacts",
            rootDirectory);
    }

    private static void DeletePbi234RuntimeTestRoot(
        Pbi234RuntimeTestRoot testRoot)
    {
        DeleteTemporaryDirectory(testRoot.RootDirectory);

        // Remove the application root only when this test had to create it
        // and it remains empty after deleting its own uniquely named child.
        if (!testRoot.ApplicationRootExisted &&
            Directory.Exists(testRoot.ApplicationRootDirectory) &&
            !Directory.EnumerateFileSystemEntries(
                testRoot.ApplicationRootDirectory).Any())
        {
            Directory.Delete(testRoot.ApplicationRootDirectory);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        string directory =
            Path.Combine(
                Path.GetTempPath(),
                "DeskVaultEncryptedDatabaseTests",
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

    private sealed class TestDatabaseEncryptionKeyService :
        IDatabaseEncryptionKeyService,
        IDatabaseEncryptionKeyProvisioningService
    {
        private readonly byte[] _key;
        private readonly Guid _keyId = Guid.NewGuid();

        public TestDatabaseEncryptionKeyService(
            byte[] key)
        {
            _key = key.ToArray();
        }

        public Task<EncryptionKeyMaterial> GetOrCreateKeyAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                CreateMaterial());
        }

        public Task<EncryptionKeyMaterial> GetKeyAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                CreateMaterial());
        }

        private EncryptionKeyMaterial CreateMaterial()
        {
            return new EncryptionKeyMaterial(
                _key.ToArray(),
                _keyId,
                keyVersion: 1);
        }
    }
}
