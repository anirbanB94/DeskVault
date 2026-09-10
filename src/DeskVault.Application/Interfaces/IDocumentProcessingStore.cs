using DeskVault.Application.Documents.Chunking;
using DeskVault.Domain.Documents;

namespace DeskVault.Application.Interfaces;

public interface IDocumentProcessingStore
{
    Task<long> AcquireProcessingGenerationAsync(
        Guid documentId,
        CancellationToken cancellationToken = default);

    Task PublishProcessingStateAsync(
        Guid documentId,
        long processingGeneration,
        DocumentStatus status,
        CancellationToken cancellationToken = default);

    Task PublishSuccessfulProcessingAsync(
        Guid documentId,
        long processingGeneration,
        CancellationToken cancellationToken = default);

    Task RecoverCancelledProcessingAsync(
        Guid documentId,
        long processingGeneration,
        CancellationToken cancellationToken = default);

    Task ReplaceChunksAsync(
        Guid documentId,
        long processingGeneration,
        IReadOnlyList<DocumentChunk> chunks,
        CancellationToken cancellationToken = default);
}
