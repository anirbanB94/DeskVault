namespace DeskVault.Application.Documents.Processing;

public sealed class StaleProcessingGenerationException
    : InvalidOperationException
{
    public StaleProcessingGenerationException(
        Guid documentId,
        long processingGeneration,
        long currentGeneration)
        : base(
            $"Processing generation '{processingGeneration}' for document '{documentId}' is stale. " +
            $"The current generation is '{currentGeneration}'.")
    {
        DocumentId = documentId;
        ProcessingGeneration = processingGeneration;
        CurrentGeneration = currentGeneration;
    }

    public Guid DocumentId { get; }

    public long ProcessingGeneration { get; }

    public long CurrentGeneration { get; }
}
