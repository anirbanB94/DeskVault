using DeskVault.Application.Interfaces;

namespace DeskVault.Application.Documents.Queries.SearchDocuments;

public sealed class SearchDocumentsHandler
{
    private readonly IDocumentSearchStore _searchStore;

    public SearchDocumentsHandler(
        IDocumentSearchStore searchStore)
    {
        _searchStore = searchStore;
    }

    public Task<IReadOnlyList<SearchDocumentsResult>> HandleAsync(
        SearchDocumentsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return _searchStore.SearchAsync(
            query.SearchText,
            cancellationToken);
    }
}
