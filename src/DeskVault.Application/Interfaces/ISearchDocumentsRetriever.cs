using DeskVault.Application.Documents.Queries.SearchDocuments;

namespace DeskVault.Application.Interfaces;

public interface ISearchDocumentsRetriever
{
    Task<SearchDocumentsPage> RetrieveAsync(
        SearchDocumentsQuery query,
        CancellationToken cancellationToken = default);
}
