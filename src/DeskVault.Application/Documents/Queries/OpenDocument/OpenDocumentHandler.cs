using DeskVault.Application.Interfaces;

namespace DeskVault.Application.Documents.Queries.OpenDocument;

public sealed class OpenDocumentHandler
{
    private readonly IDocumentRepository _repository;
    private readonly IDocumentReader _documentReader;

    public OpenDocumentHandler(
        IDocumentRepository repository,
        IDocumentReader documentReader)
    {
        _repository = repository;
        _documentReader = documentReader;
    }

    public async Task<OpenDocumentResult> HandleAsync(
        OpenDocumentQuery query,
        CancellationToken cancellationToken = default)
    {
        var document = await _repository.GetByIdAsync(
            query.DocumentId,
            cancellationToken);

        if (document is null)
        {
            throw new FileNotFoundException(
                "The requested document could not be found.");
        }

        Stream content = await _documentReader.OpenReadAsync(
            document.StoredFilePath,
            cancellationToken);

        return new OpenDocumentResult(
            content,
            document.FileName);
    }
}