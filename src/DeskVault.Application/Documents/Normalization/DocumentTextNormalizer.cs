using DeskVault.Application.Documents.Extraction;

namespace DeskVault.Application.Documents.Normalization;

public sealed class DocumentTextNormalizer
    : IDocumentTextNormalizer
{
    public Task<DocumentTextNormalizationResult> NormalizeAsync(
        DocumentTextExtractionResult extractionResult,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            extractionResult);

        cancellationToken.ThrowIfCancellationRequested();

        string normalizedText =
            extractionResult.Text
                .Normalize()
                .Replace(
                    "\r\n",
                    "\n",
                    StringComparison.Ordinal)
                .Replace(
                    "\r",
                    "\n",
                    StringComparison.Ordinal);

        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(
            new DocumentTextNormalizationResult(
                normalizedText));
    }
}
