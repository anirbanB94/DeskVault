using DeskVault.Application.Documents.Queries.SearchDocuments;

namespace DeskVault.Application.Interfaces;

public interface IDocumentSearchStore
{
    Task<IReadOnlyList<SearchDocumentsResult>> SearchAsync(
        SearchDocumentsQuery query,
        CancellationToken cancellationToken = default);
}
