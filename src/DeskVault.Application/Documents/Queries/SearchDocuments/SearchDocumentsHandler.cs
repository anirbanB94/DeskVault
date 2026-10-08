using DeskVault.Application.Interfaces;

namespace DeskVault.Application.Documents.Queries.SearchDocuments;

public sealed class SearchDocumentsHandler
{
    private readonly ISearchDocumentsRetriever _retriever;

    public SearchDocumentsHandler(
        ISearchDocumentsRetriever retriever)
    {
        ArgumentNullException.ThrowIfNull(retriever);

        _retriever = retriever;
    }

    public Task<SearchDocumentsPage> HandleAsync(
        SearchDocumentsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return _retriever.RetrieveAsync(
            query,
            cancellationToken);
    }
}
