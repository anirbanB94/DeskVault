using DeskVault.Application.Interfaces;
using DeskVault.Domain.Documents;

namespace DeskVault.Application.Documents.Queries.ListDocuments;

public sealed class ListDocumentsHandler
{
    private readonly IDocumentRepository _repository;

    public ListDocumentsHandler(
        IDocumentRepository repository)
    {
        _repository = repository;
    }

    public Task<IReadOnlyList<Document>> HandleAsync(
        ListDocumentsQuery query,
        CancellationToken cancellationToken = default)
    {
        return _repository.GetAllAsync(
            cancellationToken);
    }
}