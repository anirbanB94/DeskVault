using DeskVault.Domain.Documents;

namespace DeskVault.Application.Tests;

public sealed class DocumentLegacyStateProjectionTests
{
    [Theory]
    [InlineData(
        DocumentStatus.Available,
        DocumentLifecycleState.Active,
        DocumentProcessingState.Succeeded,
        true)]
    [InlineData(
        DocumentStatus.Indexed,
        DocumentLifecycleState.Active,
        DocumentProcessingState.Succeeded,
        true)]
    [InlineData(
        DocumentStatus.Processing,
        DocumentLifecycleState.Active,
        DocumentProcessingState.Processing,
        true)]
    [InlineData(
        DocumentStatus.Failed,
        DocumentLifecycleState.Active,
        DocumentProcessingState.Failed,
        true)]
    [InlineData(
        DocumentStatus.Archived,
        DocumentLifecycleState.Archived,
        DocumentProcessingState.Succeeded,
        true)]
    public void Restore_WhenLegacyStateHasSuccessfulGeneration_ProjectsIndependentStates(
        DocumentStatus status,
        DocumentLifecycleState expectedLifecycleState,
        DocumentProcessingState expectedProcessingState,
        bool expectedKnowledgeAvailable)
    {
        // Arrange
        const long processingGeneration = 7L;
        const long lastSuccessfulProcessingGeneration = 7L;

        Document document =
            RestoreLegacyDocument(
                status,
                processingGeneration,
                lastSuccessfulProcessingGeneration);

        // Act
        DocumentLifecycleState lifecycleState =
            document.LifecycleState;

        DocumentProcessingState processingState =
            document.ProcessingState;

        DocumentKnowledgeAvailability knowledgeAvailability =
            document.GetKnowledgeAvailability(
                DocumentKnowledgeRepresentationKind.KeywordSearch);

        // Assert
        Assert.Equal(
            expectedLifecycleState,
            lifecycleState);

        Assert.Equal(
            expectedProcessingState,
            processingState);

        Assert.Equal(
            expectedKnowledgeAvailable
                ? DocumentKnowledgeAvailabilityState.Available
                : DocumentKnowledgeAvailabilityState.Unavailable,
            knowledgeAvailability.State);

        if (expectedKnowledgeAvailable)
        {
            Assert.Equal(
                lastSuccessfulProcessingGeneration,
                knowledgeAvailability.LastAvailableProcessingGeneration);
        }
        else
        {
            Assert.Null(
                knowledgeAvailability.LastAvailableProcessingGeneration);
        }
    }

    [Theory]
    [InlineData(DocumentStatus.Available)]
    [InlineData(DocumentStatus.Indexed)]
    public void Restore_WhenLegacyAvailableStateHasUnknownGeneration_PreservesSuccessfulOutcomeWithoutFabricatingGeneration(
        DocumentStatus status)
    {
        // Arrange
        Document document =
            RestoreLegacyDocument(
                status,
                processingGeneration: 0L,
                lastSuccessfulProcessingGeneration: 0L);

        // Act
        DocumentKnowledgeAvailability knowledgeAvailability =
            document.GetKnowledgeAvailability(
                DocumentKnowledgeRepresentationKind.KeywordSearch);

        // Assert
        Assert.Equal(
            DocumentLifecycleState.Active,
            document.LifecycleState);

        Assert.Equal(
            DocumentProcessingState.Succeeded,
            document.ProcessingState);

        Assert.Equal(
            DocumentKnowledgeAvailabilityState.Available,
            knowledgeAvailability.State);

        Assert.Equal(
            0L,
            knowledgeAvailability.LastAvailableProcessingGeneration);

        Assert.Equal(
            0L,
            document.LastSuccessfulProcessingGeneration);
    }

    [Theory]
    [InlineData(
        DocumentStatus.Imported,
        DocumentLifecycleState.Active,
        DocumentProcessingState.NeverProcessed)]
    [InlineData(
        DocumentStatus.Deleted,
        DocumentLifecycleState.Deleted,
        DocumentProcessingState.NeverProcessed)]
    public void Restore_WhenLegacyStateHasNoSuccessfulGeneration_DoesNotFabricateKnowledge(
        DocumentStatus status,
        DocumentLifecycleState expectedLifecycleState,
        DocumentProcessingState expectedProcessingState)
    {
        // Arrange
        Document document =
            RestoreLegacyDocument(
                status,
                processingGeneration: 0L,
                lastSuccessfulProcessingGeneration: 0L);

        // Act
        DocumentKnowledgeAvailability knowledgeAvailability =
            document.GetKnowledgeAvailability(
                DocumentKnowledgeRepresentationKind.KeywordSearch);

        // Assert
        Assert.Equal(
            expectedLifecycleState,
            document.LifecycleState);

        Assert.Equal(
            expectedProcessingState,
            document.ProcessingState);

        Assert.Equal(
            DocumentKnowledgeAvailabilityState.Unavailable,
            knowledgeAvailability.State);

        Assert.Null(
            knowledgeAvailability.LastAvailableProcessingGeneration);
    }

    [Fact]
    public void Restore_WhenLegacyProcessingFailsAfterSuccessfulGeneration_PreservesKeywordKnowledge()
    {
        // Arrange
        Document document =
            RestoreLegacyDocument(
                DocumentStatus.Failed,
                processingGeneration: 8L,
                lastSuccessfulProcessingGeneration: 7L);

        // Act
        DocumentKnowledgeAvailability knowledgeAvailability =
            document.GetKnowledgeAvailability(
                DocumentKnowledgeRepresentationKind.KeywordSearch);

        // Assert
        Assert.Equal(
            DocumentProcessingState.Failed,
            document.ProcessingState);

        Assert.Equal(
            DocumentKnowledgeAvailabilityState.Available,
            knowledgeAvailability.State);

        Assert.Equal(
            7L,
            knowledgeAvailability.LastAvailableProcessingGeneration);
    }

    private static Document RestoreLegacyDocument(
        DocumentStatus status,
        long processingGeneration,
        long lastSuccessfulProcessingGeneration)
    {
        return Document.Restore(
            Guid.NewGuid(),
            "document.txt",
            "Test Document",
            "test-sha256",
            "document.dvault",
            new DateTime(
                2026,
                10,
                6,
                10,
                0,
                0,
                DateTimeKind.Utc),
            status,
            processingGeneration,
            lastSuccessfulProcessingGeneration);
    }
}
