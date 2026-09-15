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
                        Score = CalculateScore(result)
                    })
            .OrderByDescending(
                item => item.Score)
            .ThenBy(
                item => item.Result.DisplayName,
                StringComparer.Ordinal)
            .ThenBy(
                item => item.Result.DocumentId)
            .Select(
                item => item.Result)
            .ToList();
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
