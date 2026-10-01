using DeskVault.Application.Configurations;
using DeskVault.Application.Documents.Chunking;
using DeskVault.Application.Documents.Commands.ImportDocument;
using DeskVault.Application.Documents.Commands.ProcessDocument;
using DeskVault.Application.Documents.Commands.RecoverDocumentArtifacts;
using DeskVault.Application.Documents.Commands.RemoveDocument;
using DeskVault.Application.Documents.Extraction;
using DeskVault.Application.Documents.Extraction.CSVDocument;
using DeskVault.Application.Documents.Extraction.IniDocument;
using DeskVault.Application.Documents.Extraction.JsonDocument;
using DeskVault.Application.Documents.Extraction.MarkdownDocument;
using DeskVault.Application.Documents.Extraction.TextDocument;
using DeskVault.Application.Documents.Extraction.XmlDocument;
using DeskVault.Application.Documents.Extraction.YamlDocument;
using DeskVault.Application.Documents.Normalization;
using DeskVault.Application.Documents.Processing;
using DeskVault.Application.Documents.Queries.ReconcileDocumentArtifacts;
using DeskVault.Application.Documents.Queries.SearchDocuments;
using DeskVault.Application.Interfaces;
using DeskVault.Domain.Documents;
using DeskVault.Domain.Workspaces;
using DeskVault.Infrastructure.Persistence.Context;
using DeskVault.Infrastructure.Persistence.Entities;
using DeskVault.Infrastructure.Repositories;
using DeskVault.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Cryptography;

namespace DeskVault.Integration.Tests;

internal sealed class DocumentPipelineTestHarness : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    private readonly FileSystemStorageService _storageService;

    private readonly byte[] _encryptionKey;

    public DeskVaultDataPaths DataPaths { get; }

    public ImportDocumentHandler ImportHandler { get; }

    public DocumentProcessingService ProcessingService { get; }

    public SearchDocumentsHandler SearchHandler { get; }

    public RemoveDocumentHandler RemoveHandler { get; }

    public RecoverDocumentArtifactsHandler RecoveryHandler { get; }

    public ReconcileDocumentArtifactsHandler ReconciliationHandler { get; }

    public EncryptedDocumentReader DocumentReader { get; }

    public DocumentPipelineTestHarness(
        string rootDirectory,
        string databasePath,
        byte[] encryptionKey,
        IEnumerable<IDocumentTextExtractor>? extractors = null,
        IHashService? hashService = null,
        Func<IStorageService, IStorageService>? importStorageDecorator = null,
        Func<IDocumentRepository, IDocumentRepository>? importRepositoryDecorator = null,
        IDocumentTextChunker? chunker = null,
        DocumentProcessingOptions? processingOptions = null,
        Func<IDocumentRepository, IDocumentRepository>? removeRepositoryDecorator = null)
    {
        ArgumentNullException.ThrowIfNull(encryptionKey);

        DataPaths =
            new DeskVaultDataPaths(
                rootDirectory);

        _encryptionKey =
            [.. encryptionKey];

        _connection =
            CreateConnection(
                databasePath);

        var repository =
            CreateRepository();

        IDocumentRepository importRepository =
            importRepositoryDecorator is null
            ? repository
            : importRepositoryDecorator(
                repository);

        var processingStore =
            CreateProcessingStore();

        var searchStore =
            CreateSearchStore();

        var encryptionService =
            new DocumentEncryptionService(
                new TestEncryptionKeyService(
                    _encryptionKey),
                NullLogger<DocumentEncryptionService>.Instance);

        var artifactPathResolver =
            new DocumentArtifactPathResolver(
                DataPaths);

        _storageService =
            new FileSystemStorageService(
                encryptionService,
                DataPaths,
                artifactPathResolver,
                NullLogger<FileSystemStorageService>.Instance);

        DocumentReader =
            new EncryptedDocumentReader(
                encryptionService,
                artifactPathResolver,
                NullLogger<EncryptedDocumentReader>.Instance);

        var extractorResolver =
            new DocumentTextExtractorResolver(
                extractors ??
                [
                    new TextDocumentTextExtractor(),
                    new MarkdownDocumentTextExtractor(),
                    new CsvDocumentTextExtractor(),
                    new JsonDocumentTextExtractor(),
                    new XmlDocumentTextExtractor(),
                    new YamlDocumentTextExtractor(),
                    new IniDocumentTextExtractor()
                ]);

        var processHandler =
            CreateProcessHandler(
                repository,
                processingStore,
                DocumentReader,
                extractorResolver,
                chunker,
                processingOptions);

        ProcessingService =
            new DocumentProcessingService(
                processHandler);

        IStorageService importStorageService =
            importStorageDecorator is null
                ? _storageService
                : importStorageDecorator(
                    _storageService);

        IHashService importHashService =
            hashService ??
            new Sha256HashService(
                NullLogger<Sha256HashService>.Instance);

        ImportHandler =
            new ImportDocumentHandler(
                new ImportDocumentValidator(),
                importHashService,
                importStorageService,
                importRepository,
                NullLogger<ImportDocumentHandler>.Instance);

        SearchHandler =
            new SearchDocumentsHandler(
                searchStore,
                new SearchDocumentsRanker(),
                NullLogger<SearchDocumentsHandler>.Instance);

        var workspaceRepository =
            CreateWorkspaceRepository();

        IDocumentRepository removeRepository =
            removeRepositoryDecorator is null
                ? repository
                : removeRepositoryDecorator(
                    repository);

        RemoveHandler =
            new RemoveDocumentHandler(
                removeRepository,
                workspaceRepository,
                _storageService,
                NullLogger<RemoveDocumentHandler>.Instance);

        ReconciliationHandler =
            new ReconcileDocumentArtifactsHandler(
                repository,
                new DocumentArtifactEnumerator(
                    DataPaths),
                DocumentReader,
                _storageService,
                new Sha256HashService(
                    NullLogger<Sha256HashService>.Instance),
                NullLogger<ReconcileDocumentArtifactsHandler>.Instance);

        RecoveryHandler =
            new RecoverDocumentArtifactsHandler(
                repository,
                ReconciliationHandler,
                _storageService,
                NullLogger<RecoverDocumentArtifactsHandler>.Instance);
    }

    public async Task<Document?> GetDocumentAsync(
        Guid documentId)
    {
        var repository =
            CreateRepository();

        return await repository.GetByIdAsync(
            documentId);
    }

    public async Task<IReadOnlyList<Document>> GetDocumentsAsync()
    {
        var repository =
            CreateRepository();

        return await repository.GetAllAsync();
    }

    public async Task PersistDocumentAsync(
        Document document)
    {
        var repository =
            CreateRepository();

        await repository.AddAsync(
            document);
    }

    public async Task<Workspace?> GetWorkspaceAsync(
        Guid workspaceId)
    {
        var repository =
            CreateWorkspaceRepository();

        return await repository.GetByIdAsync(
            workspaceId);
    }

    public async Task PersistWorkspaceAsync(
        Workspace workspace)
    {
        var repository =
            CreateWorkspaceRepository();

        await repository.AddAsync(
            workspace);
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

    public Task StoreArtifactAsync(
        string sourceFilePath,
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        return _storageService.StoreAsync(
            sourceFilePath,
            documentId,
            cancellationToken);
    }

    private static ProcessDocumentHandler CreateProcessHandler(
        SqliteDocumentRepository repository,
        SqliteDocumentProcessingStore processingStore,
        EncryptedDocumentReader reader,
        DocumentTextExtractorResolver extractorResolver,
        IDocumentTextChunker? chunker,
        DocumentProcessingOptions? processingOptions)
    {
        return new ProcessDocumentHandler(
            repository,
            reader,
            extractorResolver,
            new DocumentTextNormalizer(),
            chunker ??
                new DocumentTextChunker(
                    maxChunkSize: 4000),
            processingStore,
            processingOptions ??
                new DocumentProcessingOptions(),
            NullLogger<ProcessDocumentHandler>.Instance);
    }

    private SqliteDocumentRepository CreateRepository()
    {
        return new SqliteDocumentRepository(
            CreateFactory(
                _connection),
            NullLogger<SqliteDocumentRepository>.Instance);
    }

    private SqliteWorkspaceRepository CreateWorkspaceRepository()
    {
        return new SqliteWorkspaceRepository(
            CreateFactory(
                _connection));
    }

    private SqliteDocumentProcessingStore CreateProcessingStore()
    {
        return new SqliteDocumentProcessingStore(
            CreateFactory(
                _connection),
            NullLogger<SqliteDocumentProcessingStore>.Instance);
    }

    private SqliteDocumentSearchStore CreateSearchStore()
    {
        return new SqliteDocumentSearchStore(
            CreateFactory(
                _connection),
            NullLogger<SqliteDocumentSearchStore>.Instance);
    }

    private static IDbContextFactory<DeskVaultDbContext> CreateFactory(
        SqliteConnection connection)
    {
        return new TestDbContextFactory(
            connection);
    }

    private DeskVaultDbContext CreateContext()
    {
        return CreateContext(
            _connection);
    }

    private static SqliteConnection CreateConnection(
        string databasePath)
    {
        var connection =
            new SqliteConnection(
                $"Data Source={databasePath};Pooling=False");

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
        CryptographicOperations.ZeroMemory(
            _encryptionKey);

        if (_connection.State !=
            System.Data.ConnectionState.Closed)
        {
            await _connection.CloseAsync();
        }

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
