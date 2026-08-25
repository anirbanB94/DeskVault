using System.Security.Cryptography;
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

internal sealed class DocumentPipelineTestHarness : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    public DeskVaultDataPaths DataPaths { get; }

    public ImportDocumentHandler ImportHandler { get; }

    public SearchDocumentsHandler SearchHandler { get; }

    public DocumentPipelineTestHarness(
        string rootDirectory)
    {
        DataPaths =
            new DeskVaultDataPaths(
                rootDirectory);

        _connection =
            CreateConnection();

        var repository =
            CreateRepository();

        var processingStore =
            CreateProcessingStore();

        var searchStore =
            CreateSearchStore();

        var encryptionService =
            new DocumentEncryptionService(
                new TestEncryptionKeyService(
                    RandomNumberGenerator.GetBytes(32)),
                NullLogger<DocumentEncryptionService>.Instance);

        var storageService =
            new FileSystemStorageService(
                encryptionService,
                DataPaths,
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

        ImportHandler =
            new ImportDocumentHandler(
                new ImportDocumentValidator(),
                new Sha256HashService(
                    NullLogger<Sha256HashService>.Instance),
                storageService,
                repository,
                processingService,
                NullLogger<ImportDocumentHandler>.Instance);

        SearchHandler =
            new SearchDocumentsHandler(
                searchStore,
                NullLogger<SearchDocumentsHandler>.Instance);
    }

    public async Task<Document?> GetDocumentAsync(
        Guid documentId)
    {
        var repository =
            CreateRepository();

        return await repository.GetByIdAsync(
            documentId);
    }

    public async Task<List<DocumentChunkEntity>> GetChunksAsync(
        Guid documentId)
    {
        await using DeskVaultDbContext context =
            CreateContext();

        return await context.DocumentChunks
            .AsNoTracking()
            .Where(
                chunk =>
                    chunk.DocumentId == documentId)
            .OrderBy(
                chunk => chunk.Order)
            .ToListAsync();
    }

    private SqliteDocumentRepository CreateRepository()
    {
        return new SqliteDocumentRepository(
            CreateFactory(),
            NullLogger<SqliteDocumentRepository>.Instance);
    }

    private SqliteDocumentProcessingStore CreateProcessingStore()
    {
        return new SqliteDocumentProcessingStore(
            CreateFactory(),
            NullLogger<SqliteDocumentProcessingStore>.Instance);
    }

    private SqliteDocumentSearchStore CreateSearchStore()
    {
        return new SqliteDocumentSearchStore(
            CreateFactory(),
            NullLogger<SqliteDocumentSearchStore>.Instance);
    }

    private IDbContextFactory<DeskVaultDbContext> CreateFactory()
    {
        return new TestDbContextFactory(
            _connection);
    }

    private DeskVaultDbContext CreateContext()
    {
        DbContextOptions<DeskVaultDbContext> options =
            new DbContextOptionsBuilder<DeskVaultDbContext>()
                .UseSqlite(_connection)
                .Options;

        return new DeskVaultDbContext(
            options);
    }

    private static SqliteConnection CreateConnection()
    {
        var connection =
            new SqliteConnection(
                "Data Source=:memory:");

        connection.Open();

        using var context =
            CreateContext(
                connection);

        context.Database.EnsureCreated();

        return connection;
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

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
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
            return DocumentPipelineTestHarness.CreateContext(
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
