using DeskVault.Application.Documents.Queries.SearchDocuments;

namespace DeskVault.Application.Interfaces;

public interface IDocumentSearchStore
{
    Task<IReadOnlyList<SearchDocumentsResult>> SearchAsync(
        string searchText,
        CancellationToken cancellationToken = default);
}
