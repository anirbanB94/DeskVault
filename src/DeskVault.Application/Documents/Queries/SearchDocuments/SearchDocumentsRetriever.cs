using DeskVault.Application.Interfaces;
using DeskVault.Shared.Resources;
using Microsoft.Extensions.Logging;

namespace DeskVault.Application.Documents.Queries.SearchDocuments;

public sealed class SearchDocumentsRetriever : ISearchDocumentsRetriever
{
    private readonly IDocumentSearchStore _searchStore;
    private readonly ISearchDocumentsRanker _ranker;
    private readonly ISearchDocumentsContinuationCodec _continuationCodec;
    private readonly ILogger<SearchDocumentsRetriever> _logger;

    public SearchDocumentsRetriever(
        IDocumentSearchStore searchStore,
        ISearchDocumentsRanker ranker,
        ISearchDocumentsContinuationCodec continuationCodec,
        ILogger<SearchDocumentsRetriever> logger)
    {
        ArgumentNullException.ThrowIfNull(searchStore);
        ArgumentNullException.ThrowIfNull(ranker);
        ArgumentNullException.ThrowIfNull(continuationCodec);
        ArgumentNullException.ThrowIfNull(logger);

        _searchStore = searchStore;
        _ranker = ranker;
        _continuationCodec = continuationCodec;
        _logger = logger;
    }

    public async Task<SearchDocumentsPage> RetrieveAsync(
        SearchDocumentsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.Continuation is not null &&
            string.IsNullOrWhiteSpace(
                query.Continuation.Value))
        {
            throw new ArgumentException(
                "Continuation value cannot be empty.",
                nameof(query.Continuation));
        }

        if (query.Limit <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query.Limit));
        }

        SearchDocumentsRankingKey? continuationPosition =
            query.Continuation is null
                ? null
                : _continuationCodec.Decode(
                    query,
                    query.Continuation);

        _logger.LogInformation(
            LogMessages.DocumentSearchStarted);

        IReadOnlyList<SearchDocumentsResult> results =
            await _searchStore.SearchAsync(
                query,
                continuationPosition,
                query.Limit,
                cancellationToken);

        bool hasMore =
            results.Count > query.Limit;

        IReadOnlyList<SearchDocumentsResult> pagedResults =
            hasMore
                ? results
                    .Take(query.Limit)
                    .ToList()
                : results;

        SearchDocumentsContinuation? nextContinuation =
            null;

        if (hasMore &&
            pagedResults.Count > 0)
        {
            SearchDocumentsRankingKey lastRankingKey =
                _ranker.GetRankingKey(
                    pagedResults[^1]);

            nextContinuation =
                _continuationCodec.Create(
                    query,
                    lastRankingKey);
        }

        _logger.LogInformation(
            LogMessages.DocumentSearchCompleted,
            pagedResults.Count);

        return new SearchDocumentsPage(
            pagedResults,
            hasMore,
            nextContinuation);
    }
}
