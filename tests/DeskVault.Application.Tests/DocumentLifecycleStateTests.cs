using DeskVault.Domain.Documents;

namespace DeskVault.Application.Tests;

public sealed class DocumentLifecycleStateTests
{
    [Fact]
    public void Create_WhenDocumentIsCreated_InitializesIndependentLifecycleAndProcessingStates()
    {
        // Arrange
        Document document =
            CreateDocument();

        // Act
        DocumentLifecycleState lifecycleState =
            document.LifecycleState;

        DocumentProcessingState processingState =
            document.ProcessingState;

        // Assert
        Assert.Equal(
            DocumentLifecycleState.Active,
            lifecycleState);

        Assert.Equal(
            DocumentProcessingState.NeverProcessed,
            processingState);

        Assert.Equal(
            DocumentStatus.Imported,
            document.Status);

        Assert.Equal(
            0L,
            document.ProcessingGeneration);

        Assert.Equal(
            0L,
            document.LastSuccessfulProcessingGeneration);
    }

    [Fact]
    public void Archive_WhenDocumentIsActive_TransitionsLifecycleToArchivedWithoutChangingProcessingState()
    {
        // Arrange
        Document document =
            CreateDocument();

        // Act
        document.Archive();

        // Assert
        Assert.Equal(
            DocumentLifecycleState.Archived,
            document.LifecycleState);

        Assert.Equal(
            DocumentProcessingState.NeverProcessed,
            document.ProcessingState);

        Assert.Equal(
            DocumentStatus.Imported,
            document.Status);
    }

    [Fact]
    public void RestoreFromArchive_WhenDocumentIsArchived_TransitionsLifecycleToActive()
    {
        // Arrange
        Document document =
            CreateDocument();

        document.Archive();

        // Act
        document.RestoreFromArchive();

        // Assert
        Assert.Equal(
            DocumentLifecycleState.Active,
            document.LifecycleState);
    }

    [Fact]
    public void Archive_WhenDocumentIsAlreadyArchived_ThrowsInvalidOperationException()
    {
        // Arrange
        Document document =
            CreateDocument();

        document.Archive();

        // Act
        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    document.Archive());

        // Assert
        Assert.Contains(
            "cannot transition",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RestoreFromArchive_WhenDocumentIsActive_ThrowsInvalidOperationException()
    {
        // Arrange
        Document document =
            CreateDocument();

        // Act
        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    document.RestoreFromArchive());

        // Assert
        Assert.Contains(
            "cannot transition",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Delete_WhenDocumentIsActive_TransitionsLifecycleToDeleted()
    {
        // Arrange
        Document document =
            CreateDocument();

        // Act
        document.Delete();

        // Assert
        Assert.Equal(
            DocumentLifecycleState.Deleted,
            document.LifecycleState);
    }

    [Fact]
    public void Delete_WhenDocumentIsArchived_TransitionsLifecycleToDeleted()
    {
        // Arrange
        Document document =
            CreateDocument();

        document.Archive();

        // Act
        document.Delete();

        // Assert
        Assert.Equal(
            DocumentLifecycleState.Deleted,
            document.LifecycleState);
    }

    [Fact]
    public void Delete_WhenDocumentIsAlreadyDeleted_ThrowsInvalidOperationException()
    {
        // Arrange
        Document document =
            CreateDocument();

        document.Delete();

        // Act
        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    document.Delete());

        // Assert
        Assert.Contains(
            "cannot be deleted again",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BeginProcessing_WhenDocumentIsActive_ChangesOnlyProcessingState()
    {
        // Arrange
        Document document =
            CreateDocument();

        // Act
        document.BeginProcessing();

        // Assert
        Assert.Equal(
            DocumentLifecycleState.Active,
            document.LifecycleState);

        Assert.Equal(
            DocumentProcessingState.Processing,
            document.ProcessingState);

        Assert.Equal(
            DocumentStatus.Imported,
            document.Status);
    }

    [Fact]
    public void MarkProcessingSucceeded_WhenProcessingIsInProgress_ChangesOnlyProcessingState()
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

        Assert.Equal(
            DocumentStatus.Imported,
            document.Status);
    }

    [Fact]
    public void MarkProcessingFailed_WhenProcessingIsInProgress_ChangesOnlyProcessingState()
    {
        // Arrange
        Document document =
            CreateDocument();

        document.BeginProcessing();

        // Act
        document.MarkProcessingFailed();

        // Assert
        Assert.Equal(
            DocumentLifecycleState.Active,
            document.LifecycleState);

        Assert.Equal(
            DocumentProcessingState.Failed,
            document.ProcessingState);

        Assert.Equal(
            DocumentStatus.Imported,
            document.Status);
    }

    [Fact]
    public void MarkProcessingCancelled_WhenProcessingIsInProgress_ChangesOnlyProcessingState()
    {
        // Arrange
        Document document =
            CreateDocument();

        document.BeginProcessing();

        // Act
        document.MarkProcessingCancelled();

        // Assert
        Assert.Equal(
            DocumentLifecycleState.Active,
            document.LifecycleState);

        Assert.Equal(
            DocumentProcessingState.Cancelled,
            document.ProcessingState);

        Assert.Equal(
            DocumentStatus.Imported,
            document.Status);
    }

    [Fact]
    public void BeginProcessing_WhenDocumentIsDeleted_ThrowsInvalidOperationException()
    {
        // Arrange
        Document document =
            CreateDocument();

        document.Delete();

        // Act
        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    document.BeginProcessing());

        // Assert
        Assert.Contains(
            "cannot be processed",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);

        Assert.Equal(
            DocumentLifecycleState.Deleted,
            document.LifecycleState);

        Assert.Equal(
            DocumentProcessingState.NeverProcessed,
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
    public void LifecycleTransition_DoesNotChangeKnowledgeAvailability()
    {
        // Arrange
        Document document =
            CreateDocument();

        DocumentKnowledgeAvailability beforeArchive =
            document.GetKnowledgeAvailability(
                DocumentKnowledgeRepresentationKind.KeywordSearch);

        // Act
        document.Archive();

        DocumentKnowledgeAvailability afterArchive =
            document.GetKnowledgeAvailability(
                DocumentKnowledgeRepresentationKind.KeywordSearch);

        // Assert
        Assert.Equal(
            beforeArchive.Representation,
            afterArchive.Representation);

        Assert.Equal(
            beforeArchive.State,
            afterArchive.State);

        Assert.Equal(
            beforeArchive.LastAvailableProcessingGeneration,
            afterArchive.LastAvailableProcessingGeneration);
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
