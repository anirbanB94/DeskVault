using DeskVault.Application.Documents.Chunking;

namespace DeskVault.Application.Interfaces;

public interface IDocumentProcessingStore
{
    Task ReplaceChunksAsync(
        Guid documentId,
        IReadOnlyList<DocumentChunk> chunks,
        CancellationToken cancellationToken = default);
}
