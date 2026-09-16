using DeskVault.Application.Interfaces;
using DeskVault.Shared.Resources;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace DeskVault.Application.Documents.Queries.SearchDocuments;

public sealed class SearchDocumentsHandler
{
    private readonly IDocumentSearchStore _searchStore;
    private readonly ISearchDocumentsRanker _ranker;
    private readonly ILogger<SearchDocumentsHandler> _logger;

    public SearchDocumentsHandler(
        IDocumentSearchStore searchStore,
        ISearchDocumentsRanker ranker,
        ILogger<SearchDocumentsHandler> logger)
    {
        _searchStore = searchStore;
        _ranker = ranker;
        _logger = logger;
    }

    public async Task<SearchDocumentsPage> HandleAsync(
        SearchDocumentsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.Continuation is not null &&
            string.IsNullOrWhiteSpace(query.Continuation.Value))
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

        _logger.LogInformation(
            LogMessages.DocumentSearchStarted);

        var results =
            await _searchStore.SearchAsync(
                query,
                cancellationToken);

        var rankedResults =
            _ranker.Rank(results);

        int position = 0;

        if (query.Continuation is not null &&
            !int.TryParse(
                query.Continuation.Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out position))
        {
            throw new ArgumentException(
                "Continuation value must be a non-negative integer.",
                nameof(query.Continuation));
        }

        if (position < 0)
        {
            throw new ArgumentException(
                "Continuation value must be a non-negative integer.",
                nameof(query.Continuation));
        }

        var pagedResults =
            rankedResults
                .Skip(position)
                .Take(query.Limit)
                .ToList();

        bool hasMore =
            position + pagedResults.Count <
            rankedResults.Count;

        var nextContinuation =
            hasMore
                ? new SearchDocumentsContinuation(
                    (position + pagedResults.Count).ToString(
                        CultureInfo.InvariantCulture))
                : null;

        _logger.LogInformation(
            LogMessages.DocumentSearchCompleted,
            rankedResults.Count);

        return new SearchDocumentsPage(
            pagedResults,
            hasMore,
            nextContinuation);
    }
}
