using DeskVault.Application.Documents.Content;
using DeskVault.Application.Documents.Provenance;

namespace DeskVault.Application.Documents.Normalization;

/// <summary>
/// Represents the normalized result of document-content processing.
/// </summary>
/// <remarks>
/// Text remains the deterministic searchable representation used by the
/// existing downstream processing pipeline. Content carries the normalized
/// structured representation when one is available.
/// </remarks>
public sealed record DocumentTextNormalizationResult(
    string Text,
    DocumentSourceLocationMappingKind SourceLocationMappingKind =
        DocumentSourceLocationMappingKind.Unknown,
    DocumentContent? Content = null);
