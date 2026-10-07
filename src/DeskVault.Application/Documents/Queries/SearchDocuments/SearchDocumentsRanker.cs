using DeskVault.Application.Interfaces;

namespace DeskVault.Application.Documents.Queries.SearchDocuments;

public sealed class SearchDocumentsRanker : ISearchDocumentsRanker
{
    private const int ExactMetadataScore = 100;
    private const int ExactContentScore = 60;
    private const int PartialMetadataScore = 40;
    private const int PartialContentScore = 20;

    public IReadOnlyList<SearchDocumentsResult> Rank(
        IReadOnlyList<SearchDocumentsResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        return results
            .Select(
                result =>
                    new
                    {
                        Result = result,
                        RankingKey = GetRankingKey(result)
                    })
            .OrderBy(
                item => item.RankingKey)
            .Select(
                item => item.Result)
            .ToList();
    }

    public SearchDocumentsRankingKey GetRankingKey(
        SearchDocumentsResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new SearchDocumentsRankingKey(
            CalculateScore(result),
            result.DisplayName,
            result.DocumentId);
    }

    private static int CalculateScore(
        SearchDocumentsResult result)
    {
        return result.Matches.Sum(
            match =>
                match.Source switch
                {
                    SearchMatchSource.DocumentMetadata =>
                        match.Kind == SearchMatchKind.Exact
                            ? ExactMetadataScore
                            : PartialMetadataScore,

                    SearchMatchSource.ProcessedContent =>
                        match.Kind == SearchMatchKind.Exact
                            ? ExactContentScore
                            : PartialContentScore,

                    _ => 0
                });
    }
}
