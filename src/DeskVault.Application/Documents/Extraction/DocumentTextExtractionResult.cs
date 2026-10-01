using DeskVault.Application.Documents.Provenance;

namespace DeskVault.Application.Documents.Extraction;

public sealed record DocumentTextExtractionResult(
    string Text,
    DocumentSourceLocationMappingKind SourceLocationMappingKind =
        DocumentSourceLocationMappingKind.Unknown);
