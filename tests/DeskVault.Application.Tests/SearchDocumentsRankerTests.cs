using DeskVault.Application.Documents.Queries.SearchDocuments;

namespace DeskVault.Application.Tests;

public sealed class SearchDocumentsRankerTests
{
    private readonly SearchDocumentsRanker _ranker = new();

    [Fact]
    public void Rank_WhenExactMetadataAndExactContentMatchExist_RanksMetadataFirst()
    {
        Guid metadataId = CreateDocumentId(1);
        Guid contentId = CreateDocumentId(2);

        // Arrange
        IReadOnlyList<SearchDocumentsResult> results =
        [
            CreateResult(
                contentId,
                "Content Match",
                CreateMatch(
                    SearchMatchSource.ProcessedContent,
                    SearchMatchKind.Exact)),

            CreateResult(
                metadataId,
                "Metadata Match",
                CreateMatch(
                    SearchMatchSource.DocumentMetadata,
                    SearchMatchKind.Exact))
        ];

        // Act
        IReadOnlyList<SearchDocumentsResult> ranked =
            _ranker.Rank(results);

        // Assert
        Assert.Collection(
            ranked,
            result => Assert.Equal(metadataId, result.DocumentId),
            result => Assert.Equal(contentId, result.DocumentId));
    }

    [Fact]
    public void Rank_WhenExactAndPartialMatchesExist_RanksExactFirst()
    {
        Guid exactId = CreateDocumentId(1);
        Guid partialId = CreateDocumentId(2);

        // Arrange
        IReadOnlyList<SearchDocumentsResult> results =
        [
            CreateResult(
                partialId,
                "Partial Match",
                CreateMatch(
                    SearchMatchSource.DocumentMetadata,
                    SearchMatchKind.Partial)),

            CreateResult(
                exactId,
                "Exact Match",
                CreateMatch(
                    SearchMatchSource.ProcessedContent,
                    SearchMatchKind.Exact))
        ];

        // Act
        IReadOnlyList<SearchDocumentsResult> ranked =
            _ranker.Rank(results);

        // Assert
        Assert.Collection(
            ranked,
            result => Assert.Equal(exactId, result.DocumentId),
            result => Assert.Equal(partialId, result.DocumentId));
    }

    [Fact]
    public void Rank_WhenMultipleMatchesExist_CombinesTheirScores()
    {
        Guid multipleMatchId = CreateDocumentId(1);
        Guid singleMatchId = CreateDocumentId(2);

        // Arrange
        IReadOnlyList<SearchDocumentsResult> results =
        [
            CreateResult(
                singleMatchId,
                "Single Match",
                CreateMatch(
                    SearchMatchSource.DocumentMetadata,
                    SearchMatchKind.Exact)),

            CreateResult(
                multipleMatchId,
                "Multiple Matches",
                CreateMatch(
                    SearchMatchSource.ProcessedContent,
                    SearchMatchKind.Exact),
                CreateMatch(
                    SearchMatchSource.ProcessedContent,
                    SearchMatchKind.Exact))
        ];

        // Act
        IReadOnlyList<SearchDocumentsResult> ranked =
            _ranker.Rank(results);

        // Assert
        Assert.Collection(
            ranked,
            result => Assert.Equal(multipleMatchId, result.DocumentId),
            result => Assert.Equal(singleMatchId, result.DocumentId));
    }

    [Fact]
    public void Rank_WhenMetadataIsWeakerButHasEnoughAdditionalMatches_RanksByCombinedScore()
    {
        Guid strongerCombinedId = CreateDocumentId(1);
        Guid singleExactMetadataId = CreateDocumentId(2);

        // Arrange
        IReadOnlyList<SearchDocumentsResult> results =
        [
            CreateResult(
                singleExactMetadataId,
                "Single Exact Metadata",
                CreateMatch(
                    SearchMatchSource.DocumentMetadata,
                    SearchMatchKind.Exact)),

            CreateResult(
                strongerCombinedId,
                "Multiple Exact Content",
                CreateMatch(
                    SearchMatchSource.ProcessedContent,
                    SearchMatchKind.Exact),
                CreateMatch(
                    SearchMatchSource.ProcessedContent,
                    SearchMatchKind.Exact))
        ];

        // Act
        IReadOnlyList<SearchDocumentsResult> ranked =
            _ranker.Rank(results);

        // Assert
        Assert.Collection(
            ranked,
            result => Assert.Equal(
                strongerCombinedId,
                result.DocumentId),
            result => Assert.Equal(
                singleExactMetadataId,
                result.DocumentId));
    }

    [Fact]
    public void Rank_WhenScoresAreEquivalent_UsesDisplayNameAsTieBreaker()
    {
        Guid alphaId = CreateDocumentId(1);
        Guid zetaId = CreateDocumentId(2);

        // Arrange
        IReadOnlyList<SearchDocumentsResult> results =
        [
            CreateResult(
                zetaId,
                "Zeta Document",
                CreateMatch(
                    SearchMatchSource.ProcessedContent,
                    SearchMatchKind.Exact)),

            CreateResult(
                alphaId,
                "Alpha Document",
                CreateMatch(
                    SearchMatchSource.ProcessedContent,
                    SearchMatchKind.Exact))
        ];

        // Act
        IReadOnlyList<SearchDocumentsResult> ranked =
            _ranker.Rank(results);

        // Assert
        Assert.Collection(
            ranked,
            result => Assert.Equal(alphaId, result.DocumentId),
            result => Assert.Equal(zetaId, result.DocumentId));
    }

    [Fact]
    public void Rank_WhenDisplayNamesAreEquivalent_UsesDocumentIdAsTieBreaker()
    {
        Guid firstId = CreateDocumentId(1);
        Guid secondId = CreateDocumentId(2);

        // Arrange
        IReadOnlyList<SearchDocumentsResult> results =
        [
            CreateResult(
                secondId,
                "Same Name",
                CreateMatch(
                    SearchMatchSource.ProcessedContent,
                    SearchMatchKind.Exact)),

            CreateResult(
                firstId,
                "Same Name",
                CreateMatch(
                    SearchMatchSource.ProcessedContent,
                    SearchMatchKind.Exact))
        ];

        // Act
        IReadOnlyList<SearchDocumentsResult> ranked =
            _ranker.Rank(results);

        // Assert
        Assert.Collection(
            ranked,
            result => Assert.Equal(firstId, result.DocumentId),
            result => Assert.Equal(secondId, result.DocumentId));
    }

    [Fact]
    public void Rank_WhenInputIsEmpty_ReturnsEmptyResult()
    {
        // Arrange
        IReadOnlyList<SearchDocumentsResult> results = [];

        // Act
        IReadOnlyList<SearchDocumentsResult> ranked =
            _ranker.Rank(results);

        // Assert
        Assert.Empty(ranked);
    }

    [Fact]
    public void Rank_WhenInputIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        IReadOnlyList<SearchDocumentsResult> results = null!;

        // Act
        Action act = () => _ranker.Rank(results);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void Rank_WhenPartialMetadataAndPartialContentMatchExist_RanksMetadataFirst()
    {
        Guid metadataId = CreateDocumentId(1);
        Guid contentId = CreateDocumentId(2);

        // Arrange
        IReadOnlyList<SearchDocumentsResult> results =
        [
            CreateResult(
                contentId,
                "Partial Content",
                CreateMatch(
                    SearchMatchSource.ProcessedContent,
                    SearchMatchKind.Partial)),

            CreateResult(
                metadataId,
                "Partial Metadata",
                CreateMatch(
                    SearchMatchSource.DocumentMetadata,
                    SearchMatchKind.Partial))
        ];

        // Act
        IReadOnlyList<SearchDocumentsResult> ranked =
            _ranker.Rank(results);

        // Assert
        Assert.Collection(
            ranked,
            result => Assert.Equal(metadataId, result.DocumentId),
            result => Assert.Equal(contentId, result.DocumentId));
    }

    [Fact]
    public void Rank_WhenExactContentAndPartialMetadataMatchExist_RanksExactContentFirst()
    {
        Guid exactContentId = CreateDocumentId(1);
        Guid partialMetadataId = CreateDocumentId(2);

        // Arrange
        IReadOnlyList<SearchDocumentsResult> results =
        [
            CreateResult(
                partialMetadataId,
                "Partial Metadata",
                CreateMatch(
                    SearchMatchSource.DocumentMetadata,
                    SearchMatchKind.Partial)),

            CreateResult(
                exactContentId,
                "Exact Content",
                CreateMatch(
                    SearchMatchSource.ProcessedContent,
                    SearchMatchKind.Exact))
        ];

        // Act
        IReadOnlyList<SearchDocumentsResult> ranked =
            _ranker.Rank(results);

        // Assert
        Assert.Collection(
            ranked,
            result => Assert.Equal(exactContentId, result.DocumentId),
            result => Assert.Equal(partialMetadataId, result.DocumentId));
    }

    [Fact]
    public void Rank_WhenPartialContentMatchesAccumulate_CombinesTheirScores()
    {
        Guid multipleMatchId = CreateDocumentId(1);
        Guid singleMatchId = CreateDocumentId(2);

        // Arrange
        IReadOnlyList<SearchDocumentsResult> results =
        [
            CreateResult(
                singleMatchId,
                "Single Match",
                CreateMatch(
                    SearchMatchSource.DocumentMetadata,
                    SearchMatchKind.Partial)),

            CreateResult(
                multipleMatchId,
                "Multiple Matches",
                CreateMatch(
                    SearchMatchSource.ProcessedContent,
                    SearchMatchKind.Partial),
                CreateMatch(
                    SearchMatchSource.ProcessedContent,
                    SearchMatchKind.Partial),
                CreateMatch(
                    SearchMatchSource.ProcessedContent,
                    SearchMatchKind.Partial))
        ];

        // Act
        IReadOnlyList<SearchDocumentsResult> ranked =
            _ranker.Rank(results);

        // Assert
        Assert.Collection(
            ranked,
            result => Assert.Equal(multipleMatchId, result.DocumentId),
            result => Assert.Equal(singleMatchId, result.DocumentId));
    }

    private static SearchMatch CreateMatch(
        SearchMatchSource source,
        SearchMatchKind kind)
    {
        return new SearchMatch(
            source,
            kind,
            "matching context");
    }

    private static SearchDocumentsResult CreateResult(
        Guid documentId,
        string displayName,
        params SearchMatch[] matches)
    {
        return new SearchDocumentsResult(
            documentId,
            $"{displayName}.txt",
            displayName,
            matches,
            matches.Length);
    }

    private static Guid CreateDocumentId(int value)
    {
        return Guid.Parse(
            $"00000000-0000-0000-0000-{value:D12}");
    }
}
