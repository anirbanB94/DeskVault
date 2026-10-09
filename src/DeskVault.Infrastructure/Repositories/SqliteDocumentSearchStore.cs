using DeskVault.Application.Documents.Queries.SearchDocuments;
using DeskVault.Application.Interfaces;
using DeskVault.Domain.Documents;
using DeskVault.Infrastructure.Persistence;
using DeskVault.Infrastructure.Persistence.Context;
using DeskVault.Shared.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DeskVault.Infrastructure.Repositories;

public sealed class SqliteDocumentSearchStore
    : IDocumentSearchStore
{
    private readonly IDbContextFactory<DeskVaultDbContext> _dbContextFactory;
    private readonly ISearchDocumentsRanker _ranker;
    private readonly ILogger<SqliteDocumentSearchStore> _logger;

    public SqliteDocumentSearchStore(
        IDbContextFactory<DeskVaultDbContext> dbContextFactory,
        ISearchDocumentsRanker ranker,
        ILogger<SqliteDocumentSearchStore> logger)
    {
        ArgumentNullException.ThrowIfNull(dbContextFactory);
        ArgumentNullException.ThrowIfNull(ranker);
        ArgumentNullException.ThrowIfNull(logger);

        _dbContextFactory = dbContextFactory;
        _ranker = ranker;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SearchDocumentsResult>> SearchAsync(
        SearchDocumentsQuery query,
        SearchDocumentsRankingKey? continuationPosition,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        string normalizedSearchText =
            SearchTextCanonicalizer.CanonicalizeSearchText(
                query.SearchText);

        if (pageSize <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageSize));
        }

        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation(
            LogMessages.DocumentSearchStoreStarted);

        try
        {
            await using var dbContext =
                await _dbContextFactory.CreateDbContextAsync(
                    cancellationToken);

            IReadOnlyList<string>? normalizedFileTypes =
                NormalizeFileTypes(
                    query.FileTypes);

            string escapedSearchText =
                EscapeLikePattern(
                    normalizedSearchText);

            var contentMatchesQuery =
                from chunk in dbContext.DocumentChunks.AsNoTracking()
                join document in dbContext.Documents.AsNoTracking()
                    on chunk.DocumentId equals document.Id
                where
                    SqliteSearchFunctions.ContainsCanonicalized(
                        chunk.Text,
                        normalizedSearchText)
                    && chunk.ProcessingGeneration ==
                       document.LastSuccessfulProcessingGeneration
                    && dbContext.DocumentKnowledgeAvailabilities.Any(
                        availability =>
                            availability.DocumentId ==
                            chunk.DocumentId
                            && availability.Representation ==
                            (int)
                                DocumentKnowledgeRepresentationKind
                                    .KeywordSearch
                            && availability.State ==
                            (int)
                                DocumentKnowledgeAvailabilityState
                                    .Available
                            && availability.LastAvailableProcessingGeneration
                               != null
                            && availability.LastAvailableProcessingGeneration ==
                               chunk.ProcessingGeneration)
                select new
                {
                    chunk.DocumentId,
                    chunk.Order,
                    chunk.Text
                };

            var candidateRows =
                from document in dbContext.Documents.AsNoTracking()
                join contentMatch in contentMatchesQuery
                    on document.Id equals contentMatch.DocumentId
                    into matchingChunks
                from contentMatch in matchingChunks.DefaultIfEmpty()
                orderby document.Id, contentMatch.Order
                select new
                {
                    document.Id,
                    document.FileName,
                    document.DisplayName,
                    ChunkOrder =
                        contentMatch == null
                            ? null
                            : (int?)contentMatch.Order,
                    ChunkText =
                        contentMatch == null
                            ? null
                            : contentMatch.Text
                };

            int bufferCapacity =
                pageSize == int.MaxValue
                    ? int.MaxValue
                    : pageSize + 1;

            var buffer =
                new PriorityQueue<
                    SearchDocumentsResult,
                    SearchDocumentsRankingKey>(
                    bufferCapacity,
                    Comparer<SearchDocumentsRankingKey>.Create(
                        static (left, right) =>
                            right.CompareTo(left)));

            Guid? currentDocumentId = null;
            string? currentFileName = null;
            string? currentDisplayName = null;
            List<SearchMatch>? currentMatches = null;
            bool currentDocumentIncluded = false;

            async Task FlushCurrentDocumentAsync()
            {
                if (!currentDocumentIncluded ||
                    currentDocumentId is null ||
                    currentFileName is null ||
                    currentDisplayName is null ||
                    currentMatches is null)
                {
                    return;
                }

                if (currentMatches.Count == 0)
                {
                    return;
                }

                SearchDocumentsResult result =
                    new(
                        currentDocumentId.Value,
                        currentFileName,
                        currentDisplayName,
                        currentMatches,
                        currentMatches.Count);

                SearchDocumentsRankingKey rankingKey =
                    _ranker.GetRankingKey(
                        result);

                if (continuationPosition is not null &&
                    rankingKey.CompareTo(
                        continuationPosition.Value) <= 0)
                {
                    return;
                }

                AddCandidate(
                    buffer,
                    result,
                    rankingKey,
                    bufferCapacity);
            }

            await foreach (
                var row in candidateRows.AsAsyncEnumerable()
                    .WithCancellation(cancellationToken))
            {
                if (currentDocumentId != row.Id)
                {
                    await FlushCurrentDocumentAsync();

                    currentDocumentId = row.Id;
                    currentFileName = row.FileName;
                    currentDisplayName = row.DisplayName;
                    currentMatches = [];
                    currentDocumentIncluded =
                        normalizedFileTypes is null ||
                        normalizedFileTypes.Contains(
                            Path.GetExtension(
                                row.FileName),
                            StringComparer.OrdinalIgnoreCase);

                    if (currentDocumentIncluded)
                    {
                        AddMetadataMatch(
                            currentMatches,
                            row.FileName,
                            normalizedSearchText);

                        AddMetadataMatch(
                            currentMatches,
                            row.DisplayName,
                            normalizedSearchText);
                    }
                }

                if (currentDocumentIncluded &&
                    row.ChunkText is not null)
                {
                    string canonicalChunkText =
                        SearchTextCanonicalizer.CanonicalizeValue(
                            row.ChunkText);

                    currentMatches!.Add(
                        new SearchMatch(
                            SearchMatchSource.ProcessedContent,
                            DetermineMatchKind(
                                canonicalChunkText,
                                normalizedSearchText),
                            row.ChunkText));
                }
            }

            await FlushCurrentDocumentAsync();

            return buffer
                .UnorderedItems
                .Select(
                    item => item.Element)
                .OrderBy(
                    result => _ranker.GetRankingKey(result))
                .ToList();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                LogMessages.DocumentSearchStoreFailed);

            throw;
        }
    }

    private static IReadOnlyList<string>? NormalizeFileTypes(
        IReadOnlyList<string>? fileTypes)
    {
        if (fileTypes is null || fileTypes.Count == 0)
        {
            return null;
        }

        string[] normalizedFileTypes =
            fileTypes
                .Where(
                    fileType =>
                        !string.IsNullOrWhiteSpace(
                            fileType))
                .Select(
                    fileType =>
                    {
                        string normalized =
                            fileType.Trim();

                        return normalized.StartsWith(
                            ".",
                            StringComparison.Ordinal)
                            ? normalized.ToLowerInvariant()
                            : $".{normalized.ToLowerInvariant()}";
                    })
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

        return normalizedFileTypes.Length == 0
            ? null
            : normalizedFileTypes;
    }

    private static void AddCandidate(
        PriorityQueue<
            SearchDocumentsResult,
            SearchDocumentsRankingKey> buffer,
        SearchDocumentsResult result,
        SearchDocumentsRankingKey rankingKey,
        int capacity)
    {
        if (buffer.Count < capacity)
        {
            buffer.Enqueue(
                result,
                rankingKey);

            return;
        }

        buffer.TryPeek(
            out _,
            out SearchDocumentsRankingKey worstKey);

        if (rankingKey.CompareTo(
                worstKey) >= 0)
        {
            return;
        }

        buffer.Dequeue();

        buffer.Enqueue(
            result,
            rankingKey);
    }

    private static void AddMetadataMatch(
        List<SearchMatch> matches,
        string value,
        string searchText)
    {
        string canonicalValue =
            SearchTextCanonicalizer.CanonicalizeValue(
                value);

        if (!SearchTextCanonicalizer.ContainsCanonicalized(
                canonicalValue,
                searchText))
        {
            return;
        }

        matches.Add(
            new SearchMatch(
                SearchMatchSource.DocumentMetadata,
                DetermineMatchKind(
                    canonicalValue,
                    searchText),
                value));
    }

    private static string EscapeLikePattern(
        string value)
    {
        return value
            .Replace(
                "\\",
                "\\\\",
                StringComparison.Ordinal)
            .Replace(
                "%",
                "\\%",
                StringComparison.Ordinal)
            .Replace(
                "_",
                "\\_",
                StringComparison.Ordinal);
    }

    private static SearchMatchKind DetermineMatchKind(
        string canonicalValue,
        string canonicalSearchText)
    {
        int matchIndex =
            SearchTextCanonicalizer.IndexOfCanonicalized(
                canonicalValue,
                canonicalSearchText);

        if (matchIndex < 0)
        {
            return SearchMatchKind.Partial;
        }

        bool leftBoundary =
            matchIndex == 0
            || !IsLexicalCharacter(
                canonicalValue[matchIndex - 1]);

        int matchEnd =
            matchIndex + canonicalSearchText.Length;

        bool rightBoundary =
            matchEnd == canonicalValue.Length
            || !IsLexicalCharacter(
                canonicalValue[matchEnd]);

        return leftBoundary && rightBoundary
            ? SearchMatchKind.Exact
            : SearchMatchKind.Partial;
    }

    private static bool IsLexicalCharacter(
        char character)
    {
        return char.IsLetterOrDigit(character)
            || character == '_';
    }
}
