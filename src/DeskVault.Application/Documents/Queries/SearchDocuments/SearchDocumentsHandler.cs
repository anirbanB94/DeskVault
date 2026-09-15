using DeskVault.Application.Interfaces;
using DeskVault.Shared.Resources;
using Microsoft.Extensions.Logging;

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

    public async Task<IReadOnlyList<SearchDocumentsResult>> HandleAsync(
        SearchDocumentsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        _logger.LogInformation(
            LogMessages.DocumentSearchStarted);

        var results =
            await _searchStore.SearchAsync(
                query,
                cancellationToken);

        var rankedResults =
            _ranker.Rank(results);

        _logger.LogInformation(
            LogMessages.DocumentSearchCompleted,
            rankedResults.Count);

        return rankedResults;
    }
}
