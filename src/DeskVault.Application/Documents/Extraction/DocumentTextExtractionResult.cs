using DeskVault.Application.Documents.Content;
using DeskVault.Application.Documents.Provenance;

namespace DeskVault.Application.Documents.Extraction;

/// <summary>
/// Represents the result produced by a document text extractor.
/// </summary>
/// <remarks>
/// Text remains the existing text-oriented extraction result. Content is
/// optionally supplied when the extractor can preserve useful structure.
/// </remarks>
public sealed record DocumentTextExtractionResult(
    string Text,
    DocumentSourceLocationMappingKind SourceLocationMappingKind =
        DocumentSourceLocationMappingKind.Unknown,
    DocumentContent? Content = null);
