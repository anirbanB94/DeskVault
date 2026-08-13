using DeskVault.Application.Interfaces;

namespace DeskVault.Application.Documents.Queries.GetDocument;

public sealed class GetDocumentHandler
{
    private readonly IDocumentRepository _repository;

    public GetDocumentHandler(
        IDocumentRepository repository)
    {
        _repository = repository;
    }

    public async Task<GetDocumentResult> HandleAsync(
        GetDocumentQuery query,
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

        return new GetDocumentResult(
            document.Id,
            document.FileName,
            document.DisplayName,
            document.Sha256Hash,
            document.ImportedAt,
            document.Status);
    }
}
