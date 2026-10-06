using DeskVault.Domain.Documents;

namespace DeskVault.Application.Tests;

public sealed class DocumentKnowledgeAvailabilityTests
{
    [Fact]
    public void Create_WhenDocumentIsCreated_DefinesKeywordSearchAsUnavailable()
    {
        // Arrange
        Document document =
            CreateDocument();

        // Act
        DocumentKnowledgeAvailability availability =
            document.GetKnowledgeAvailability(
                DocumentKnowledgeRepresentationKind.KeywordSearch);

        // Assert
        Assert.Equal(
            DocumentKnowledgeRepresentationKind.KeywordSearch,
            availability.Representation);

        Assert.Equal(
            DocumentKnowledgeAvailabilityState.Unavailable,
            availability.State);

        Assert.Null(
            availability.LastAvailableProcessingGeneration);

        Assert.Single(
            document.KnowledgeAvailability);
    }

    [Fact]
    public void Available_WhenGenerationIsProvided_CreatesValidKnowledgeAvailability()
    {
        // Arrange
        const long processingGeneration = 3L;

        // Act
        var availability =
            new DocumentKnowledgeAvailability(
                DocumentKnowledgeRepresentationKind.KeywordSearch,
                DocumentKnowledgeAvailabilityState.Available,
                processingGeneration);

        // Assert
        Assert.Equal(
            DocumentKnowledgeRepresentationKind.KeywordSearch,
            availability.Representation);

        Assert.Equal(
            DocumentKnowledgeAvailabilityState.Available,
            availability.State);

        Assert.Equal(
            processingGeneration,
            availability.LastAvailableProcessingGeneration);
    }

    [Fact]
    public void Stale_WhenGenerationIsProvided_CreatesValidKnowledgeAvailability()
    {
        // Arrange
        const long processingGeneration = 5L;

        // Act
        var availability =
            new DocumentKnowledgeAvailability(
                DocumentKnowledgeRepresentationKind.KeywordSearch,
                DocumentKnowledgeAvailabilityState.Stale,
                processingGeneration);

        // Assert
        Assert.Equal(
            DocumentKnowledgeAvailabilityState.Stale,
            availability.State);

        Assert.Equal(
            processingGeneration,
            availability.LastAvailableProcessingGeneration);
    }

    [Fact]
    public void Failed_WhenGenerationIsNotAvailable_CreatesValidKnowledgeAvailability()
    {
        // Arrange
        // No successful representation generation exists.

        // Act
        var availability =
            new DocumentKnowledgeAvailability(
                DocumentKnowledgeRepresentationKind.KeywordSearch,
                DocumentKnowledgeAvailabilityState.Failed,
                null);

        // Assert
        Assert.Equal(
            DocumentKnowledgeAvailabilityState.Failed,
            availability.State);

        Assert.Null(
            availability.LastAvailableProcessingGeneration);
    }

    [Fact]
    public void Available_WhenGenerationIsMissing_ThrowsArgumentException()
    {
        // Arrange
        long? processingGeneration = null;

        // Act
        ArgumentException exception =
            Assert.Throws<ArgumentException>(
                () =>
                    new DocumentKnowledgeAvailability(
                        DocumentKnowledgeRepresentationKind.KeywordSearch,
                        DocumentKnowledgeAvailabilityState.Available,
                        processingGeneration));

        // Assert
        Assert.Equal(
            "lastAvailableProcessingGeneration",
            exception.ParamName);

        Assert.Contains(
            "Available or stale knowledge must reference a processing generation.",
            exception.Message);
    }

    [Fact]
    public void Stale_WhenGenerationIsMissing_ThrowsArgumentException()
    {
        // Arrange
        long? processingGeneration = null;

        // Act
        ArgumentException exception =
            Assert.Throws<ArgumentException>(
                () =>
                    new DocumentKnowledgeAvailability(
                        DocumentKnowledgeRepresentationKind.KeywordSearch,
                        DocumentKnowledgeAvailabilityState.Stale,
                        processingGeneration));

        // Assert
        Assert.Equal(
            "lastAvailableProcessingGeneration",
            exception.ParamName);

        Assert.Contains(
            "Available or stale knowledge must reference a processing generation.",
            exception.Message);
    }

    [Fact]
    public void KnowledgeAvailability_WhenGenerationIsZero_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        const long processingGeneration = 0L;

        // Act
        ArgumentOutOfRangeException exception =
            Assert.Throws<ArgumentOutOfRangeException>(
                () =>
                    new DocumentKnowledgeAvailability(
                        DocumentKnowledgeRepresentationKind.KeywordSearch,
                        DocumentKnowledgeAvailabilityState.Unavailable,
                        processingGeneration));

        // Assert
        Assert.Equal(
            "lastAvailableProcessingGeneration",
            exception.ParamName);

        Assert.Contains(
            "must be greater than zero",
            exception.Message);
    }

    [Fact]
    public void KnowledgeAvailability_WhenGenerationIsNegative_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        const long processingGeneration = -1L;

        // Act
        ArgumentOutOfRangeException exception =
            Assert.Throws<ArgumentOutOfRangeException>(
                () =>
                    new DocumentKnowledgeAvailability(
                        DocumentKnowledgeRepresentationKind.KeywordSearch,
                        DocumentKnowledgeAvailabilityState.Unavailable,
                        processingGeneration));

        // Assert
        Assert.Equal(
            "lastAvailableProcessingGeneration",
            exception.ParamName);

        Assert.Contains(
            "must be greater than zero",
            exception.Message);
    }

    [Fact]
    public void KnowledgeAvailability_WhenRepresentationIsInvalid_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var representation =
            (DocumentKnowledgeRepresentationKind)999;

        // Act
        ArgumentOutOfRangeException exception =
            Assert.Throws<ArgumentOutOfRangeException>(
                () =>
                    new DocumentKnowledgeAvailability(
                        representation,
                        DocumentKnowledgeAvailabilityState.Unavailable,
                        null));

        // Assert
        Assert.Equal(
            "representation",
            exception.ParamName);

        Assert.Contains(
            "Knowledge representation is invalid.",
            exception.Message);
    }

    [Fact]
    public void KnowledgeAvailability_WhenStateIsInvalid_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var state =
            (DocumentKnowledgeAvailabilityState)999;

        // Act
        ArgumentOutOfRangeException exception =
            Assert.Throws<ArgumentOutOfRangeException>(
                () =>
                    new DocumentKnowledgeAvailability(
                        DocumentKnowledgeRepresentationKind.KeywordSearch,
                        state,
                        null));

        // Assert
        Assert.Equal(
            "state",
            exception.ParamName);

        Assert.Contains(
            "Knowledge availability state is invalid.",
            exception.Message);
    }

    [Fact]
    public void SetKnowledgeAvailability_WhenRepresentationIsAlreadyDefined_ReplacesExistingOutcome()
    {
        // Arrange
        Document document =
            CreateDocument();

        document.SetKnowledgeAvailability(
            DocumentKnowledgeRepresentationKind.KeywordSearch,
            DocumentKnowledgeAvailabilityState.Failed,
            null);

        // Act
        document.SetKnowledgeAvailability(
            DocumentKnowledgeRepresentationKind.KeywordSearch,
            DocumentKnowledgeAvailabilityState.Stale,
            4L);

        // Assert
        DocumentKnowledgeAvailability availability =
            document.GetKnowledgeAvailability(
                DocumentKnowledgeRepresentationKind.KeywordSearch);

        Assert.Equal(
            DocumentKnowledgeAvailabilityState.Stale,
            availability.State);

        Assert.Equal(
            4L,
            availability.LastAvailableProcessingGeneration);

        Assert.Single(
            document.KnowledgeAvailability);
    }

    [Fact]
    public void SetKnowledgeAvailability_WhenRepresentationIsNotAlreadyDefined_AddsIndependentOutcome()
    {
        // Arrange
        Document document =
            Document.Create(
                Guid.NewGuid(),
                "document.txt",
                "Test Document",
                $"hash-{Guid.NewGuid():N}",
                "document.dvault");

        // Act
        // KeywordSearch is currently the only concrete MVP2 representation.
        // This verifies the collection behaves as an independent representation store.
        document.SetKnowledgeAvailability(
            DocumentKnowledgeRepresentationKind.KeywordSearch,
            DocumentKnowledgeAvailabilityState.Available,
            2L);

        // Assert
        DocumentKnowledgeAvailability availability =
            document.GetKnowledgeAvailability(
                DocumentKnowledgeRepresentationKind.KeywordSearch);

        Assert.Equal(
            DocumentKnowledgeAvailabilityState.Available,
            availability.State);

        Assert.Equal(
            2L,
            availability.LastAvailableProcessingGeneration);
    }

    [Fact]
    public void SuccessfulProcessing_WhenKeywordSearchRemainsUnavailable_ProcessingAndKnowledgeStatesRemainIndependent()
    {
        // Arrange
        const long processingGeneration = 3L;

        Document document =
            Document.Restore(
                Guid.NewGuid(),
                "document.txt",
                "Successfully Processed Document",
                $"hash-{Guid.NewGuid():N}",
                "document.dvault",
                DateTime.UtcNow,
                DocumentStatus.Available,
                processingGeneration,
                processingGeneration,
                "processing-v1",
                DocumentLifecycleState.Active,
                DocumentProcessingState.Succeeded,
                [
                    new DocumentKnowledgeAvailability(
                        DocumentKnowledgeRepresentationKind.KeywordSearch,
                        DocumentKnowledgeAvailabilityState.Unavailable,
                        null)
                ]);

        // Act
        DocumentKnowledgeAvailability availability =
            document.GetKnowledgeAvailability(
                DocumentKnowledgeRepresentationKind.KeywordSearch);

        // Assert
        Assert.Equal(
            DocumentProcessingState.Succeeded,
            document.ProcessingState);

        Assert.Equal(
            DocumentKnowledgeAvailabilityState.Unavailable,
            availability.State);

        Assert.Null(
            availability.LastAvailableProcessingGeneration);

        Assert.Equal(
            processingGeneration,
            document.LastSuccessfulProcessingGeneration);
    }

    [Fact]
    public void SetKnowledgeAvailability_WhenMarkedAvailable_AssociatesRepresentationWithProcessingGeneration()
    {
        // Arrange
        const long processingGeneration = 7L;

        Document document =
            Document.Restore(
                Guid.NewGuid(),
                "document.txt",
                "Test Document",
                $"hash-{Guid.NewGuid():N}",
                "document.dvault",
                DateTime.UtcNow,
                DocumentStatus.Available,
                processingGeneration,
                processingGeneration,
                "processing-v1",
                DocumentLifecycleState.Active,
                DocumentProcessingState.Succeeded);

        // Act
        document.SetKnowledgeAvailability(
            DocumentKnowledgeRepresentationKind.KeywordSearch,
            DocumentKnowledgeAvailabilityState.Available,
            processingGeneration);

        // Assert
        DocumentKnowledgeAvailability availability =
            document.GetKnowledgeAvailability(
                DocumentKnowledgeRepresentationKind.KeywordSearch);

        Assert.Equal(
            DocumentKnowledgeAvailabilityState.Available,
            availability.State);

        Assert.Equal(
            processingGeneration,
            availability.LastAvailableProcessingGeneration);
    }

    [Fact]
    public void LifecycleStateChange_DoesNotInferKnowledgeAvailability()
    {
        // Arrange
        Document document =
            CreateDocument();

        // Act
        document.Archive();

        DocumentKnowledgeAvailability availability =
            document.GetKnowledgeAvailability(
                DocumentKnowledgeRepresentationKind.KeywordSearch);

        // Assert
        Assert.Equal(
            DocumentLifecycleState.Archived,
            document.LifecycleState);

        Assert.Equal(
            DocumentKnowledgeAvailabilityState.Unavailable,
            availability.State);

        Assert.Null(
            availability.LastAvailableProcessingGeneration);
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
