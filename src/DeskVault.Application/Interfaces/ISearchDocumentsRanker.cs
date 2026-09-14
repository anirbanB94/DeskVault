using DeskVault.Application.Documents.Queries.SearchDocuments;

namespace DeskVault.Application.Interfaces;

public interface ISearchDocumentsRanker
{
    IReadOnlyList<SearchDocumentsResult> Rank(
        IReadOnlyList<SearchDocumentsResult> results);
}
