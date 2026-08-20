using DeskVault.Application.Documents.Extraction;

namespace DeskVault.Application.Documents.Normalization;

public interface IDocumentTextNormalizer
{
    Task<DocumentTextNormalizationResult> NormalizeAsync(
        DocumentTextExtractionResult extractionResult,
        CancellationToken cancellationToken = default);
}
