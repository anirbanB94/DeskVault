using DeskVault.Application.Documents.Chunking;
using DeskVault.Application.Documents.Commands.ProcessDocument;
using DeskVault.Application.Documents.Extraction;
using DeskVault.Application.Documents.Normalization;
using DeskVault.Application.Documents.Processing;
using DeskVault.Application.Interfaces;
using DeskVault.Domain.Documents;

namespace DeskVault.Application.Tests;

public sealed class ProcessDocumentHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenDocumentDoesNotExist_ReturnsNotFound()
    {
        var repository =
            new TestDocumentRepository();

        var reader =
            new TestDocumentReader();

        var extractor =
            new TestDocumentTextExtractor();

        var resolver =
            new DocumentTextExtractorResolver(
                [extractor]);

        var normalizer =
            new DocumentTextNormalizer();

        var chunker =
            new DocumentTextChunker(
                maxChunkSize: 100);

        var processingStore =
            new TestDocumentProcessingStore();

        var handler =
            new ProcessDocumentHandler(
                repository,
                reader,
                resolver,
                normalizer,
                chunker,
                processingStore);

        ProcessDocumentResult result =
            await handler.HandleAsync(
                new ProcessDocumentCommand(
                    Guid.NewGuid()));

        Assert.Equal(
            ProcessDocumentResultStatus.NotFound,
            result.Status);

        Assert.Null(
            result.DocumentId);

        Assert.Empty(
            processingStore.ReplacedChunks);

        Assert.False(
            reader.WasOpened);

        Assert.False(
            extractor.WasCalled);
    }

    [Fact]
    public async Task HandleAsync_WhenDocumentExists_ProcessesAndStoresChunks()
    {
        Document document =
            Document.Create(
                Guid.NewGuid(),
                "document.txt",
                "Test Document",
                "sha256-test-hash",
                "document.dvault");

        var repository =
            new TestDocumentRepository(document);

        var reader =
            new TestDocumentReader();

        var extractor =
            new TestDocumentTextExtractor();

        var resolver =
            new DocumentTextExtractorResolver(
                [extractor]);

        var normalizer =
            new DocumentTextNormalizer();

        var chunker =
            new DocumentTextChunker(
                maxChunkSize: 100);

        var processingStore =
            new TestDocumentProcessingStore();

        var handler =
            new ProcessDocumentHandler(
                repository,
                reader,
                resolver,
                normalizer,
                chunker,
                processingStore);

        ProcessDocumentResult result =
            await handler.HandleAsync(
                new ProcessDocumentCommand(
                    document.Id));

        Assert.Equal(
            ProcessDocumentResultStatus.Success,
            result.Status);

        Assert.Equal(
            document.Id,
            result.DocumentId);

        Assert.True(
            reader.WasOpened);

        Assert.True(
            extractor.WasCalled);

        Assert.Equal(
            document.FileName,
            extractor.FileName);

        Assert.Equal(
            1,
            processingStore.ReplaceCallCount);

        Assert.Equal(
            document.Id,
            processingStore.DocumentId);

        Assert.Single(
            processingStore.ReplacedChunks);

        Assert.Equal(
            0,
            processingStore.ReplacedChunks[0].Order);

        Assert.Equal(
            "First paragraph.\n\nSecond paragraph.",
            processingStore.ReplacedChunks[0].Text);
    }

    [Fact]
    public async Task HandleAsync_WhenProcessingProducesMultipleChunks_PreservesChunkOrder()
    {
        Document document =
            Document.Create(
                Guid.NewGuid(),
                "document.txt",
                "Test Document",
                "sha256-test-hash",
                "document.dvault");

        var repository =
            new TestDocumentRepository(document);

        var reader =
            new TestDocumentReader();

        var extractor =
            new TestDocumentTextExtractor();

        var resolver =
            new DocumentTextExtractorResolver(
                [extractor]);

        var normalizer =
            new DocumentTextNormalizer();

        var chunker =
            new DocumentTextChunker(
                maxChunkSize: 17);

        var processingStore =
            new TestDocumentProcessingStore();

        var handler =
            new ProcessDocumentHandler(
                repository,
                reader,
                resolver,
                normalizer,
                chunker,
                processingStore);

        ProcessDocumentResult result =
            await handler.HandleAsync(
                new ProcessDocumentCommand(
                    document.Id));

        Assert.Equal(
            ProcessDocumentResultStatus.Success,
            result.Status);

        Assert.Equal(
            2,
            processingStore.ReplacedChunks.Count);

        Assert.Equal(
            0,
            processingStore.ReplacedChunks[0].Order);

        Assert.Equal(
            1,
            processingStore.ReplacedChunks[1].Order);

        Assert.Equal(
            "First paragraph.",
            processingStore.ReplacedChunks[0].Text);

        Assert.Equal(
            "Second paragraph.",
            processingStore.ReplacedChunks[1].Text);
    }

    [Fact]
    public async Task HandleAsync_WhenCancellationIsRequestedBeforeProcessing_ThrowsOperationCanceledException()
    {
        var repository =
            new TestDocumentRepository();

        var reader =
            new TestDocumentReader();

        var extractor =
            new TestDocumentTextExtractor();

        var resolver =
            new DocumentTextExtractorResolver(
                [extractor]);

        var normalizer =
            new DocumentTextNormalizer();

        var chunker =
            new DocumentTextChunker(
                maxChunkSize: 100);

        var processingStore =
            new TestDocumentProcessingStore();

        var handler =
            new ProcessDocumentHandler(
                repository,
                reader,
                resolver,
                normalizer,
                chunker,
                processingStore);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                handler.HandleAsync(
                    new ProcessDocumentCommand(
                        Guid.NewGuid()),
                    cancellationTokenSource.Token));

        Assert.False(
            reader.WasOpened);

        Assert.False(
            extractor.WasCalled);

        Assert.Equal(
            0,
            processingStore.ReplaceCallCount);
    }


    [Fact]
    public async Task ProcessAsync_WhenDocumentExists_CompletesSuccessfully()
    {
        Document document =
            Document.Create(
                Guid.NewGuid(),
                "document.txt",
                "Test Document",
                "sha256-test-hash",
                "document.dvault");

        var repository =
            new TestDocumentRepository(document);

        var reader =
            new TestDocumentReader();

        var extractor =
            new TestDocumentTextExtractor();

        var resolver =
            new DocumentTextExtractorResolver(
                [extractor]);

        var normalizer =
            new DocumentTextNormalizer();

        var chunker =
            new DocumentTextChunker(
                maxChunkSize: 100);

        var processingStore =
            new TestDocumentProcessingStore();

        var handler =
            new ProcessDocumentHandler(
                repository,
                reader,
                resolver,
                normalizer,
                chunker,
                processingStore);

        var service =
            new DocumentProcessingService(
                handler);

        await service.ProcessAsync(
            document.Id);

        Assert.Equal(
            document.Id,
            processingStore.DocumentId);

        Assert.Equal(
            1,
            processingStore.ReplaceCallCount);

        Assert.Single(
            processingStore.ReplacedChunks);
    }

    [Fact]
    public async Task ProcessAsync_WhenDocumentDoesNotExist_ThrowsFileNotFoundException()
    {
        var repository =
            new TestDocumentRepository();

        var reader =
            new TestDocumentReader();

        var extractor =
            new TestDocumentTextExtractor();

        var resolver =
            new DocumentTextExtractorResolver(
                [extractor]);

        var normalizer =
            new DocumentTextNormalizer();

        var chunker =
            new DocumentTextChunker(
                maxChunkSize: 100);

        var processingStore =
            new TestDocumentProcessingStore();

        var handler =
            new ProcessDocumentHandler(
                repository,
                reader,
                resolver,
                normalizer,
                chunker,
                processingStore);

        var service =
            new DocumentProcessingService(
                handler);

        await Assert.ThrowsAsync<FileNotFoundException>(
            () =>
                service.ProcessAsync(
                    Guid.NewGuid()));

        Assert.Equal(
            0,
            processingStore.ReplaceCallCount);

        Assert.False(
            reader.WasOpened);

        Assert.False(
            extractor.WasCalled);
    }

    private sealed class TestDocumentRepository
        : IDocumentRepository
    {
        private readonly Document? _document;

        public TestDocumentRepository(
            Document? document = null)
        {
            _document = document;
        }

        public Task<bool> ExistsByHashAsync(
            string sha256Hash,
            CancellationToken cancellationToken = default)
        {
            bool exists =
                _document?.Sha256Hash == sha256Hash;

            return Task.FromResult(exists);
        }

        public Task AddAsync(
            Document document,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<Document?> GetByIdAsync(
            Guid documentId,
            CancellationToken cancellationToken = default)
        {
            Document? result =
                _document?.Id == documentId
                    ? _document
                    : null;

            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<Document>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<Document> documents =
                _document is null
                    ? []
                    : [_document];

            return Task.FromResult(
                documents);
        }

        public Task DeleteAsync(
            Guid documentId,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class TestDocumentReader
        : IDocumentReader
    {
        public bool WasOpened { get; private set; }

        public Task<Stream> OpenReadAsync(
            string storedFilePath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            WasOpened = true;

            Stream stream =
                new MemoryStream();

            return Task.FromResult(
                stream);
        }
    }

    private sealed class TestDocumentTextExtractor
        : IDocumentTextExtractor
    {
        public bool WasCalled { get; private set; }

        public string? FileName { get; private set; }

        public bool CanExtract(
            string fileName)
        {
            return fileName.EndsWith(
                ".txt",
                StringComparison.OrdinalIgnoreCase);
        }

        public Task<DocumentTextExtractionResult> ExtractAsync(
            Stream documentStream,
            string fileName,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            WasCalled = true;
            FileName = fileName;

            return Task.FromResult(
                new DocumentTextExtractionResult(
                    "First paragraph.\n\nSecond paragraph."));
        }
    }

    private sealed class TestDocumentProcessingStore
        : IDocumentProcessingStore
    {
        public Guid DocumentId { get; private set; }

        public int ReplaceCallCount { get; private set; }

        public IReadOnlyList<DocumentChunk> ReplacedChunks { get; private set; } =
            [];

        public Task ReplaceChunksAsync(
            Guid documentId,
            IReadOnlyList<DocumentChunk> chunks,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            DocumentId = documentId;
            ReplaceCallCount++;
            ReplacedChunks = chunks.ToList();

            return Task.CompletedTask;
        }
    }
}
