using DeskVault.Application.Documents.Queries.SearchDocuments;

namespace DeskVault.Application.Interfaces;

public interface IDocumentSearchStore
{
    Task<IReadOnlyList<SearchDocumentsResult>> SearchAsync(
        SearchDocumentsQuery query,
        SearchDocumentsRankingKey? continuationPosition,
        int pageSize,
        CancellationToken cancellationToken = default);
}
