using DeskVault.Application.Documents.Provenance;

namespace DeskVault.Application.Documents.Normalization;

public sealed record DocumentTextNormalizationResult(
    string Text,
    DocumentSourceLocationMappingKind SourceLocationMappingKind =
        DocumentSourceLocationMappingKind.Unknown);
