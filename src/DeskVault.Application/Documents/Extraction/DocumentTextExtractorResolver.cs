using System.Collections.Generic;

namespace DeskVault.Application.Documents.Extraction;

public sealed class DocumentTextExtractorResolver
{
    private readonly IReadOnlyList<IDocumentTextExtractor> _extractors;

    public DocumentTextExtractorResolver(
        IEnumerable<IDocumentTextExtractor> extractors)
    {
        _extractors =
            extractors
                .ToList();
    }

    public IDocumentTextExtractor Resolve(
        string fileName)
    {
        IDocumentTextExtractor? extractor =
            _extractors.FirstOrDefault(
                candidate => candidate.CanExtract(fileName));

        if (extractor is null)
        {
            throw new NotSupportedException(
                $"No document text extractor is available for '{fileName}'.");
        }

        return extractor;
    }
}
