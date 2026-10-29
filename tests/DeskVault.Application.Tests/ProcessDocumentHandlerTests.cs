using DeskVault.Application.Configurations;
using DeskVault.Application.Documents.Chunking;
using DeskVault.Application.Documents.Commands.ProcessDocument;
using DeskVault.Application.Documents.Content;
using DeskVault.Application.Documents.Extraction;
using DeskVault.Application.Documents.Normalization;
using DeskVault.Application.Documents.Processing;
using DeskVault.Application.Documents.Provenance;
using DeskVault.Application.Interfaces;
using DeskVault.Application.Tests.TestDoubles;
using DeskVault.Domain.Documents;
using DeskVault.Shared.Resources;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using static DeskVault.Application.Tests.TestDoubles.SafeDiagnosticLogAssertions;

namespace DeskVault.Application.Tests;

public sealed class ProcessDocumentHandlerTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenMaximumDecryptedDocumentBytesIsNotPositive_ThrowsArgumentOutOfRangeException(
        long maximumDecryptedDocumentBytes)
    {
        var repository =
            new Mock<IDocumentRepository>();

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

        var processingOptions =
            new DocumentProcessingOptions
            {
                MaxDecryptedDocumentBytes =
                    maximumDecryptedDocumentBytes
            };

        ArgumentOutOfRangeException exception =
            Assert.Throws<ArgumentOutOfRangeException>(
                () =>
                    new ProcessDocumentHandler(
                        repository.Object,
                        reader,
                        resolver,
                        normalizer,
                        chunker,
                        processingStore,
                        processingOptions,
                        NullLogger<ProcessDocumentHandler>.Instance));

        Assert.Equal(
            "processingOptions",
            exception.ParamName);

        Assert.Contains(
            "Maximum decrypted document bytes must be greater than zero.",
            exception.Message);
    }

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
            await processingContext.Handler.HandleAsync(new ProcessDocumentCommand(
                    Guid.NewGuid()), TestContext.Current.CancellationToken);

        Assert.Equal(
            ProcessDocumentResultStatus.NotFound,
            result.Status);

        Assert.Null(result.DocumentId);

        Assert.Empty(
            processingContext.ProcessingStore.SuccessfulProcessingChunks);

        Assert.Equal(
            0,
            processingContext.ProcessingStore.SuccessfulProcessingCallCount);

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
    public async Task HandleAsync_WhenDocumentExists_ProcessesAndPublishesProcessingState()
    {
        // Arrange
        Document document = CreateDocument();

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetByIdAsync(
                document.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        const long maxDecryptedDocumentBytes = 1024;

        var processingContext =
            CreateProcessingContext(
                repository,
                maxChunkSize: 100,
                maxDecryptedDocumentBytes);

        // Act
        ProcessDocumentResult result =
            await processingContext.Handler.HandleAsync(new ProcessDocumentCommand(
                    document.Id), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            ProcessDocumentResultStatus.Success,
            result.Status);

        Assert.Equal(
            document.Id,
            result.DocumentId);

        DocumentProcessingRuleVersion expectedProcessingRuleVersion =
            DocumentRuleVersionFactory.CreateProcessingRuleVersion(
                processingContext.Extractor.RuleVersion,
                processingContext.Normalizer.RuleVersion);

        Assert.Equal(
            expectedProcessingRuleVersion,
            result.ProcessingRuleVersion);

        Assert.Equal(
            expectedProcessingRuleVersion,
            processingContext.ProcessingStore.SuccessfulProcessingRuleVersion);

        Assert.Equal(
            processingContext.Chunker.RuleVersion,
            result.ChunkingRuleVersion);

        Assert.Equal(
            processingContext.Chunker.RuleVersion,
            processingContext.ProcessingStore
                .SuccessfulProcessingChunks[0]
                .ChunkingRuleVersion);

        Assert.Equal(
            1L,
            processingContext.ProcessingStore.AcquiredGeneration);

        Assert.Equal(
            1,
            processingContext.ProcessingStore.SuccessfulProcessingCallCount);

        Assert.Single(
            processingContext.ProcessingStore.PublishedStates);

        Assert.Equal(
            DocumentProcessingState.Processing,
            processingContext.ProcessingStore.PublishedStates[0].State);

        Assert.Equal(
            1L,
            processingContext.ProcessingStore.PublishedStates[0]
                .ProcessingGeneration);

        Assert.True(
            processingContext.ProcessingStore.WasSuccessfulProcessingPublished);

        Assert.Equal(
            1L,
            processingContext.ProcessingStore.SuccessfulProcessingGeneration);

        Assert.False(
            processingContext.ProcessingStore.WasCancellationRecoveryRequested);

        Assert.Single(
            processingContext.ProcessingStore.SuccessfulProcessingChunks);

        Assert.Equal(
            0,
            processingContext.ProcessingStore.SuccessfulProcessingChunks[0].Order);

        Assert.Equal(
            "First paragraph.\n\nSecond paragraph.",
            processingContext.ProcessingStore.SuccessfulProcessingChunks[0].Text);

        Assert.True(
            processingContext.Reader.WasOpened);

        Assert.Equal(
            document.Id,
            processingContext.Reader.OpenedDocumentId);

        Assert.Equal(
            maxDecryptedDocumentBytes,
            processingContext.Reader.MaximumPlaintextBytes);

        Assert.True(
            processingContext.Extractor.WasCalled);

        Assert.Equal(
            document.FileName,
            processingContext.Extractor.FileName);

        Assert.Equal(
            DocumentLifecycleState.Active,
            document.LifecycleState);

        Assert.Equal(
            DocumentProcessingState.Succeeded,
            document.ProcessingState);

        DocumentKnowledgeAvailability keywordSearchAvailability =
            document.GetKnowledgeAvailability(
                DocumentKnowledgeRepresentationKind.KeywordSearch);

        Assert.Equal(
            DocumentKnowledgeAvailabilityState.Unavailable,
            keywordSearchAvailability.State);
    }

    [Fact]
    public async Task HandleAsync_WhenExtractorProvidesStructuredContent_ChunksSearchableProjection()
    {
        // Arrange
        Document document =
            CreateDocument();

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetByIdAsync(
                document.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        DocumentContent content =
            CreateStructuredContent();

        var processingContext =
            CreateProcessingContext(
                repository,
                maxChunkSize: 500);

        processingContext.Extractor.ReturnText =
            "legacy text that should not be used";

        processingContext.Extractor.ReturnContent =
            content;

        // Act
        ProcessDocumentResult result =
            await processingContext.Handler.HandleAsync(new ProcessDocumentCommand(
                    document.Id), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            ProcessDocumentResultStatus.Success,
            result.Status);

        DocumentChunk chunk =
            Assert.Single(
                processingContext.ProcessingStore.SuccessfulProcessingChunks);

        Assert.Equal(
            0,
            chunk.Order);

        Assert.Equal(
            content.SearchableText,
            chunk.Text);
    }

    [Fact]
    public async Task HandleAsync_WhenProcessingFails_PublishesFailureStateAndRethrows()
    {
        // Arrange
        Document document = CreateDocument();

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetByIdAsync(
                document.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var logger = new RecordingLogger<ProcessDocumentHandler>();

        var processingContext =
            CreateProcessingContext(
                repository,
                logger: logger);

        processingContext.Extractor.ThrowOnExtract = true;
        processingContext.Extractor.ExceptionMessage =
            CreateSensitiveDetails();

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                processingContext.Handler.HandleAsync(new ProcessDocumentCommand(
                        document.Id), TestContext.Current.CancellationToken));

        // Assert
        Assert.True(
            processingContext.Reader.WasOpened);

        Assert.True(
            processingContext.Extractor.WasCalled);

        Assert.Equal(
            1L,
            processingContext.ProcessingStore.AcquiredGeneration);

        Assert.Equal(
            [
                DocumentProcessingState.Processing,
                DocumentProcessingState.Failed
            ],
            processingContext.ProcessingStore.PublishedStates
                .Select(x => x.State)
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
            processingContext.ProcessingStore.SuccessfulProcessingCallCount);

        Assert.Empty(
            processingContext.ProcessingStore.SuccessfulProcessingChunks);

        Assert.Equal(
            DocumentLifecycleState.Active,
            document.LifecycleState);

        Assert.Equal(
            DocumentProcessingState.Failed,
            document.ProcessingState);

        AssertSafeFailureLog(
            logger.Entries,
            LogLevel.Error,
            DiagnosticOperationCodes.DocumentProcessingFailure,
            LogMessages.DocumentProcessingFailed,
            nameof(InvalidOperationException));
    }

    [Fact]
    public async Task HandleAsync_WhenDocumentContentIntegrityFails_PublishesFailureStateWithoutExtractingOrPublishingContent()
    {
        // Arrange
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

        processingContext.Reader
            .ThrowDocumentContentIntegrityExceptionOnOpen = true;

        // Act
        await Assert.ThrowsAsync<DocumentContentIntegrityException>(
            () =>
                processingContext.Handler.HandleAsync(new ProcessDocumentCommand(
                        document.Id), TestContext.Current.CancellationToken));

        // Assert
        Assert.True(
            processingContext.Reader.WasOpened);

        Assert.Equal(
            document.Id,
            processingContext.Reader.OpenedDocumentId);

        Assert.False(
            processingContext.Extractor.WasCalled);

        Assert.Equal(
            1L,
            processingContext.ProcessingStore.AcquiredGeneration);

        Assert.Equal(
            [
                DocumentProcessingState.Processing,
            DocumentProcessingState.Failed
            ],
            processingContext.ProcessingStore.PublishedStates
                .Select(x => x.State)
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
            processingContext.ProcessingStore.SuccessfulProcessingCallCount);

        Assert.Empty(
            processingContext.ProcessingStore.SuccessfulProcessingChunks);

        Assert.Equal(
            DocumentLifecycleState.Active,
            document.LifecycleState);

        Assert.Equal(
            DocumentProcessingState.Failed,
            document.ProcessingState);
    }

    [Fact]
    public async Task HandleAsync_WhenResourceLimitIsExceeded_PublishesFailureStateAndRethrows()
    {
        // Arrange
        Document document = CreateDocument();

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetByIdAsync(
                document.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var logger = new RecordingLogger<ProcessDocumentHandler>();

        var processingContext =
            CreateProcessingContext(
                repository,
                maxDecryptedDocumentBytes: 100,
                logger: logger);

        processingContext.Reader.ThrowResourceLimitExceededOnOpen = true;

        // Act
        ResourceLimitExceededException exception =
            await Assert.ThrowsAsync<ResourceLimitExceededException>(
                () =>
                    processingContext.Handler.HandleAsync(new ProcessDocumentCommand(
                            document.Id), TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(
            100L,
            processingContext.Reader.MaximumPlaintextBytes);

        Assert.Equal(
            100L,
            exception.LimitBytes);

        Assert.Equal(
            101L,
            exception.AttemptedBytes);

        Assert.Equal(
            [
                DocumentProcessingState.Processing,
                DocumentProcessingState.Failed
            ],
            processingContext.ProcessingStore.PublishedStates
                .Select(x => x.State)
                .ToArray());

        Assert.All(
            processingContext.ProcessingStore.PublishedStates,
            publication =>
                Assert.Equal(
                    1L,
                    publication.ProcessingGeneration));

        AssertSafeFailureLog(
            logger.Entries,
            LogLevel.Error,
            DiagnosticOperationCodes.DocumentProcessingFailure,
            LogMessages.DocumentProcessingFailed,
            nameof(ResourceLimitExceededException));

        Assert.True(
            processingContext.Reader.WasOpened);

        Assert.False(
            processingContext.Extractor.WasCalled);

        Assert.False(
            processingContext.ProcessingStore.WasSuccessfulProcessingPublished);

        Assert.False(
            processingContext.ProcessingStore.WasCancellationRecoveryRequested);

        Assert.Equal(
            0,
            processingContext.ProcessingStore.SuccessfulProcessingCallCount);

        Assert.Empty(
            processingContext.ProcessingStore.SuccessfulProcessingChunks);

        Assert.Equal(
            DocumentLifecycleState.Active,
            document.LifecycleState);

        Assert.Equal(
            DocumentProcessingState.Failed,
            document.ProcessingState);
    }

    [Fact]
    public async Task HandleAsync_WhenNormalizedTextExceedsResourceLimit_PublishesFailureStateAndRethrows()
    {
        // Arrange
        Document document = CreateDocument();

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetByIdAsync(
                document.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var processingOptions =
            new DocumentProcessingOptions
            {
                MaxDecryptedDocumentBytes =
                    1024,
                MaxProcessedTextBytes =
                    5
            };

        var processingContext =
            CreateProcessingContext(
                repository,
                processingOptions: processingOptions);

        processingContext.Extractor.ReturnText =
            "First paragraph.";

        // Act
        ResourceLimitExceededException exception =
            await Assert.ThrowsAsync<ResourceLimitExceededException>(
                () =>
                    processingContext.Handler.HandleAsync(new ProcessDocumentCommand(
                            document.Id), TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(
            5L,
            exception.LimitBytes);

        Assert.True(
            exception.AttemptedBytes > exception.LimitBytes);

        Assert.True(
            processingContext.Reader.WasOpened);

        Assert.True(
            processingContext.Extractor.WasCalled);

        Assert.Equal(
            [
                DocumentProcessingState.Processing,
                DocumentProcessingState.Failed
            ],
            processingContext.ProcessingStore.PublishedStates
                .Select(x => x.State)
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
            processingContext.ProcessingStore.SuccessfulProcessingCallCount);

        Assert.Empty(
            processingContext.ProcessingStore.SuccessfulProcessingChunks);

        Assert.Equal(
            DocumentLifecycleState.Active,
            document.LifecycleState);

        Assert.Equal(
            DocumentProcessingState.Failed,
            document.ProcessingState);
    }

    [Fact]
    public async Task HandleAsync_WhenProcessedTextIsWithinResourceLimit_CompletesSuccessfully()
    {
        Document document = CreateDocument();

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetByIdAsync(
                document.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var processingOptions =
            new DocumentProcessingOptions
            {
                MaxDecryptedDocumentBytes =
                    1024,
                MaxProcessedTextBytes =
                    1000
            };

        var processingContext =
            CreateProcessingContext(
                repository,
                processingOptions: processingOptions);

        ProcessDocumentResult result =
            await processingContext.Handler.HandleAsync(new ProcessDocumentCommand(
                    document.Id), TestContext.Current.CancellationToken);

        Assert.Equal(
            ProcessDocumentResultStatus.Success,
            result.Status);

        Assert.Equal(
            document.Id,
            result.DocumentId);

        Assert.True(
            processingContext.ProcessingStore
                .WasSuccessfulProcessingPublished);

        Assert.Equal(
            1,
            processingContext.ProcessingStore
                .SuccessfulProcessingCallCount);

        Assert.Single(
            processingContext.ProcessingStore
                .SuccessfulProcessingChunks);

        Assert.Equal(
            DocumentLifecycleState.Active,
            document.LifecycleState);

        Assert.Equal(
            DocumentProcessingState.Succeeded,
            document.ProcessingState);

        DocumentKnowledgeAvailability keywordSearchAvailability =
            document.GetKnowledgeAvailability(
                DocumentKnowledgeRepresentationKind.KeywordSearch);

        Assert.Equal(
            DocumentKnowledgeAvailabilityState.Unavailable,
            keywordSearchAvailability.State);
    }

    [Fact]
    public async Task HandleAsync_WhenCallerIsCancelledBeforeFailurePublication_PublishesFailureStateAndRethrowsOriginalException()
    {
        // Arrange
        Document document = CreateDocument();

        var repository =
            new Mock<IDocumentRepository>();

        repository
            .Setup(x => x.GetByIdAsync(
                document.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        var processingContext =
            CreateProcessingContext(repository);

        processingContext.Extractor.BeforeThrow =
            cancellationTokenSource.Cancel;

        processingContext.Extractor.ThrowOnExtract = true;

        // Act
        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () =>
                    processingContext.Handler.HandleAsync(
                        new ProcessDocumentCommand(document.Id),
                        cancellationTokenSource.Token));

        // Assert
        Assert.Equal(
            "Test extraction failure.",
            exception.Message);

        Assert.Equal(
            [
                DocumentProcessingState.Processing,
                DocumentProcessingState.Failed
            ],
            processingContext.ProcessingStore.PublishedStates
                .Select(x => x.State)
                .ToArray());

        Assert.True(
            processingContext.ProcessingStore
                .FailurePublicationReceivedNonCancelledToken);

        Assert.False(
            processingContext.ProcessingStore
                .WasSuccessfulProcessingPublished);

        Assert.Equal(
            0,
            processingContext.ProcessingStore.SuccessfulProcessingCallCount);

        Assert.Empty(
            processingContext.ProcessingStore.SuccessfulProcessingChunks);

        Assert.Equal(
            DocumentLifecycleState.Active,
            document.LifecycleState);

        Assert.Equal(
            DocumentProcessingState.Failed,
            document.ProcessingState);
    }

    [Fact]
    public async Task HandleAsync_WhenNoExtractorSupportsDocument_PublishesFailureStateAndRethrows()
    {
        // Arrange
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

        // Act
        await Assert.ThrowsAsync<NotSupportedException>(
            () =>
                processingContext.Handler.HandleAsync(new ProcessDocumentCommand(
                        document.Id), TestContext.Current.CancellationToken));

        // Assert
        Assert.False(
            processingContext.Reader.WasOpened);

        Assert.False(
            processingContext.Extractor.WasCalled);

        Assert.Equal(
            1L,
            processingContext.ProcessingStore.AcquiredGeneration);

        Assert.Equal(
            [
                DocumentProcessingState.Processing,
                DocumentProcessingState.Failed
            ],
            processingContext.ProcessingStore.PublishedStates
                .Select(x => x.State)
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
            processingContext.ProcessingStore.SuccessfulProcessingCallCount);

        Assert.Empty(
            processingContext.ProcessingStore.SuccessfulProcessingChunks);

        Assert.Equal(
            DocumentLifecycleState.Active,
            document.LifecycleState);

        Assert.Equal(
            DocumentProcessingState.Failed,
            document.ProcessingState);
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
            await processingContext.Handler.HandleAsync(new ProcessDocumentCommand(
                    document.Id), TestContext.Current.CancellationToken);

        Assert.Equal(
            ProcessDocumentResultStatus.Success,
            result.Status);

        Assert.Equal(
            1L,
            processingContext.ProcessingStore.AcquiredGeneration);

        Assert.Equal(
            1,
            processingContext.ProcessingStore.SuccessfulProcessingCallCount);

        Assert.Equal(
            1L,
            processingContext.ProcessingStore.SuccessfulProcessingGeneration);

        Assert.Equal(
            2,
            processingContext.ProcessingStore.SuccessfulProcessingChunks.Count);

        Assert.Equal(
            0,
            processingContext.ProcessingStore.SuccessfulProcessingChunks[0].Order);

        Assert.Equal(
            1,
            processingContext.ProcessingStore.SuccessfulProcessingChunks[1].Order);

        Assert.Equal(
            "First paragraph.",
            processingContext.ProcessingStore.SuccessfulProcessingChunks[0].Text);

        Assert.Equal(
            "Second paragraph.",
            processingContext.ProcessingStore.SuccessfulProcessingChunks[1].Text);

        Assert.Equal(
            DocumentLifecycleState.Active,
            document.LifecycleState);

        Assert.Equal(
            DocumentProcessingState.Succeeded,
            document.ProcessingState);
    }

    [Fact]
    public async Task HandleAsync_WhenDirectTextSourceMappingIsAvailable_PropagatesSourceLocationToProcessedChunks()
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

        processingContext.Extractor.ReturnText =
            "First line.\nSecond line.\nThird line.";

        processingContext.Extractor.SourceLocationMappingKind =
            DocumentSourceLocationMappingKind.DirectText;

        ProcessDocumentResult result =
            await processingContext.Handler.HandleAsync(new ProcessDocumentCommand(
                    document.Id), TestContext.Current.CancellationToken);

        Assert.Equal(
            ProcessDocumentResultStatus.Success,
            result.Status);

        DocumentChunk chunk =
            Assert.Single(
                processingContext.ProcessingStore.SuccessfulProcessingChunks);

        Assert.Equal(
            "First line.\nSecond line.\nThird line.",
            chunk.Text);

        Assert.Equal(
            new DocumentSourceLocation(
                1,
                3),
            chunk.SourceLocation);

        Assert.Equal(
            DocumentProcessingState.Succeeded,
            document.ProcessingState);
    }

    [Fact]
    public async Task HandleAsync_WhenSourceLocationMappingIsUnknown_DoesNotAssignSourceLocation()
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

        processingContext.Extractor.ReturnText =
            "Generated representation.\nSecond line.";

        processingContext.Extractor.SourceLocationMappingKind =
            DocumentSourceLocationMappingKind.Unknown;

        ProcessDocumentResult result =
            await processingContext.Handler.HandleAsync(new ProcessDocumentCommand(
                    document.Id), TestContext.Current.CancellationToken);

        Assert.Equal(
            ProcessDocumentResultStatus.Success,
            result.Status);

        DocumentChunk chunk =
            Assert.Single(
                processingContext.ProcessingStore.SuccessfulProcessingChunks);

        Assert.Null(
            chunk.SourceLocation);

        Assert.Equal(
            DocumentProcessingState.Succeeded,
            document.ProcessingState);
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
            processingContext.ProcessingStore.SuccessfulProcessingCallCount);

        Assert.Empty(
            processingContext.ProcessingStore.SuccessfulProcessingChunks);

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
        // Arrange
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

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                processingContext.Handler.HandleAsync(
                    new ProcessDocumentCommand(
                        document.Id),
                    cancellationTokenSource.Token));

        // Assert
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
            DocumentProcessingState.Processing,
            processingContext.ProcessingStore.PublishedStates[0].State);

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
            processingContext.ProcessingStore.SuccessfulProcessingCallCount);

        Assert.Empty(
            processingContext.ProcessingStore.SuccessfulProcessingChunks);

        Assert.Equal(
            DocumentLifecycleState.Active,
            document.LifecycleState);

        Assert.Equal(
            DocumentProcessingState.Cancelled,
            document.ProcessingState);
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
            await processingContext.Handler.HandleAsync(new ProcessDocumentCommand(
                    document.Id), TestContext.Current.CancellationToken);

        ProcessDocumentResult secondResult =
            await processingContext.Handler.HandleAsync(new ProcessDocumentCommand(
                    document.Id), TestContext.Current.CancellationToken);

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
            processingContext.ProcessingStore.SuccessfulProcessingCallCount);

        Assert.Equal(
            2L,
            processingContext.ProcessingStore.SuccessfulProcessingGeneration);

        Assert.Equal(
            2,
            processingContext.ProcessingStore.PublishedStates.Count);

        Assert.Equal(
            [
                1L,
                2L
            ],
            processingContext.ProcessingStore.PublishedStates
                .Select(x => x.ProcessingGeneration)
                .ToArray());

        Assert.True(
            processingContext.ProcessingStore.WasSuccessfulProcessingPublished);

        Assert.Equal(
            0,
            processingContext.ProcessingStore.SuccessfulProcessingChunks[0].Order);

        Assert.Equal(
            "First paragraph.\n\nSecond paragraph.",
            processingContext.ProcessingStore.SuccessfulProcessingChunks[0].Text);

        Assert.Equal(
            DocumentLifecycleState.Active,
            document.LifecycleState);

        Assert.Equal(
            DocumentProcessingState.Succeeded,
            document.ProcessingState);
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

        await service.ProcessAsync(document.Id, TestContext.Current.CancellationToken);

        Assert.Equal(
            document.Id,
            processingContext.ProcessingStore.DocumentId);

        Assert.Equal(
            1L,
            processingContext.ProcessingStore.AcquiredGeneration);

        Assert.Equal(
            1,
            processingContext.ProcessingStore.SuccessfulProcessingCallCount);

        Assert.Equal(
            1L,
            processingContext.ProcessingStore.SuccessfulProcessingGeneration);

        Assert.Single(
            processingContext.ProcessingStore.SuccessfulProcessingChunks);

        Assert.True(
            processingContext.ProcessingStore.WasSuccessfulProcessingPublished);

        Assert.Equal(
            DocumentLifecycleState.Active,
            document.LifecycleState);

        Assert.Equal(
            DocumentProcessingState.Succeeded,
            document.ProcessingState);
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
                service.ProcessAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));

        Assert.Equal(
            0L,
            processingContext.ProcessingStore.AcquiredGeneration);

        Assert.Equal(
            0,
            processingContext.ProcessingStore.SuccessfulProcessingCallCount);

        Assert.Empty(
            processingContext.ProcessingStore.SuccessfulProcessingChunks);

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

    private static DocumentContent CreateStructuredContent()
    {
        return new DocumentContent(
        [
            new DocumentContentUnit(
            order: 0,
            kind: DocumentContentUnitKind.TableRow,
            fields:
            [
                new DocumentContentField("Id", "1001"),
                new DocumentContentField("Name", "Alice Johnson"),
                new DocumentContentField("Department", "Engineering")
            ])
        ]);
    }

    private static ProcessingContext CreateProcessingContext(
        Mock<IDocumentRepository> repository,
        int maxChunkSize = 100,
        long maxDecryptedDocumentBytes = 32 * 1024 * 1024,
        DocumentProcessingOptions? processingOptions = null,
        ILogger<ProcessDocumentHandler>? logger = null)
    {
        var reader =
            new TestDocumentReader();

        var extractor =
            new TestDocumentTextExtractor();

        var resolver =
            new DocumentTextExtractorResolver(
                [extractor]);

        var processingStore =
            new TestDocumentProcessingStore();

        processingOptions ??=
            new DocumentProcessingOptions
            {
                MaxDecryptedDocumentBytes =
                    maxDecryptedDocumentBytes
            };

        var normalizer =
            new DocumentTextNormalizer(
                processingOptions);

        var chunker =
            new DocumentTextChunker(
                maxChunkSize);

        var handler =
            new ProcessDocumentHandler(
                repository.Object,
                reader,
                resolver,
                normalizer,
                chunker,
                processingStore,
                processingOptions,
                logger ?? NullLogger<ProcessDocumentHandler>.Instance);

        return new ProcessingContext(
            handler,
            reader,
            extractor,
            normalizer,
            chunker,
            processingStore);
    }

    private sealed record ProcessingContext(
        ProcessDocumentHandler Handler,
        TestDocumentReader Reader,
        TestDocumentTextExtractor Extractor,
        DocumentTextNormalizer Normalizer,
        DocumentTextChunker Chunker,
        TestDocumentProcessingStore ProcessingStore);

    private sealed class TestDocumentReader
        : IDocumentReader
    {
        public bool WasOpened { get; private set; }

        public Guid? OpenedDocumentId { get; private set; }

        public long? MaximumPlaintextBytes { get; private set; }

        public bool ThrowResourceLimitExceededOnOpen { get; set; }

        public bool ThrowDocumentContentIntegrityExceptionOnOpen { get; set; }

        public Task<Stream> OpenReadAsync(
            Guid documentId,
            string expectedSha256Hash,
            long maximumPlaintextBytes,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            WasOpened = true;

            OpenedDocumentId = documentId;

            MaximumPlaintextBytes =
                maximumPlaintextBytes;

            if (ThrowResourceLimitExceededOnOpen)
            {
                long limitBytes =
                    maximumPlaintextBytes;

                throw new ResourceLimitExceededException(
                    limitBytes,
                    limitBytes + 1L);
            }

            if (ThrowDocumentContentIntegrityExceptionOnOpen)
            {
                throw new DocumentContentIntegrityException();
            }

            Stream stream =
                new MemoryStream();

            return Task.FromResult(stream);
        }

        public Task ValidateContentHashAsync(
            Guid documentId,
            string expectedSha256Hash,
            long maximumPlaintextBytes,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            throw new NotSupportedException(
                "Hash validation is not supported by this process-handler test double.");
        }
    }

    private sealed class TestDocumentTextExtractor
        : IDocumentTextExtractor
    {
        public bool WasCalled { get; private set; }

        public bool ThrowOnExtract { get; set; }

        public string ExceptionMessage { get; set; } = "Test extraction failure.";

        public bool CancelOnExtract { get; set; }

        public Action? BeforeThrow { get; set; }

        public string? FileName { get; private set; }

        public string ReturnText { get; set; } =
            "First paragraph.\n\nSecond paragraph.";

        public DocumentContent? ReturnContent { get; set; }

        public DocumentSourceLocationMappingKind SourceLocationMappingKind { get; set; } =
            DocumentSourceLocationMappingKind.Unknown;

        public string RuleVersion => "test-extractor-v1";

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
                BeforeThrow?.Invoke();

                throw new InvalidOperationException(
                    ExceptionMessage);
            }

            if (CancelOnExtract)
            {
                throw new OperationCanceledException(
                    cancellationToken);
            }

            return Task.FromResult(
                new DocumentTextExtractionResult(
                    ReturnText,
                    SourceLocationMappingKind,
                    ReturnContent));
        }
    }

    private sealed class TestDocumentProcessingStore
        : IDocumentProcessingStore
    {
        public Guid DocumentId { get; private set; }

        public int SuccessfulProcessingCallCount { get; private set; }

        public long AcquiredGeneration { get; private set; }

        public IReadOnlyList<DocumentChunk> SuccessfulProcessingChunks { get; private set; } =
            [];

        public List<StatePublication> PublishedStates { get; } =
            [];

        public bool WasSuccessfulProcessingPublished { get; private set; }

        public long SuccessfulProcessingGeneration { get; private set; }

        public bool WasCancellationRecoveryRequested { get; private set; }

        public bool FailurePublicationReceivedNonCancelledToken { get; private set; }

        public long CancellationRecoveryGeneration { get; private set; }

        public DocumentProcessingRuleVersion SuccessfulProcessingRuleVersion { get; private set; }

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
            DocumentProcessingState state,
            CancellationToken cancellationToken = default)
        {
            if (state == DocumentProcessingState.Failed &&
                !cancellationToken.IsCancellationRequested)
            {
                FailurePublicationReceivedNonCancelledToken = true;
            }

            cancellationToken.ThrowIfCancellationRequested();

            DocumentId = documentId;

            PublishedStates.Add(
                new StatePublication(
                    processingGeneration,
                    state));

            return Task.CompletedTask;
        }

        public Task PublishSuccessfulProcessingAsync(
            Guid documentId,
            long processingGeneration,
            DocumentProcessingRuleVersion processingRuleVersion,
            IReadOnlyList<DocumentChunk> chunks,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            DocumentId = documentId;

            SuccessfulProcessingGeneration =
                processingGeneration;

            SuccessfulProcessingRuleVersion =
                processingRuleVersion;

            SuccessfulProcessingChunks =
                chunks.ToList();

            SuccessfulProcessingCallCount++;

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

            return Task.CompletedTask;
        }

        public sealed record StatePublication(
            long ProcessingGeneration,
            DocumentProcessingState State);
    }
}
