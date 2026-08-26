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

        var statusHistory =
            new List<DocumentStatus>();

        repository
            .Setup(x => x.UpdateAsync(
                It.IsAny<Document>(),
                It.IsAny<CancellationToken>()))
            .Callback<Document, CancellationToken>(
                (updatedDocument, _) =>
                    statusHistory.Add(
                        updatedDocument.Status))
            .Returns(Task.CompletedTask);

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

        Assert.True(
            processingContext.Reader.WasOpened);

        Assert.True(
            processingContext.Extractor.WasCalled);

        Assert.Equal(
            document.FileName,
            processingContext.Extractor.FileName);

        Assert.Equal(
            1,
            processingContext.ProcessingStore.ReplaceCallCount);

        Assert.Equal(
            document.Id,
            processingContext.ProcessingStore.DocumentId);

        Assert.Single(
            processingContext.ProcessingStore.ReplacedChunks);

        Assert.Equal(
            0,
            processingContext.ProcessingStore.ReplacedChunks[0].Order);

        Assert.Equal(
            "First paragraph.\n\nSecond paragraph.",
            processingContext.ProcessingStore.ReplacedChunks[0].Text);

        Assert.Equal(
            [
                DocumentStatus.Processing,
                DocumentStatus.Indexed,
                DocumentStatus.Available
            ],
            statusHistory);

        repository.Verify(
            x => x.UpdateAsync(
                It.IsAny<Document>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(3));

        Assert.Equal(
            DocumentStatus.Available,
            document.Status);
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

        var statusHistory =
            CreateStatusHistory(
                repository);

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

        AssertProcessingFailureLifecycle(
            document,
            statusHistory,
            processingContext,
            repository);
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

        var statusHistory =
            CreateStatusHistory(
                repository);

        await Assert.ThrowsAsync<NotSupportedException>(
            () =>
                processingContext.Handler.HandleAsync(
                    new ProcessDocumentCommand(
                        document.Id)));

        Assert.False(
            processingContext.Reader.WasOpened);

        Assert.False(
            processingContext.Extractor.WasCalled);

        AssertProcessingFailureLifecycle(
            document,
            statusHistory,
            processingContext,
            repository);
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
            1,
            processingContext.ProcessingStore.ReplaceCallCount);

        Assert.Single(
            processingContext.ProcessingStore.ReplacedChunks);

        repository.Verify(
            x => x.UpdateAsync(
                It.IsAny<Document>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(3));

        Assert.Equal(
            DocumentStatus.Available,
            document.Status);
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

    private static List<DocumentStatus> CreateStatusHistory(
        Mock<IDocumentRepository> repository)
    {
        var statusHistory =
            new List<DocumentStatus>();

        repository
            .Setup(x => x.UpdateAsync(
                It.IsAny<Document>(),
                It.IsAny<CancellationToken>()))
            .Callback<Document, CancellationToken>(
                (updatedDocument, _) =>
                    statusHistory.Add(
                        updatedDocument.Status))
            .Returns(Task.CompletedTask);

        return statusHistory;
    }

    private static void AssertProcessingFailureLifecycle(
        Document document,
        IReadOnlyList<DocumentStatus> statusHistory,
        ProcessingContext processingContext,
        Mock<IDocumentRepository> repository)
    {
        Assert.Equal(
            [
                DocumentStatus.Processing,
                DocumentStatus.Failed
            ],
            statusHistory);

        Assert.Equal(
            0,
            processingContext.ProcessingStore.ReplaceCallCount);

        repository.Verify(
            x => x.UpdateAsync(
                It.IsAny<Document>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));

        Assert.Equal(
            DocumentStatus.Failed,
            document.Status);
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
