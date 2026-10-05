using DeskVault.Application.Documents.Chunking;
using DeskVault.Application.Documents.Processing;

namespace DeskVault.Application.Documents.Commands.ProcessDocument;

public sealed record ProcessDocumentResult(
    ProcessDocumentResultStatus Status,
    Guid? DocumentId,
    string Description,
    DocumentProcessingRuleVersion? ProcessingRuleVersion = null,
    DocumentChunkingRuleVersion? ChunkingRuleVersion = null);
