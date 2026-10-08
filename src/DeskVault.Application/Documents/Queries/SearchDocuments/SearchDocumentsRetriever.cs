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

        var results =
            await _searchStore.SearchAsync(
                query,
                cancellationToken);

        var rankedResults =
            _ranker.Rank(
                results);

        int position =
            FindStartingPosition(
                rankedResults,
                continuationPosition);

        var pagedResults =
            rankedResults
                .Skip(position)
                .Take(query.Limit)
                .ToList();

        bool hasMore =
            position + pagedResults.Count <
            rankedResults.Count;

        SearchDocumentsContinuation? nextContinuation =
            null;

        if (hasMore)
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
            rankedResults.Count);

        return new SearchDocumentsPage(
            pagedResults,
            hasMore,
            nextContinuation);
    }

    private int FindStartingPosition(
        IReadOnlyList<SearchDocumentsResult> rankedResults,
        SearchDocumentsRankingKey? continuationPosition)
    {
        if (continuationPosition is null)
        {
            return 0;
        }

        for (int index = 0;
             index < rankedResults.Count;
             index++)
        {
            SearchDocumentsRankingKey rankingKey =
                _ranker.GetRankingKey(
                    rankedResults[index]);

            if (rankingKey.CompareTo(
                    continuationPosition.Value) > 0)
            {
                return index;
            }
        }

        return rankedResults.Count;
    }
}
