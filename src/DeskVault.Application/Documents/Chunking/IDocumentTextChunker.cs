using DeskVault.Application.Documents.Normalization;
using DeskVault.Application.Documents.Processing;

namespace DeskVault.Application.Documents.Chunking;

public interface IDocumentTextChunker
{
    DocumentChunkingRuleVersion RuleVersion { get; }

    Task<IReadOnlyList<DocumentChunk>> ChunkAsync(
        DocumentTextNormalizationResult normalizationResult,
        CancellationToken cancellationToken = default);
}
