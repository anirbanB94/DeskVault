using System.Security.Cryptography;
using System.Text;
using DeskVault.Application;
using DeskVault.Application.Documents.Queries.SearchDocuments;
using DeskVault.Application.Documents.Chunking;
using DeskVault.Application.Interfaces;
using DeskVault.Domain.Documents;
using DeskVault.Infrastructure.Persistence;
using DeskVault.Infrastructure.Persistence.Context;
using DeskVault.Infrastructure.Persistence.Entities;
using DeskVault.Infrastructure.Services;
using Microsoft.Data.Sqlite;
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

        const string firstChunkText =
            "The plaintext migration test contains searchable content.";

        const string secondChunkText =
            "This second chunk verifies that chunk ordering and content survive migration.";

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

            Assert.False(
                File.Exists(
                    databasePath + ".migration"));

            Assert.False(
                File.Exists(
                    databasePath + ".migration-backup"));

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

                var dbContextFactory =
                    verificationServiceProvider.GetRequiredService<
                        IDbContextFactory<DeskVaultDbContext>>();

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

                await using DeskVaultDbContext dbContext =
                    await dbContextFactory.CreateDbContextAsync();

                List<DocumentChunkEntity> chunks =
                    await dbContext.DocumentChunks
                        .AsNoTracking()
                        .Where(
                            chunk =>
                                chunk.DocumentId == documentId)
                        .OrderBy(
                            chunk => chunk.Order)
                        .ToListAsync();

                Assert.Equal(
                    2,
                    chunks.Count);

                DocumentChunkEntity firstChunk =
                    Assert.Single(
                        chunks,
                        chunk => chunk.Order == 0);

                DocumentChunkEntity secondChunk =
                    Assert.Single(
                        chunks,
                        chunk => chunk.Order == 1);

                Assert.NotEqual(
                    firstChunkId,
                    firstChunk.Id);

                Assert.NotEqual(
                    secondChunkId,
                    secondChunk.Id);

                Assert.Equal(
                    DocumentChunkIdentity.CreateLogicalId(
                        documentId,
                        0),
                    firstChunk.Id);

                Assert.Equal(
                    DocumentChunkIdentity.CreateLogicalId(
                        documentId,
                        1),
                    secondChunk.Id);

                Assert.Equal(
                    documentId,
                    firstChunk.DocumentId);

                Assert.Equal(
                    documentId,
                    secondChunk.DocumentId);

                Assert.Equal(
                    firstChunkText,
                    firstChunk.Text);

                Assert.Equal(
                    secondChunkText,
                    secondChunk.Text);

                Assert.Equal(
                    DocumentChunkIdentity.ComputeContentHash(
                        firstChunkText),
                    firstChunk.ContentHash);

                Assert.Equal(
                    DocumentChunkIdentity.ComputeContentHash(
                        secondChunkText),
                    secondChunk.ContentHash);

                Assert.Equal(
                    0L,
                    firstChunk.ProcessingGeneration);

                Assert.Equal(
                    0L,
                    secondChunk.ProcessingGeneration);

                Assert.Null(
                    firstChunk.SourceLocationStartLine);

                Assert.Null(
                    firstChunk.SourceLocationEndLine);

                Assert.Null(
                    secondChunk.SourceLocationStartLine);

                Assert.Null(
                    secondChunk.SourceLocationEndLine);

                SearchDocumentsPage searchPage =
                    await searchHandler.HandleAsync(
                        new SearchDocumentsQuery(
                            "plaintext migration"));

                IReadOnlyList<SearchDocumentsResult> searchResults =
                    searchPage.Results;

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
                    matchingResult.Matches,
                    match =>
                        match.Source == SearchMatchSource.ProcessedContent &&
                        match.Context.Contains(
                            "plaintext migration",
                            StringComparison.OrdinalIgnoreCase));
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

    [Fact]
    public async Task EncryptedDatabase_WhenMigrationBackupRemainsAfterPromotion_IsRecoveredAndBackupRemoved()
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
                2,
                11,
                45,
                0,
                DateTimeKind.Utc);

        string databasePath =
            Path.Combine(
                rootDirectory,
                "DeskVault.db");

        string migrationBackupPath =
            databasePath +
            ".migration-backup";

        string backupSourcePath =
            Path.Combine(
                rootDirectory,
                "migration-backup-source.db");

        try
        {
            CreatePlaintextDeskVaultDatabase(
                databasePath,
                documentId,
                firstChunkId,
                secondChunkId,
                importedAt);

            ServiceProvider migrationServiceProvider =
                BuildServiceProvider(
                    rootDirectory,
                    databaseKey);

            await using (migrationServiceProvider)
            {
                var initializer =
                    migrationServiceProvider.GetRequiredService<DatabaseInitializer>();

                await initializer.InitializeAsync();
            }

            Assert.False(
                IsPlaintextSqliteDatabase(
                    databasePath));

            AssertEncryptedDocumentExists(
                databasePath,
                documentId,
                databaseKey);

            CreatePlaintextDeskVaultDatabase(
                backupSourcePath,
                documentId,
                firstChunkId,
                secondChunkId,
                importedAt);

            File.Copy(
                backupSourcePath,
                migrationBackupPath);

            Assert.True(
                File.Exists(
                    migrationBackupPath));

            Assert.True(
                IsPlaintextSqliteDatabase(
                    migrationBackupPath));

            ServiceProvider recoveryServiceProvider =
                BuildServiceProvider(
                    rootDirectory,
                    databaseKey);

            await using (recoveryServiceProvider)
            {
                var initializer =
                    recoveryServiceProvider.GetRequiredService<DatabaseInitializer>();

                await initializer.InitializeAsync();

                Assert.False(
                    IsPlaintextSqliteDatabase(
                        databasePath));

                Assert.False(
                    File.Exists(
                        databasePath + ".migration"));

                Assert.False(
                    File.Exists(
                        migrationBackupPath));

                AssertEncryptedDocumentExists(
                    databasePath,
                    documentId,
                    databaseKey);
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

    [Fact]
    public async Task PlaintextDatabase_WhenInitializedConcurrentlyThroughProductionInfrastructurePath_PreservesConsistentFinalState()
    {
        // Arrange
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
                3,
                12,
                15,
                0,
                DateTimeKind.Utc);

        string databasePath =
            Path.Combine(
                rootDirectory,
                "DeskVault.db");

        string initializationLockPath =
            databasePath +
            ".initialization.lock";

        try
        {
            CreatePlaintextDeskVaultDatabase(
                databasePath,
                documentId,
                firstChunkId,
                secondChunkId,
                importedAt);

            ServiceProvider firstServiceProvider =
                BuildServiceProvider(
                    rootDirectory,
                    databaseKey);

            ServiceProvider secondServiceProvider =
                BuildServiceProvider(
                    rootDirectory,
                    databaseKey);

            await using (firstServiceProvider)
            await using (secondServiceProvider)
            using (FileStream initializationLock =
                new(
                    initializationLockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 1,
                    options: FileOptions.Asynchronous))
            {
                DatabaseInitializer firstInitializer =
                    firstServiceProvider.GetRequiredService<DatabaseInitializer>();

                DatabaseInitializer secondInitializer =
                    secondServiceProvider.GetRequiredService<DatabaseInitializer>();

                TaskCompletionSource<bool> startGate =
                    new(
                        TaskCreationOptions.RunContinuationsAsynchronously);

                Task firstInitialization =
                    Task.Run(
                        async () =>
                        {
                            await startGate.Task;

                            await firstInitializer.InitializeAsync();
                        });

                Task secondInitialization =
                    Task.Run(
                        async () =>
                        {
                            await startGate.Task;

                            await secondInitializer.InitializeAsync();
                        });

                // Act
                startGate.SetResult(true);

                await Task.Delay(
                    TimeSpan.FromMilliseconds(500));

                // Assert
                Assert.False(
                    firstInitialization.IsCompleted);

                Assert.False(
                    secondInitialization.IsCompleted);

                initializationLock.Dispose();

                await Task.WhenAll(
                    firstInitialization,
                    secondInitialization);

                Assert.True(
                    File.Exists(
                        databasePath));

                Assert.False(
                    IsPlaintextSqliteDatabase(
                        databasePath));

                Assert.False(
                    File.Exists(
                        databasePath + ".migration"));

                Assert.False(
                    File.Exists(
                        databasePath + ".migration-backup"));

                AssertEncryptedDocumentExists(
                    databasePath,
                    documentId,
                    databaseKey);
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

    [Fact]
    public async Task ExistingWorkspaceEraVault_WhenUpgradedThroughProductionInfrastructurePath_PreservesExistingDataAndEstablishesCurrentSchema()
    {
        // Arrange
        SQLitePCL.Batteries_V2.Init();

        string rootDirectory =
            CreateTemporaryDirectory();

        byte[] databaseKey =
            RandomNumberGenerator.GetBytes(32);

        byte[]? documentEncryptionKey = null;

        Guid documentId =
            Guid.NewGuid();

        Guid chunkId =
            DocumentChunkIdentity.CreateLogicalId(
                documentId,
                0);

        Guid workspaceId =
            Guid.NewGuid();

        DateTime importedAt =
            new DateTime(
                2026,
                8,
                20,
                10,
                30,
                0,
                DateTimeKind.Utc);

        const string chunkText =
            "Existing workspace-era data must survive schema evolution.";

        string databasePath =
            Path.Combine(
                rootDirectory,
                "DeskVault.db");

        string expectedStoredFilePath =
            Path.Combine(
                rootDirectory,
                "Documents",
                $"{documentId}.dvault");

        try
        {
            DbContextOptions<DeskVaultDbContext> options =
                new DbContextOptionsBuilder<DeskVaultDbContext>()
                    .UseSqlite(
                        $"Data Source={databasePath};Pooling=False")
                    .Options;

            await using (var legacyDbContext =
                new DeskVaultDbContext(options))
            {
                await legacyDbContext.Database.MigrateAsync(
                    "20260917093951_AddWorkspacePersistence");

                await legacyDbContext.Database.CloseConnectionAsync();
            }

            await using (SqliteConnection connection =
                new($"Data Source={databasePath};Pooling=False"))
            {
                await connection.OpenAsync();

                await using SqliteCommand command =
                    connection.CreateCommand();

                command.CommandText =
                    """
                INSERT INTO "Documents" (
                    "Id",
                    "FileName",
                    "DisplayName",
                    "Sha256Hash",
                    "ImportedAt",
                    "Status",
                    "StoredFilePath",
                    "ProcessingGeneration",
                    "LastSuccessfulProcessingGeneration")
                VALUES (
                    $documentId,
                    'workspace-era-test.txt',
                    'Workspace Era Test',
                    'cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc',
                    $importedAt,
                    $status,
                    $storedFilePath,
                    0,
                    0);

                INSERT INTO "DocumentChunks" (
                    "Id",
                    "DocumentId",
                    "Order",
                    "Text",
                    "ContentHash",
                    "ProcessingGeneration")
                VALUES (
                    $chunkId,
                    $documentId,
                    0,
                    $chunkText,
                    $contentHash,
                    0);

                INSERT INTO "Workspaces" (
                    "Id",
                    "Name",
                    "Type",
                    "LastActiveDocumentId")
                VALUES (
                    $workspaceId,
                    'Existing Research Workspace',
                    1,
                    $documentId);

                INSERT INTO "WorkspaceDocumentMemberships" (
                    "WorkspaceId",
                    "DocumentId",
                    "Order")
                VALUES (
                    $workspaceId,
                    $documentId,
                    0);
                """;

                command.Parameters.AddWithValue(
                    "$documentId",
                    documentId.ToString().ToUpperInvariant());

                command.Parameters.AddWithValue(
                    "$importedAt",
                    importedAt.ToString("O"));

                command.Parameters.AddWithValue(
                    "$status",
                    (int)DocumentStatus.Available);

                command.Parameters.AddWithValue(
                    "$storedFilePath",
                    expectedStoredFilePath);

                command.Parameters.AddWithValue(
                    "$chunkId",
                    chunkId.ToString().ToUpperInvariant());

                command.Parameters.AddWithValue(
                    "$chunkText",
                    chunkText);

                command.Parameters.AddWithValue(
                    "$contentHash",
                    DocumentChunkIdentity.ComputeContentHash(
                        chunkText));

                command.Parameters.AddWithValue(
                    "$workspaceId",
                    workspaceId.ToString().ToUpperInvariant());

                await command.ExecuteNonQueryAsync();
            }

            documentEncryptionKey =
                RandomNumberGenerator.GetBytes(32);

            byte[] existingSourceContent =
                Encoding.UTF8.GetBytes(
                    "Existing encrypted source artifact must survive schema evolution.");

            var dataPaths =
                new DeskVaultDataPaths(
                    rootDirectory);

            var documentEncryptionService =
                new DocumentEncryptionService(
                    new TestDocumentEncryptionKeyService(
                        documentEncryptionKey),
                    Microsoft.Extensions.Logging.Abstractions.NullLogger<DocumentEncryptionService>.Instance);

            var storageService =
                new FileSystemStorageService(
                    documentEncryptionService,
                    dataPaths,
                    new DocumentArtifactPathResolver(dataPaths),
                    Microsoft.Extensions.Logging.Abstractions.NullLogger<FileSystemStorageService>.Instance);

            string sourceFilePath =
                Path.Combine(
                    rootDirectory,
                    "existing-source.txt");

            await File.WriteAllBytesAsync(
                sourceFilePath,
                existingSourceContent);

            string storedArtifactPath =
                await storageService.StoreAsync(
                    sourceFilePath,
                    documentId);

            Assert.Equal(
                expectedStoredFilePath,
                storedArtifactPath);

            Assert.True(
                IsPlaintextSqliteDatabase(
                    databasePath));

            ServiceProvider serviceProvider =
                BuildServiceProvider(
                    rootDirectory,
                    databaseKey);

            await using (serviceProvider)
            {
                DatabaseInitializer initializer =
                    serviceProvider.GetRequiredService<DatabaseInitializer>();

                // Act
                await initializer.InitializeAsync();

                // Assert
                Assert.False(
                    IsPlaintextSqliteDatabase(
                        databasePath));

                Assert.False(
                    File.Exists(
                        databasePath + ".migration"));

                Assert.False(
                    File.Exists(
                        databasePath + ".migration-backup"));

                IDbContextFactory<DeskVaultDbContext> factory =
                    serviceProvider.GetRequiredService<
                        IDbContextFactory<DeskVaultDbContext>>();

                await using DeskVaultDbContext dbContext =
                    await factory.CreateDbContextAsync();

                DocumentEntity document =
                    Assert.Single(
                        await dbContext.Documents
                            .AsNoTracking()
                            .Where(
                                document =>
                                    document.Id ==
                                    documentId)
                            .ToListAsync());

                Assert.Equal(
                    documentId,
                    document.Id);

                Assert.Equal(
                    "workspace-era-test.txt",
                    document.FileName);

                Assert.Equal(
                    "Workspace Era Test",
                    document.DisplayName);

                Assert.Equal(
                    importedAt,
                    document.ImportedAt);

                Assert.Equal(
                    DocumentStatus.Available,
                    (DocumentStatus)document.Status);

                Assert.Equal(
                    expectedStoredFilePath,
                    document.StoredFilePath);

                Assert.Equal(
                    0L,
                    document.ProcessingGeneration);

                Assert.Equal(
                    0L,
                    document.LastSuccessfulProcessingGeneration);

                DocumentChunkEntity chunk =
                    Assert.Single(
                        await dbContext.DocumentChunks
                            .AsNoTracking()
                            .Where(
                                chunk =>
                                    chunk.Id ==
                                    chunkId)
                            .ToListAsync());

                Assert.Equal(
                    chunkId,
                    chunk.Id);

                Assert.Equal(
                    documentId,
                    chunk.DocumentId);

                Assert.Equal(
                    chunkText,
                    chunk.Text);

                Assert.Equal(
                    DocumentChunkIdentity.ComputeContentHash(
                        chunkText),
                    chunk.ContentHash);

                Assert.Equal(
                    0L,
                    chunk.ProcessingGeneration);

                Assert.Null(
                    chunk.SourceLocationStartLine);

                Assert.Null(
                    chunk.SourceLocationEndLine);

                WorkspaceEntity workspace =
                    Assert.Single(
                        await dbContext.Workspaces
                            .AsNoTracking()
                            .Where(
                                workspace =>
                                    workspace.Id ==
                                    workspaceId)
                            .ToListAsync());

                Assert.Equal(
                    workspaceId,
                    workspace.Id);

                Assert.Equal(
                    "Existing Research Workspace",
                    workspace.Name);

                Assert.Null(
                    workspace.Description);

                Assert.Equal(
                    1,
                    workspace.Type);

                Assert.Equal(
                    documentId,
                    workspace.LastActiveDocumentId);

                Assert.NotEqual(
                    default,
                    workspace.LastUpdated);

                WorkspaceDocumentMembershipEntity membership =
                    Assert.Single(
                        await dbContext.WorkspaceDocumentMemberships
                            .AsNoTracking()
                            .Where(
                                membership =>
                                    membership.WorkspaceId ==
                                    workspaceId &&
                                    membership.DocumentId ==
                                    documentId)
                            .ToListAsync());

                Assert.Equal(
                    workspaceId,
                    membership.WorkspaceId);

                Assert.Equal(
                    documentId,
                    membership.DocumentId);

                Assert.Equal(
                    0,
                    membership.Order);

                SearchDocumentsHandler searchHandler =
                    serviceProvider.GetRequiredService<
                        SearchDocumentsHandler>();

                SearchDocumentsPage searchPage =
                    await searchHandler.HandleAsync(
                        new SearchDocumentsQuery(
                            "workspace-era data"));

                IReadOnlyList<SearchDocumentsResult> searchResults =
                    searchPage.Results;

                SearchDocumentsResult matchingResult =
                    Assert.Single(
                        searchResults,
                        result =>
                            result.DocumentId ==
                            documentId);

                Assert.Equal(
                    "Workspace Era Test",
                    matchingResult.DisplayName);

                Assert.Contains(
                    matchingResult.Matches,
                    match =>
                        match.Context.Contains(
                            "Existing workspace-era data",
                            StringComparison.OrdinalIgnoreCase));
            }

            var documentReader =
                new EncryptedDocumentReader(
                    documentEncryptionService,
                    new DocumentArtifactPathResolver(
                        dataPaths),
                    Microsoft.Extensions.Logging.Abstractions.NullLogger<EncryptedDocumentReader>.Instance);

            await using Stream decryptedArtifact =
                await documentReader.OpenReadAsync(
                    documentId);

            using var decryptedMemory =
                new MemoryStream();

            await decryptedArtifact.CopyToAsync(
                decryptedMemory);

            Assert.Equal(
                existingSourceContent,
                decryptedMemory.ToArray());
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                databaseKey);

            if (documentEncryptionKey is not null)
            {
                CryptographicOperations.ZeroMemory(
                    documentEncryptionKey);
            }

            DeleteTemporaryDirectory(
                rootDirectory);
        }
    }

    [Fact]
    public async Task ExistingWorkspaceEraVault_WhenSchemaMigrationFails_RetryCompletesUpgradeSafely()
    {
        // Arrange
        SQLitePCL.Batteries_V2.Init();

        string rootDirectory =
            CreateTemporaryDirectory();

        byte[] databaseKey =
            RandomNumberGenerator.GetBytes(32);

        Guid documentId =
            Guid.NewGuid();

        Guid firstChunkId =
            DocumentChunkIdentity.CreateLogicalId(
                documentId,
                0);

        Guid secondChunkId =
            DocumentChunkIdentity.CreateLogicalId(
                documentId,
                1);

        Guid workspaceId =
            Guid.NewGuid();

        DateTime importedAt =
            new DateTime(
                2026,
                8,
                21,
                10,
                30,
                0,
                DateTimeKind.Utc);

        const string firstChunkText =
            "Existing data must remain intact when schema migration fails.";

        const string secondChunkText =
            "The migration must be safely retryable after the failure is corrected.";

        string databasePath =
            Path.Combine(
                rootDirectory,
                "DeskVault.db");

        string databasePassword =
            Convert.ToBase64String(
                databaseKey);

        try
        {
            DbContextOptions<DeskVaultDbContext> legacyOptions =
                new DbContextOptionsBuilder<DeskVaultDbContext>()
                    .UseSqlite(
                        $"Data Source={databasePath};Pooling=False")
                    .Options;

            await using (var legacyDbContext =
                new DeskVaultDbContext(legacyOptions))
            {
                await legacyDbContext.Database.MigrateAsync(
                    "20260917093951_AddWorkspacePersistence");

                await legacyDbContext.Database.CloseConnectionAsync();
            }

            await using (SqliteConnection legacyConnection =
                new(
                    $"Data Source={databasePath};Pooling=False"))
            {
                await legacyConnection.OpenAsync();

                await using SqliteCommand legacyCommand =
                    legacyConnection.CreateCommand();

                legacyCommand.CommandText =
                    """
                INSERT INTO "Documents" (
                    "Id",
                    "FileName",
                    "DisplayName",
                    "Sha256Hash",
                    "ImportedAt",
                    "Status",
                    "StoredFilePath",
                    "ProcessingGeneration",
                    "LastSuccessfulProcessingGeneration")
                VALUES (
                    $documentId,
                    'schema-retry-test.txt',
                    'Schema Retry Test',
                    'dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd',
                    $importedAt,
                    $status,
                    $storedFilePath,
                    0,
                    0);

                INSERT INTO "DocumentChunks" (
                    "Id",
                    "DocumentId",
                    "Order",
                    "Text",
                    "ContentHash",
                    "ProcessingGeneration")
                VALUES (
                    $firstChunkId,
                    $documentId,
                    0,
                    $firstChunkText,
                    $firstChunkContentHash,
                    0);

                INSERT INTO "DocumentChunks" (
                    "Id",
                    "DocumentId",
                    "Order",
                    "Text",
                    "ContentHash",
                    "ProcessingGeneration")
                VALUES (
                    $secondChunkId,
                    $documentId,
                    1,
                    $secondChunkText,
                    $secondChunkContentHash,
                    0);

                INSERT INTO "Workspaces" (
                    "Id",
                    "Name",
                    "Type",
                    "LastActiveDocumentId")
                VALUES (
                    $workspaceId,
                    'Schema Retry Workspace',
                    1,
                    $documentId);

                INSERT INTO "WorkspaceDocumentMemberships" (
                    "WorkspaceId",
                    "DocumentId",
                    "Order")
                VALUES (
                    $workspaceId,
                    $documentId,
                    0);
                """;

                legacyCommand.Parameters.AddWithValue(
                    "$documentId",
                    documentId.ToString().ToUpperInvariant());

                legacyCommand.Parameters.AddWithValue(
                    "$importedAt",
                    importedAt.ToString("O"));

                legacyCommand.Parameters.AddWithValue(
                    "$status",
                    (int)DocumentStatus.Available);

                legacyCommand.Parameters.AddWithValue(
                    "$storedFilePath",
                    Path.Combine(
                        rootDirectory,
                        "Documents",
                        $"{documentId}.dvault"));

                legacyCommand.Parameters.AddWithValue(
                    "$firstChunkId",
                    firstChunkId.ToString().ToUpperInvariant());

                legacyCommand.Parameters.AddWithValue(
                    "$firstChunkText",
                    firstChunkText);

                legacyCommand.Parameters.AddWithValue(
                    "$firstChunkContentHash",
                    DocumentChunkIdentity.ComputeContentHash(
                        firstChunkText));

                legacyCommand.Parameters.AddWithValue(
                    "$secondChunkId",
                    secondChunkId.ToString().ToUpperInvariant());

                legacyCommand.Parameters.AddWithValue(
                    "$secondChunkText",
                    secondChunkText);

                legacyCommand.Parameters.AddWithValue(
                    "$secondChunkContentHash",
                    DocumentChunkIdentity.ComputeContentHash(
                        secondChunkText));

                legacyCommand.Parameters.AddWithValue(
                    "$workspaceId",
                    workspaceId.ToString().ToUpperInvariant());

                await legacyCommand.ExecuteNonQueryAsync();
            }

            ServiceProvider encryptionServiceProvider =
                BuildServiceProvider(
                    rootDirectory,
                    databaseKey);

            await using (encryptionServiceProvider)
            {
                IDatabaseEncryptionMigrator encryptionMigrator =
                    encryptionServiceProvider.GetRequiredService<
                        IDatabaseEncryptionMigrator>();

                await encryptionMigrator.MigrateAsync(
                    databasePath,
                    databaseKey);
            }

            Assert.False(
                IsPlaintextSqliteDatabase(
                    databasePath));

            await using (SqliteConnection setupConnection =
                new(
                    $"Data Source={databasePath};Password={databasePassword};Pooling=False"))
            {
                await setupConnection.OpenAsync();

                await using SqliteCommand setupCommand =
                    setupConnection.CreateCommand();

                setupCommand.CommandText =
                    """
                ALTER TABLE "Workspaces"
                ADD COLUMN "LastUpdated" TEXT;
                """;

                await setupCommand.ExecuteNonQueryAsync();
            }

            // Act - first initialization must fail during the pending schema migration.
            ServiceProvider failingServiceProvider =
                BuildServiceProvider(
                    rootDirectory,
                    databaseKey);

            await using (failingServiceProvider)
            {
                DatabaseInitializer initializer =
                    failingServiceProvider.GetRequiredService<DatabaseInitializer>();

                Exception exception =
                    await Assert.ThrowsAnyAsync<Exception>(
                        () =>
                            initializer.InitializeAsync());

                Assert.Contains(
                    "LastUpdated",
                    exception.ToString(),
                    StringComparison.OrdinalIgnoreCase);
            }

            // Assert - the failed migration was not recorded as successfully applied.
            await using (SqliteConnection failedConnection =
                new(
                    $"Data Source={databasePath};Password={databasePassword};Pooling=False"))
            {
                await failedConnection.OpenAsync();

                await using SqliteCommand migrationHistoryCommand =
                    failedConnection.CreateCommand();

                migrationHistoryCommand.CommandText =
                    """
                SELECT COUNT(*)
                FROM "__EFMigrationsHistory"
                WHERE "MigrationId" = '20260922113712_AddWorkspaceMetadata';
                """;

                object? result =
                    await migrationHistoryCommand.ExecuteScalarAsync();

                Assert.Equal(
                    0L,
                    Convert.ToInt64(result));

                await using SqliteCommand documentCountCommand =
                    failedConnection.CreateCommand();

                documentCountCommand.CommandText =
                    """
                SELECT COUNT(*)
                FROM "Documents"
                WHERE "Id" = $documentId;
                """;

                documentCountCommand.Parameters.AddWithValue(
                    "$documentId",
                    documentId.ToString().ToUpperInvariant());

                object? documentCountResult =
                    await documentCountCommand.ExecuteScalarAsync();

                Assert.Equal(
                    1L,
                    Convert.ToInt64(documentCountResult));
            }

            // Repair the deliberately invalid schema state so the migration can be retried.
            await using (SqliteConnection repairConnection =
                new(
                    $"Data Source={databasePath};Password={databasePassword};Pooling=False"))
            {
                await repairConnection.OpenAsync();

                await using SqliteCommand repairCommand =
                    repairConnection.CreateCommand();

                repairCommand.CommandText =
                    """
                ALTER TABLE "Workspaces"
                DROP COLUMN "LastUpdated";
                """;

                await repairCommand.ExecuteNonQueryAsync();
            }

            // Act - retry against the same vault.
            ServiceProvider retryServiceProvider =
                BuildServiceProvider(
                    rootDirectory,
                    databaseKey);

            await using (retryServiceProvider)
            {
                DatabaseInitializer initializer =
                    retryServiceProvider.GetRequiredService<DatabaseInitializer>();

                await initializer.InitializeAsync();

                IDbContextFactory<DeskVaultDbContext> factory =
                    retryServiceProvider.GetRequiredService<
                        IDbContextFactory<DeskVaultDbContext>>();

                await using DeskVaultDbContext dbContext =
                    await factory.CreateDbContextAsync();

                DocumentEntity document =
                    Assert.Single(
                        await dbContext.Documents
                            .AsNoTracking()
                            .Where(
                                document =>
                                    document.Id ==
                                    documentId)
                            .ToListAsync());

                Assert.Equal(
                    documentId,
                    document.Id);

                Assert.Equal(
                    "schema-retry-test.txt",
                    document.FileName);

                Assert.Equal(
                    "Schema Retry Test",
                    document.DisplayName);

                Assert.Equal(
                    importedAt,
                    document.ImportedAt);

                Assert.Equal(
                    DocumentStatus.Available,
                    (DocumentStatus)document.Status);

                Assert.Equal(
                    0L,
                    document.ProcessingGeneration);

                Assert.Equal(
                    0L,
                    document.LastSuccessfulProcessingGeneration);

                List<DocumentChunkEntity> chunks =
                    await dbContext.DocumentChunks
                        .AsNoTracking()
                        .Where(
                            chunk =>
                                chunk.DocumentId ==
                                documentId)
                        .OrderBy(
                            chunk =>
                                chunk.Order)
                        .ToListAsync();

                Assert.Equal(
                    2,
                    chunks.Count);

                Assert.Equal(
                    firstChunkId,
                    chunks[0].Id);

                Assert.Equal(
                    firstChunkText,
                    chunks[0].Text);

                Assert.Equal(
                    DocumentChunkIdentity.ComputeContentHash(
                        firstChunkText),
                    chunks[0].ContentHash);

                Assert.Equal(
                    secondChunkId,
                    chunks[1].Id);

                Assert.Equal(
                    secondChunkText,
                    chunks[1].Text);

                Assert.Equal(
                    DocumentChunkIdentity.ComputeContentHash(
                        secondChunkText),
                    chunks[1].ContentHash);

                WorkspaceEntity workspace =
                    Assert.Single(
                        await dbContext.Workspaces
                            .AsNoTracking()
                            .Where(
                                workspace =>
                                    workspace.Id ==
                                    workspaceId)
                            .ToListAsync());

                Assert.Equal(
                    workspaceId,
                    workspace.Id);

                Assert.Equal(
                    "Schema Retry Workspace",
                    workspace.Name);

                Assert.Equal(
                    documentId,
                    workspace.LastActiveDocumentId);

                Assert.NotEqual(
                    default,
                    workspace.LastUpdated);

                WorkspaceDocumentMembershipEntity membership =
                    Assert.Single(
                        await dbContext.WorkspaceDocumentMemberships
                            .AsNoTracking()
                            .Where(
                                membership =>
                                    membership.WorkspaceId ==
                                    workspaceId &&
                                    membership.DocumentId ==
                                    documentId)
                            .ToListAsync());

                Assert.Equal(
                    workspaceId,
                    membership.WorkspaceId);

                Assert.Equal(
                    documentId,
                    membership.DocumentId);

                Assert.Equal(
                    0,
                    membership.Order);
            }

            Assert.False(
                IsPlaintextSqliteDatabase(
                    databasePath));

            Assert.False(
                File.Exists(
                    databasePath + ".migration"));

            Assert.False(
                File.Exists(
                    databasePath + ".migration-backup"));

            await using (SqliteConnection finalConnection =
                new(
                    $"Data Source={databasePath};Password={databasePassword};Pooling=False"))
            {
                await finalConnection.OpenAsync();

                await using SqliteCommand finalMigrationHistoryCommand =
                    finalConnection.CreateCommand();

                finalMigrationHistoryCommand.CommandText =
                    """
                SELECT COUNT(*)
                FROM "__EFMigrationsHistory"
                WHERE "MigrationId" = '20260922113712_AddWorkspaceMetadata';
                """;

                object? finalResult =
                    await finalMigrationHistoryCommand.ExecuteScalarAsync();

                Assert.Equal(
                    1L,
                    Convert.ToInt64(finalResult));
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

    private sealed class TestDocumentEncryptionKeyService
        : IEncryptionKeyService
    {
        private readonly byte[] _key;

        public TestDocumentEncryptionKeyService(
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

        public Task<byte[]> GetKeyAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                _key.ToArray());
        }
    }
}
