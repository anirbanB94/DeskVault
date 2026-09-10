using DeskVault.Application.Documents.Chunking;
using DeskVault.Application.Documents.Commands.ProcessDocument;
using DeskVault.Application.Documents.Extraction;
using DeskVault.Application.Documents.Normalization;
using DeskVault.Application.Documents.Processing;
using DeskVault.Application.Interfaces;
using DeskVault.Domain.Documents;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DeskVault.Application.Tests;

public sealed class ProcessDocumentHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenDocumentDoesNotExist_ReturnsNotFound()
    {
        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetByIdAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Document?)null);

        var processingContext =
            CreateProcessingContext(repository);

        ProcessDocumentResult result =
            await processingContext.Handler.HandleAsync(
                new ProcessDocumentCommand(
                    Guid.NewGuid()));

        Assert.Equal(
            ProcessDocumentResultStatus.NotFound,
            result.Status);

        Assert.Null(result.DocumentId);

        Assert.Empty(
            processingContext.ProcessingStore.ReplacedChunks);

        Assert.Equal(
            0L,
            processingContext.ProcessingStore.AcquiredGeneration);

        Assert.False(
            processingContext.Reader.WasOpened);

        Assert.False(
            processingContext.Extractor.WasCalled);

        repository.Verify(
            x => x.UpdateAsync(
                It.IsAny<Document>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenDocumentExists_ProcessesAndUpdatesStatusLifecycle()
    {
        Document document = CreateDocument();

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetByIdAsync(
                document.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var processingContext =
            CreateProcessingContext(
                repository,
                maxChunkSize: 100);

        ProcessDocumentResult result =
            await processingContext.Handler.HandleAsync(
                new ProcessDocumentCommand(
                    document.Id));

        Assert.Equal(
            ProcessDocumentResultStatus.Success,
            result.Status);

        Assert.Equal(
            document.Id,
            result.DocumentId);

        Assert.Equal(
            1L,
            processingContext.ProcessingStore.AcquiredGeneration);

        Assert.Equal(
            1L,
            processingContext.ProcessingStore.ReplacedGeneration);

        Assert.Equal(
            1,
            processingContext.ProcessingStore.ReplaceCallCount);

        Assert.Equal(
            2,
            processingContext.ProcessingStore.PublishedStates.Count);

        Assert.Equal(
            DocumentStatus.Processing,
            processingContext.ProcessingStore.PublishedStates[0].Status);

        Assert.Equal(
            DocumentStatus.Indexed,
            processingContext.ProcessingStore.PublishedStates[1].Status);

        Assert.All(
            processingContext.ProcessingStore.PublishedStates,
            publication =>
                Assert.Equal(
                    1L,
                    publication.ProcessingGeneration));

        Assert.True(
            processingContext.ProcessingStore.WasSuccessfulProcessingPublished);

        Assert.Equal(
            1L,
            processingContext.ProcessingStore.SuccessfulProcessingGeneration);

        Assert.False(
            processingContext.ProcessingStore.WasCancellationRecoveryRequested);

        Assert.Single(
            processingContext.ProcessingStore.ReplacedChunks);

        Assert.Equal(
            0,
            processingContext.ProcessingStore.ReplacedChunks[0].Order);

        Assert.Equal(
            "First paragraph.\n\nSecond paragraph.",
            processingContext.ProcessingStore.ReplacedChunks[0].Text);

        Assert.True(
            processingContext.Reader.WasOpened);

        Assert.True(
            processingContext.Extractor.WasCalled);

        Assert.Equal(
            document.FileName,
            processingContext.Extractor.FileName);
    }

    [Fact]
    public async Task HandleAsync_WhenProcessingFails_MarksDocumentAsFailedAndRethrows()
    {
        Document document = CreateDocument();

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetByIdAsync(
                document.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var processingContext =
            CreateProcessingContext(repository);

        processingContext.Extractor.ThrowOnExtract = true;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                processingContext.Handler.HandleAsync(
                    new ProcessDocumentCommand(
                        document.Id)));

        Assert.True(
            processingContext.Reader.WasOpened);

        Assert.True(
            processingContext.Extractor.WasCalled);

        Assert.Equal(
            1L,
            processingContext.ProcessingStore.AcquiredGeneration);

        Assert.Equal(
            [
                DocumentStatus.Processing,
                DocumentStatus.Failed
            ],
            processingContext.ProcessingStore.PublishedStates
                .Select(x => x.Status)
                .ToArray());

        Assert.All(
            processingContext.ProcessingStore.PublishedStates,
            publication =>
                Assert.Equal(
                    1L,
                    publication.ProcessingGeneration));

        Assert.False(
            processingContext.ProcessingStore.WasSuccessfulProcessingPublished);

        Assert.False(
            processingContext.ProcessingStore.WasCancellationRecoveryRequested);

        Assert.Equal(
            0,
            processingContext.ProcessingStore.ReplaceCallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenNoExtractorSupportsDocument_MarksDocumentAsFailedAndRethrows()
    {
        Document document =
            Document.Create(
                Guid.NewGuid(),
                "document.pdf",
                "Unsupported Document",
                "sha256-test-hash",
                "document.dvault");

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetByIdAsync(
                document.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var processingContext =
            CreateProcessingContext(repository);

        await Assert.ThrowsAsync<NotSupportedException>(
            () =>
                processingContext.Handler.HandleAsync(
                    new ProcessDocumentCommand(
                        document.Id)));

        Assert.False(
            processingContext.Reader.WasOpened);

        Assert.False(
            processingContext.Extractor.WasCalled);

        Assert.Equal(
            1L,
            processingContext.ProcessingStore.AcquiredGeneration);

        Assert.Equal(
            [
                DocumentStatus.Processing,
                DocumentStatus.Failed
            ],
            processingContext.ProcessingStore.PublishedStates
                .Select(x => x.Status)
                .ToArray());

        Assert.All(
            processingContext.ProcessingStore.PublishedStates,
            publication =>
                Assert.Equal(
                    1L,
                    publication.ProcessingGeneration));

        Assert.False(
            processingContext.ProcessingStore.WasSuccessfulProcessingPublished);

        Assert.False(
            processingContext.ProcessingStore.WasCancellationRecoveryRequested);

        Assert.Equal(
            0,
            processingContext.ProcessingStore.ReplaceCallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenProcessingProducesMultipleChunks_PreservesChunkOrder()
    {
        Document document = CreateDocument();

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetByIdAsync(
                document.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var processingContext =
            CreateProcessingContext(
                repository,
                maxChunkSize: 17);

        ProcessDocumentResult result =
            await processingContext.Handler.HandleAsync(
                new ProcessDocumentCommand(
                    document.Id));

        Assert.Equal(
            ProcessDocumentResultStatus.Success,
            result.Status);

        Assert.Equal(
            1L,
            processingContext.ProcessingStore.AcquiredGeneration);

        Assert.Equal(
            1L,
            processingContext.ProcessingStore.ReplacedGeneration);

        Assert.Equal(
            2,
            processingContext.ProcessingStore.ReplacedChunks.Count);

        Assert.Equal(
            0,
            processingContext.ProcessingStore.ReplacedChunks[0].Order);

        Assert.Equal(
            1,
            processingContext.ProcessingStore.ReplacedChunks[1].Order);

        Assert.Equal(
            "First paragraph.",
            processingContext.ProcessingStore.ReplacedChunks[0].Text);

        Assert.Equal(
            "Second paragraph.",
            processingContext.ProcessingStore.ReplacedChunks[1].Text);
    }

    [Fact]
    public async Task HandleAsync_WhenCancellationIsRequestedBeforeProcessing_ThrowsOperationCanceledException()
    {
        var repository =
            new Mock<IDocumentRepository>();

        var processingContext =
            CreateProcessingContext(repository);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                processingContext.Handler.HandleAsync(
                    new ProcessDocumentCommand(
                        Guid.NewGuid()),
                    cancellationTokenSource.Token));

        Assert.False(
            processingContext.Reader.WasOpened);

        Assert.False(
            processingContext.Extractor.WasCalled);

        Assert.Equal(
            0L,
            processingContext.ProcessingStore.AcquiredGeneration);

        Assert.Empty(
            processingContext.ProcessingStore.PublishedStates);

        Assert.Equal(
            0,
            processingContext.ProcessingStore.ReplaceCallCount);

        repository.Verify(
            x => x.GetByIdAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        repository.Verify(
            x => x.UpdateAsync(
                It.IsAny<Document>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenCancellationOccursDuringExtraction_RethrowsAndRecoversProcessingState()
    {
        Document document = CreateDocument();

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetByIdAsync(
                document.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var processingContext =
            CreateProcessingContext(repository);

        processingContext.Extractor.CancelOnExtract = true;

        using var cancellationTokenSource =
            new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                processingContext.Handler.HandleAsync(
                    new ProcessDocumentCommand(
                        document.Id),
                    cancellationTokenSource.Token));

        Assert.True(
            processingContext.Reader.WasOpened);

        Assert.True(
            processingContext.Extractor.WasCalled);

        Assert.Equal(
            1L,
            processingContext.ProcessingStore.AcquiredGeneration);

        Assert.Single(
            processingContext.ProcessingStore.PublishedStates);

        Assert.Equal(
            DocumentStatus.Processing,
            processingContext.ProcessingStore.PublishedStates[0].Status);

        Assert.Equal(
            1L,
            processingContext.ProcessingStore.PublishedStates[0]
                .ProcessingGeneration);

        Assert.False(
            processingContext.ProcessingStore.WasSuccessfulProcessingPublished);

        Assert.True(
            processingContext.ProcessingStore.WasCancellationRecoveryRequested);

        Assert.Equal(
            1L,
            processingContext.ProcessingStore.CancellationRecoveryGeneration);

        Assert.Equal(
            0,
            processingContext.ProcessingStore.ReplaceCallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenProcessingSameDocumentTwice_ProducesDeterministicLifecycle()
    {
        Document document = CreateDocument();

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetByIdAsync(
                document.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var processingContext =
            CreateProcessingContext(repository);

        ProcessDocumentResult firstResult =
            await processingContext.Handler.HandleAsync(
                new ProcessDocumentCommand(
                    document.Id));

        ProcessDocumentResult secondResult =
            await processingContext.Handler.HandleAsync(
                new ProcessDocumentCommand(
                    document.Id));

        Assert.Equal(
            ProcessDocumentResultStatus.Success,
            firstResult.Status);

        Assert.Equal(
            ProcessDocumentResultStatus.Success,
            secondResult.Status);

        Assert.Equal(
            document.Id,
            firstResult.DocumentId);

        Assert.Equal(
            document.Id,
            secondResult.DocumentId);

        Assert.Equal(
            firstResult.Description,
            secondResult.Description);

        Assert.Equal(
            2,
            processingContext.ProcessingStore.ReplaceCallCount);

        Assert.Equal(
            2,
            processingContext.ProcessingStore.ReplacedGeneration);

        Assert.Equal(
            4,
            processingContext.ProcessingStore.PublishedStates.Count);

        Assert.Equal(
            [
                1L,
                1L,
                2L,
                2L
            ],
            processingContext.ProcessingStore.PublishedStates
                .Select(x => x.ProcessingGeneration)
                .ToArray());

        Assert.True(
            processingContext.ProcessingStore.WasSuccessfulProcessingPublished);

        Assert.Equal(
            2L,
            processingContext.ProcessingStore.SuccessfulProcessingGeneration);
    }

    [Fact]
    public async Task ProcessAsync_WhenDocumentExists_CompletesSuccessfully()
    {
        Document document = CreateDocument();

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetByIdAsync(
                document.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var processingContext =
            CreateProcessingContext(repository);

        var service =
            new DocumentProcessingService(
                processingContext.Handler);

        await service.ProcessAsync(
            document.Id);

        Assert.Equal(
            document.Id,
            processingContext.ProcessingStore.DocumentId);

        Assert.Equal(
            1L,
            processingContext.ProcessingStore.AcquiredGeneration);

        Assert.Equal(
            1,
            processingContext.ProcessingStore.ReplaceCallCount);

        Assert.Equal(
            1L,
            processingContext.ProcessingStore.ReplacedGeneration);

        Assert.Single(
            processingContext.ProcessingStore.ReplacedChunks);

        Assert.True(
            processingContext.ProcessingStore.WasSuccessfulProcessingPublished);

        Assert.Equal(
            1L,
            processingContext.ProcessingStore.SuccessfulProcessingGeneration);
    }

    [Fact]
    public async Task ProcessAsync_WhenDocumentDoesNotExist_ThrowsFileNotFoundException()
    {
        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetByIdAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Document?)null);

        var processingContext =
            CreateProcessingContext(repository);

        var service =
            new DocumentProcessingService(
                processingContext.Handler);

        await Assert.ThrowsAsync<FileNotFoundException>(
            () =>
                service.ProcessAsync(
                    Guid.NewGuid()));

        Assert.Equal(
            0L,
            processingContext.ProcessingStore.AcquiredGeneration);

        Assert.Equal(
            0,
            processingContext.ProcessingStore.ReplaceCallCount);

        Assert.False(
            processingContext.Reader.WasOpened);

        Assert.False(
            processingContext.Extractor.WasCalled);

        repository.Verify(
            x => x.UpdateAsync(
                It.IsAny<Document>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static Document CreateDocument()
    {
        return Document.Create(
            Guid.NewGuid(),
            "document.txt",
            "Test Document",
            "sha256-test-hash",
            "document.dvault");
    }

    private static ProcessingContext CreateProcessingContext(
        Mock<IDocumentRepository> repository,
        int maxChunkSize = 100)
    {
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
                maxChunkSize);

        var processingStore =
            new TestDocumentProcessingStore();

        var handler =
            new ProcessDocumentHandler(
                repository.Object,
                reader,
                resolver,
                normalizer,
                chunker,
                processingStore,
                NullLogger<ProcessDocumentHandler>.Instance);

        return new ProcessingContext(
            handler,
            reader,
            extractor,
            processingStore);
    }

    private sealed record ProcessingContext(
        ProcessDocumentHandler Handler,
        TestDocumentReader Reader,
        TestDocumentTextExtractor Extractor,
        TestDocumentProcessingStore ProcessingStore);

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

            return Task.FromResult(stream);
        }
    }

    private sealed class TestDocumentTextExtractor
        : IDocumentTextExtractor
    {
        public bool WasCalled { get; private set; }

        public bool ThrowOnExtract { get; set; }

        public bool CancelOnExtract { get; set; }

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

            if (ThrowOnExtract)
            {
                throw new InvalidOperationException(
                    "Test extraction failure.");
            }

            if (CancelOnExtract)
            {
                throw new OperationCanceledException(
                    cancellationToken);
            }

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

        public long AcquiredGeneration { get; private set; }

        public long ReplacedGeneration { get; private set; }

        public IReadOnlyList<DocumentChunk> ReplacedChunks { get; private set; } =
            [];

        public List<StatePublication> PublishedStates { get; } =
            [];

        public bool WasSuccessfulProcessingPublished { get; private set; }

        public long SuccessfulProcessingGeneration { get; private set; }

        public bool WasCancellationRecoveryRequested { get; private set; }

        public long CancellationRecoveryGeneration { get; private set; }

        public Task<long> AcquireProcessingGenerationAsync(
            Guid documentId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            DocumentId = documentId;
            AcquiredGeneration++;

            return Task.FromResult(
                AcquiredGeneration);
        }

        public Task PublishProcessingStateAsync(
            Guid documentId,
            long processingGeneration,
            DocumentStatus status,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            DocumentId = documentId;

            PublishedStates.Add(
                new StatePublication(
                    processingGeneration,
                    status));

            return Task.CompletedTask;
        }

        public Task PublishSuccessfulProcessingAsync(
            Guid documentId,
            long processingGeneration,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            DocumentId = documentId;
            SuccessfulProcessingGeneration =
                processingGeneration;
            WasSuccessfulProcessingPublished = true;

            return Task.CompletedTask;
        }

        public Task RecoverCancelledProcessingAsync(
            Guid documentId,
            long processingGeneration,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            DocumentId = documentId;
            CancellationRecoveryGeneration =
                processingGeneration;
            WasCancellationRecoveryRequested = true;

            return Task.CompletedTask;
        }

        public Task ReplaceChunksAsync(
            Guid documentId,
            long processingGeneration,
            IReadOnlyList<DocumentChunk> chunks,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            DocumentId = documentId;
            ReplacedGeneration = processingGeneration;
            ReplaceCallCount++;
            ReplacedChunks = chunks.ToList();

            return Task.CompletedTask;
        }

        public sealed record StatePublication(
            long ProcessingGeneration,
            DocumentStatus Status);
    }
}
