using System.Security.Cryptography;
using System.Text;
using DeskVault.Application.Documents.Chunking;
using DeskVault.Application.Documents.Commands.ImportDocument;
using DeskVault.Application.Documents.Commands.ProcessDocument;
using DeskVault.Application.Documents.Extraction;
using DeskVault.Application.Documents.Extraction.CSVDocument;
using DeskVault.Application.Documents.Extraction.MarkdownDocument;
using DeskVault.Application.Documents.Extraction.TextDocument;
using DeskVault.Application.Documents.Normalization;
using DeskVault.Application.Documents.Processing;
using DeskVault.Application.Documents.Queries.SearchDocuments;
using DeskVault.Application.Interfaces;
using DeskVault.Domain.Documents;
using DeskVault.Infrastructure.Persistence.Context;
using DeskVault.Infrastructure.Persistence.Entities;
using DeskVault.Infrastructure.Repositories;
using DeskVault.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeskVault.Integration.Tests;

public sealed class DocumentImportIntegrationTests
{
    [Fact]
    public async Task ImportDocument_WhenValidTextDocument_CompletesProcessingAndMakesDocumentSearchable()
    {
        string rootDirectory =
            Path.Combine(
                Path.GetTempPath(),
                "DeskVaultIntegrationTests",
                Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(rootDirectory);

        try
        {
            DeskVaultDataPaths dataPaths =
                new(rootDirectory);

            string sourceFilePath =
                Path.Combine(
                    rootDirectory,
                    "integration-test.txt");

            string sourceText =
                """
                DeskVault integration testing verifies the complete document pipeline.

                This document contains searchable enterprise architecture content.
                """;

            await File.WriteAllTextAsync(
                sourceFilePath,
                sourceText,
                Encoding.UTF8);

            await using SqliteConnection connection =
                CreateConnection();

            var repository =
                CreateRepository(connection);

            var processingStore =
                CreateProcessingStore(connection);

            var searchStore =
                CreateSearchStore(connection);

            var encryptionService =
                new DocumentEncryptionService(
                    new TestEncryptionKeyService(
                        RandomNumberGenerator.GetBytes(32)),
                    NullLogger<DocumentEncryptionService>.Instance);

            var storageService =
                new FileSystemStorageService(
                    encryptionService,
                    dataPaths,
                    NullLogger<FileSystemStorageService>.Instance);

            var reader =
                new EncryptedDocumentReader(
                    encryptionService,
                    NullLogger<EncryptedDocumentReader>.Instance);

            var extractorResolver =
                new DocumentTextExtractorResolver(
                [
                    new TextDocumentTextExtractor(),
                    new MarkdownDocumentTextExtractor(),
                    new CsvDocumentTextExtractor()
                ]);

            var processHandler =
                new ProcessDocumentHandler(
                    repository,
                    reader,
                    extractorResolver,
                    new DocumentTextNormalizer(),
                    new DocumentTextChunker(
                        maxChunkSize: 4000),
                    processingStore,
                    NullLogger<ProcessDocumentHandler>.Instance);

            var processingService =
                new DocumentProcessingService(
                    processHandler);

            var importHandler =
                new ImportDocumentHandler(
                    new ImportDocumentValidator(),
                    new Sha256HashService(
                        NullLogger<Sha256HashService>.Instance),
                    storageService,
                    repository,
                    processingService,
                    NullLogger<ImportDocumentHandler>.Instance);

            ImportDocumentResult importResult =
                await importHandler.HandleAsync(
                    new ImportDocumentCommand(
                        sourceFilePath,
                        "Integration Test Document"));

            Assert.Equal(
                ImportDocumentResultStatus.Success,
                importResult.Status);

            Assert.NotNull(
                importResult.DocumentId);

            Guid documentId =
                importResult.DocumentId.Value;

            Document? document =
                await repository.GetByIdAsync(
                    documentId);

            Assert.NotNull(document);

            Assert.Equal(
                "integration-test.txt",
                document.FileName);

            Assert.Equal(
                "Integration Test Document",
                document.DisplayName);

            Assert.Equal(
                DocumentStatus.Available,
                document.Status);

            Assert.True(
                File.Exists(
                    document.StoredFilePath));

            Assert.EndsWith(
                ".dvault",
                document.StoredFilePath,
                StringComparison.OrdinalIgnoreCase);

            byte[] storedBytes =
                await File.ReadAllBytesAsync(
                    document.StoredFilePath);

            byte[] plaintextBytes =
                await File.ReadAllBytesAsync(
                    sourceFilePath);

            Assert.NotEqual(
                plaintextBytes,
                storedBytes);

            List<DocumentChunkEntity> chunks =
                await GetChunksAsync(
                    connection,
                    documentId);

            Assert.NotEmpty(
                chunks);

            string indexedText =
                string.Join(
                    "\n",
                    chunks
                        .OrderBy(
                            chunk => chunk.Order)
                        .Select(
                            chunk => chunk.Text));

            Assert.Contains(
                "DeskVault integration testing",
                indexedText);

            Assert.Contains(
                "searchable enterprise architecture content",
                indexedText);

            var searchHandler =
                new SearchDocumentsHandler(
                    searchStore,
                    NullLogger<SearchDocumentsHandler>.Instance);

            IReadOnlyList<SearchDocumentsResult> searchResults =
                await searchHandler.HandleAsync(
                    new SearchDocumentsQuery(
                        "ENTERPRISE ARCHITECTURE"));

            SearchDocumentsResult matchingResult =
                Assert.Single(searchResults, result => result.DocumentId == documentId);

            Assert.Equal(
                "integration-test.txt",
                matchingResult.FileName);

            Assert.Equal(
                "Integration Test Document",
                matchingResult.DisplayName);

            Assert.Contains(
                "enterprise architecture",
                matchingResult.ChunkText,
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(rootDirectory))
            {
                Directory.Delete(
                    rootDirectory,
                    recursive: true);
            }
        }
    }

    private static SqliteConnection CreateConnection()
    {
        var connection =
            new SqliteConnection(
                "Data Source=:memory:");

        connection.Open();

        using var context =
            CreateContext(connection);

        context.Database.EnsureCreated();

        return connection;
    }

    private static SqliteDocumentRepository CreateRepository(
        SqliteConnection connection)
    {
        return new SqliteDocumentRepository(
            CreateFactory(connection),
            NullLogger<SqliteDocumentRepository>.Instance);
    }

    private static SqliteDocumentProcessingStore CreateProcessingStore(
        SqliteConnection connection)
    {
        return new SqliteDocumentProcessingStore(
            CreateFactory(connection),
            NullLogger<SqliteDocumentProcessingStore>.Instance);
    }

    private static SqliteDocumentSearchStore CreateSearchStore(
        SqliteConnection connection)
    {
        return new SqliteDocumentSearchStore(
            CreateFactory(connection),
            NullLogger<SqliteDocumentSearchStore>.Instance);
    }

    private static async Task<List<DocumentChunkEntity>> GetChunksAsync(
        SqliteConnection connection,
        Guid documentId)
    {
        await using DeskVaultDbContext context =
            CreateContext(connection);

        return await context.DocumentChunks
            .AsNoTracking()
            .Where(
                chunk =>
                    chunk.DocumentId == documentId)
            .OrderBy(
                chunk => chunk.Order)
            .ToListAsync();
    }

    private static IDbContextFactory<DeskVaultDbContext> CreateFactory(
        SqliteConnection connection)
    {
        return new TestDbContextFactory(
            connection);
    }

    private static DeskVaultDbContext CreateContext(
        SqliteConnection connection)
    {
        DbContextOptions<DeskVaultDbContext> options =
            new DbContextOptionsBuilder<DeskVaultDbContext>()
                .UseSqlite(connection)
                .Options;

        return new DeskVaultDbContext(
            options);
    }

    private sealed class TestDbContextFactory
        : IDbContextFactory<DeskVaultDbContext>
    {
        private readonly SqliteConnection _connection;

        public TestDbContextFactory(
            SqliteConnection connection)
        {
            _connection = connection;
        }

        public DeskVaultDbContext CreateDbContext()
        {
            return CreateContext();
        }

        public Task<DeskVaultDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                CreateContext());
        }

        private DeskVaultDbContext CreateContext()
        {
            return DocumentImportIntegrationTests.CreateContext(
                _connection);
        }
    }

    private sealed class TestEncryptionKeyService
        : IEncryptionKeyService
    {
        private readonly byte[] _key;

        public TestEncryptionKeyService(
            byte[] key)
        {
            _key = key;
        }

        public Task<byte[]> GetOrCreateKeyAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                _key);
        }
    }
}
