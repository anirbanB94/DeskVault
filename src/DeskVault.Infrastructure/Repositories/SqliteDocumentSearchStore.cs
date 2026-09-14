using DeskVault.Application.Documents.Queries.SearchDocuments;
using DeskVault.Application.Interfaces;
using DeskVault.Infrastructure.Persistence.Context;
using DeskVault.Shared.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DeskVault.Infrastructure.Repositories;

public sealed class SqliteDocumentSearchStore
    : IDocumentSearchStore
{
    private readonly IDbContextFactory<DeskVaultDbContext> _dbContextFactory;
    private readonly ILogger<SqliteDocumentSearchStore> _logger;

    public SqliteDocumentSearchStore(
        IDbContextFactory<DeskVaultDbContext> dbContextFactory,
        ILogger<SqliteDocumentSearchStore> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SearchDocumentsResult>> SearchAsync(
        SearchDocumentsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentException.ThrowIfNullOrWhiteSpace(query.SearchText);

        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation(
            LogMessages.DocumentSearchStoreStarted);

        try
        {
            await using var dbContext =
                await _dbContextFactory.CreateDbContextAsync(
                    cancellationToken);

            string normalizedSearchText =
                query.SearchText.Trim();

            IReadOnlyList<string>? normalizedFileTypes =
                NormalizeFileTypes(
                    query.FileTypes);

            var documents =
                await dbContext.Documents
                    .AsNoTracking()
                    .Select(
                        document =>
                            new
                            {
                                document.Id,
                                document.FileName,
                                document.DisplayName,
                                document.LastSuccessfulProcessingGeneration
                            })
                    .ToListAsync(
                        cancellationToken);

            if (normalizedFileTypes is not null)
            {
                documents =
                    documents
                        .Where(
                            document =>
                                normalizedFileTypes.Contains(
                                    Path.GetExtension(
                                        document.FileName),
                                    StringComparer.OrdinalIgnoreCase))
                        .ToList();
            }

            var documentMatches =
                documents.ToDictionary(
                    document => document.Id,
                    _ => new List<SearchMatch>());

            foreach (var document in documents)
            {
                AddMetadataMatch(
                    documentMatches[document.Id],
                    document.FileName,
                    normalizedSearchText);

                AddMetadataMatch(
                    documentMatches[document.Id],
                    document.DisplayName,
                    normalizedSearchText);
            }

            Guid[] eligibleDocumentIds =
                documents
                    .Select(
                        document =>
                            document.Id)
                    .ToArray();

            string escapedSearchText =
                EscapeLikePattern(
                    normalizedSearchText);

            var contentMatches =
                eligibleDocumentIds.Length == 0
                    ? []
                    : await dbContext.DocumentChunks
                        .AsNoTracking()
                        .Where(
                            chunk =>
                                eligibleDocumentIds.Contains(
                                    chunk.DocumentId)
                                && EF.Functions.Like(
                                    chunk.Text,
                                    $"%{escapedSearchText}%",
                                    "\\"))
                        .Join(
                            dbContext.Documents,
                            chunk => chunk.DocumentId,
                            document => document.Id,
                            (chunk, document) =>
                                new
                                {
                                    chunk.DocumentId,
                                    chunk.Order,
                                    chunk.Text,
                                    chunk.ProcessingGeneration,
                                    document.LastSuccessfulProcessingGeneration
                                })
                        .Where(
                            match =>
                                match.ProcessingGeneration
                                == match.LastSuccessfulProcessingGeneration)
                        .OrderBy(
                            match => match.DocumentId)
                        .ThenBy(
                            match => match.Order)
                        .ToListAsync(
                            cancellationToken);

            foreach (var contentMatch in contentMatches)
            {
                documentMatches[contentMatch.DocumentId].Add(
                    new SearchMatch(
                        SearchMatchSource.ProcessedContent,
                        DetermineMatchKind(
                            contentMatch.Text,
                            normalizedSearchText),
                        contentMatch.Text));
            }

            var results =
                documents
                    .Where(
                        document =>
                            documentMatches[document.Id].Count > 0)
                    .OrderBy(
                        document => document.DisplayName)
                    .ThenBy(
                        document => document.Id)
                    .Select(
                        document =>
                        {
                            List<SearchMatch> matches =
                                documentMatches[document.Id];

                            return new SearchDocumentsResult(
                                document.Id,
                                document.FileName,
                                document.DisplayName,
                                matches,
                                matches.Count);
                        })
                    .ToList();

            _logger.LogInformation(
                LogMessages.DocumentSearchStoreCompleted,
                results.Count);

            return results;
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

    private static void AddMetadataMatch(
        List<SearchMatch> matches,
        string value,
        string searchText)
    {
        if (!value.Contains(
                searchText,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        matches.Add(
            new SearchMatch(
                SearchMatchSource.DocumentMetadata,
                DetermineMatchKind(
                    value,
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
        string value,
        string searchText)
    {
        int matchIndex =
            value.IndexOf(
                searchText,
                StringComparison.OrdinalIgnoreCase);

        if (matchIndex < 0)
        {
            return SearchMatchKind.Partial;
        }

        bool leftBoundary =
            matchIndex == 0
            || !IsLexicalCharacter(
                value[matchIndex - 1]);

        int matchEnd =
            matchIndex + searchText.Length;

        bool rightBoundary =
            matchEnd == value.Length
            || !IsLexicalCharacter(
                value[matchEnd]);

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
