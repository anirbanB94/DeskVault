namespace DeskVault.Application.Documents.Extraction;

public interface IDocumentTextExtractor
{
    bool CanExtract(string fileName);

    Task<DocumentTextExtractionResult> ExtractAsync(
        Stream documentStream,
        string fileName,
        CancellationToken cancellationToken = default);
}
