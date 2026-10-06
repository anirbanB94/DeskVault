using DeskVault.Domain.Documents;

namespace DeskVault.Application.Tests;

public sealed class DocumentProcessingStateTests
{
    [Fact]
    public void Create_WhenDocumentIsCreated_InitializesNeverProcessedState()
    {
        // Arrange
        Document document =
            CreateDocument();

        // Act
        DocumentProcessingState processingState =
            document.ProcessingState;

        // Assert
        Assert.Equal(
            DocumentProcessingState.NeverProcessed,
            processingState);
    }

    [Fact]
    public void BeginProcessing_WhenProcessingHasNotStarted_TransitionsToProcessing()
    {
        // Arrange
        Document document =
            CreateDocument();

        // Act
        document.BeginProcessing();

        // Assert
        Assert.Equal(
            DocumentProcessingState.Processing,
            document.ProcessingState);
    }

    [Fact]
    public void MarkProcessingSucceeded_WhenProcessingIsInProgress_TransitionsToSucceeded()
    {
        // Arrange
        Document document =
            CreateDocument();

        document.BeginProcessing();

        // Act
        document.MarkProcessingSucceeded();

        // Assert
        Assert.Equal(
            DocumentProcessingState.Succeeded,
            document.ProcessingState);
    }

    [Fact]
    public void MarkProcessingFailed_WhenProcessingIsInProgress_TransitionsToFailed()
    {
        // Arrange
        Document document =
            CreateDocument();

        document.BeginProcessing();

        // Act
        document.MarkProcessingFailed();

        // Assert
        Assert.Equal(
            DocumentProcessingState.Failed,
            document.ProcessingState);
    }

    [Fact]
    public void MarkProcessingCancelled_WhenProcessingIsInProgress_TransitionsToCancelled()
    {
        // Arrange
        Document document =
            CreateDocument();

        document.BeginProcessing();

        // Act
        document.MarkProcessingCancelled();

        // Assert
        Assert.Equal(
            DocumentProcessingState.Cancelled,
            document.ProcessingState);
    }

    [Fact]
    public void MarkProcessingSucceeded_WhenProcessingHasNotStarted_ThrowsInvalidOperationException()
    {
        // Arrange
        Document document =
            CreateDocument();

        // Act
        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    document.MarkProcessingSucceeded());

        // Assert
        Assert.Contains(
            "must be in progress",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);

        Assert.Equal(
            DocumentProcessingState.NeverProcessed,
            document.ProcessingState);
    }

    [Fact]
    public void MarkProcessingFailed_WhenProcessingHasNotStarted_ThrowsInvalidOperationException()
    {
        // Arrange
        Document document =
            CreateDocument();

        // Act
        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    document.MarkProcessingFailed());

        // Assert
        Assert.Contains(
            "must be in progress",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);

        Assert.Equal(
            DocumentProcessingState.NeverProcessed,
            document.ProcessingState);
    }

    [Fact]
    public void MarkProcessingCancelled_WhenProcessingHasNotStarted_ThrowsInvalidOperationException()
    {
        // Arrange
        Document document =
            CreateDocument();

        // Act
        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    document.MarkProcessingCancelled());

        // Assert
        Assert.Contains(
            "must be in progress",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);

        Assert.Equal(
            DocumentProcessingState.NeverProcessed,
            document.ProcessingState);
    }

    [Fact]
    public void ProcessingOutcome_WhenChanged_DoesNotChangeLifecycleState()
    {
        // Arrange
        Document document =
            CreateDocument();

        document.BeginProcessing();

        // Act
        document.MarkProcessingSucceeded();

        // Assert
        Assert.Equal(
            DocumentLifecycleState.Active,
            document.LifecycleState);

        Assert.Equal(
            DocumentProcessingState.Succeeded,
            document.ProcessingState);
    }

    [Fact]
    public void ProcessingOutcome_WhenChanged_DoesNotChangeKnowledgeAvailability()
    {
        // Arrange
        Document document =
            CreateDocument();

        DocumentKnowledgeAvailability beforeProcessing =
            document.GetKnowledgeAvailability(
                DocumentKnowledgeRepresentationKind.KeywordSearch);

        document.BeginProcessing();

        // Act
        document.MarkProcessingSucceeded();

        DocumentKnowledgeAvailability afterProcessing =
            document.GetKnowledgeAvailability(
                DocumentKnowledgeRepresentationKind.KeywordSearch);

        // Assert
        Assert.Equal(
            beforeProcessing.Representation,
            afterProcessing.Representation);

        Assert.Equal(
            beforeProcessing.State,
            afterProcessing.State);

        Assert.Equal(
            beforeProcessing.LastAvailableProcessingGeneration,
            afterProcessing.LastAvailableProcessingGeneration);
    }

    [Fact]
    public void BeginProcessing_WhenDocumentIsArchived_StillChangesProcessingState()
    {
        // Arrange
        Document document =
            CreateDocument();

        document.Archive();

        // Act
        document.BeginProcessing();

        // Assert
        Assert.Equal(
            DocumentLifecycleState.Archived,
            document.LifecycleState);

        Assert.Equal(
            DocumentProcessingState.Processing,
            document.ProcessingState);
    }

    [Fact]
    public void BeginProcessing_WhenCalledAfterSuccessfulProcessing_StartsANewProcessingState()
    {
        // Arrange
        Document document =
            CreateDocument();

        document.BeginProcessing();
        document.MarkProcessingSucceeded();

        // Act
        document.BeginProcessing();

        // Assert
        Assert.Equal(
            DocumentProcessingState.Processing,
            document.ProcessingState);
    }

    private static Document CreateDocument()
    {
        return Document.Create(
            Guid.NewGuid(),
            "document.txt",
            "Test Document",
            $"hash-{Guid.NewGuid():N}",
            "document.dvault");
    }
}
