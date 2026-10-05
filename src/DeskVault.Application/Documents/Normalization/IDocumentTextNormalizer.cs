using DeskVault.Application.Documents.Extraction;

namespace DeskVault.Application.Documents.Normalization;

public interface IDocumentTextNormalizer
{
    string RuleVersion { get; }

    Task<DocumentTextNormalizationResult> NormalizeAsync(
        DocumentTextExtractionResult extractionResult,
        CancellationToken cancellationToken = default);
}
