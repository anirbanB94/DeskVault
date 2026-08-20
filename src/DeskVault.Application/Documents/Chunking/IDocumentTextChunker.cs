using DeskVault.Application.Documents.Normalization;

namespace DeskVault.Application.Documents.Chunking;

public interface IDocumentTextChunker
{
    Task<IReadOnlyList<DocumentChunk>> ChunkAsync(
        DocumentTextNormalizationResult normalizationResult,
        CancellationToken cancellationToken = default);
}
