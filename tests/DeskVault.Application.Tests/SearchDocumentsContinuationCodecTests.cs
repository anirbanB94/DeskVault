using DeskVault.Application.Documents.Queries.SearchDocuments;

namespace DeskVault.Application.Tests;

public sealed class SearchDocumentsContinuationCodecTests
{
    private readonly SearchDocumentsContinuationCodec _codec = new();

    [Fact]
    public void Create_AndDecode_PreservesRankingPosition()
    {
        // Arrange
        SearchDocumentsQuery query =
            CreateQuery();

        SearchDocumentsRankingKey rankingKey =
            CreateRankingKey();

        // Act
        SearchDocumentsContinuation continuation =
            _codec.Create(
                query,
                rankingKey);

        SearchDocumentsRankingKey decoded =
            _codec.Decode(
                query,
                continuation);

        // Assert
        Assert.Equal(
            rankingKey,
            decoded);

        Assert.DoesNotMatch(
            "^\\d+$",
            continuation.Value);
    }

    [Fact]
    public void Create_WithDifferentPageSize_ProducesSameContinuation()
    {
        // Arrange
        SearchDocumentsQuery firstQuery =
            CreateQuery(
                limit: 10);

        SearchDocumentsQuery secondQuery =
            CreateQuery(
                limit: 50);

        SearchDocumentsRankingKey rankingKey =
            CreateRankingKey();

        // Act
        SearchDocumentsContinuation firstContinuation =
            _codec.Create(
                firstQuery,
                rankingKey);

        SearchDocumentsContinuation secondContinuation =
            _codec.Create(
                secondQuery,
                rankingKey);

        // Assert
        Assert.Equal(
            firstContinuation.Value,
            secondContinuation.Value);
    }

    [Fact]
    public void Decode_WhenSearchTextChanges_RejectsContinuation()
    {
        // Arrange
        SearchDocumentsQuery originalQuery =
            CreateQuery(
                searchText: "security");

        SearchDocumentsQuery changedQuery =
            CreateQuery(
                searchText: "incident");

        SearchDocumentsContinuation continuation =
            _codec.Create(
                originalQuery,
                CreateRankingKey());

        // Act
        Action act =
            () =>
                _codec.Decode(
                    changedQuery,
                    continuation);

        // Assert
        Assert.Throws<ArgumentException>(
            act);
    }

    [Fact]
    public void Decode_WhenFileTypesChange_RejectsContinuation()
    {
        // Arrange
        SearchDocumentsQuery originalQuery =
            CreateQuery(
                fileTypes: ["md"]);

        SearchDocumentsQuery changedQuery =
            CreateQuery(
                fileTypes: ["txt"]);

        SearchDocumentsContinuation continuation =
            _codec.Create(
                originalQuery,
                CreateRankingKey());

        // Act
        Action act =
            () =>
                _codec.Decode(
                    changedQuery,
                    continuation);

        // Assert
        Assert.Throws<ArgumentException>(
            act);
    }

    [Fact]
    public void Decode_WhenEquivalentFileTypesUseDifferentCaseAndOrder_AcceptsContinuation()
    {
        // Arrange
        SearchDocumentsQuery originalQuery =
            CreateQuery(
                fileTypes: ["md", "TXT"]);

        SearchDocumentsQuery equivalentQuery =
            CreateQuery(
                fileTypes: ["txt", ".MD"]);

        SearchDocumentsContinuation continuation =
            _codec.Create(
                originalQuery,
                CreateRankingKey());

        // Act
        SearchDocumentsRankingKey decoded =
            _codec.Decode(
                equivalentQuery,
                continuation);

        // Assert
        Assert.Equal(
            CreateRankingKey(),
            decoded);
    }

    [Fact]
    public void Decode_WhenContinuationIsMalformed_RejectsContinuation()
    {
        // Arrange
        SearchDocumentsQuery query =
            CreateQuery();

        SearchDocumentsContinuation continuation =
            new("not-a-valid-continuation");

        // Act
        Action act =
            () =>
                _codec.Decode(
                    query,
                    continuation);

        // Assert
        Assert.Throws<ArgumentException>(
            act);
    }

    [Fact]
    public void Decode_WhenContinuationIsEmpty_RejectsContinuation()
    {
        // Arrange
        SearchDocumentsQuery query =
            CreateQuery();

        SearchDocumentsContinuation continuation =
            new("");

        // Act
        Action act =
            () =>
                _codec.Decode(
                    query,
                    continuation);

        // Assert
        Assert.Throws<ArgumentException>(
            act);
    }

    private static SearchDocumentsQuery CreateQuery(
        string searchText = "security",
        IReadOnlyList<string>? fileTypes = null,
        int limit = 20)
    {
        return new SearchDocumentsQuery(
            searchText,
            fileTypes,
            Limit: limit);
    }

    private static SearchDocumentsRankingKey CreateRankingKey()
    {
        return new SearchDocumentsRankingKey(
            100,
            "Security Policy",
            Guid.Parse(
                "00000000-0000-0000-0000-000000000001"));
    }
}
