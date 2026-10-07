using DeskVault.Application.Documents.Queries.SearchDocuments;

namespace DeskVault.Application.Interfaces;

public interface ISearchDocumentsContinuationCodec
{
    SearchDocumentsContinuation Create(
        SearchDocumentsQuery query,
        SearchDocumentsRankingKey rankingKey);

    SearchDocumentsRankingKey Decode(
        SearchDocumentsQuery query,
        SearchDocumentsContinuation continuation);
}
