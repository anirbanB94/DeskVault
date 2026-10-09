using DeskVault.Application.Documents.Queries.SearchDocuments;
using System.Text;
using System.Text.Json;

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

    [Fact]
    public void Create_WhenSearchTextIsCanonicallyEquivalent_ProducesSameContinuation()
    {
        // Arrange
        SearchDocumentsQuery composedQuery =
            CreateQuery(
                searchText: "café");

        SearchDocumentsQuery decomposedQuery =
            CreateQuery(
                searchText: "cafe\u0301");

        SearchDocumentsRankingKey rankingKey =
            CreateRankingKey();

        // Act
        SearchDocumentsContinuation composedContinuation =
            _codec.Create(
                composedQuery,
                rankingKey);

        SearchDocumentsContinuation decomposedContinuation =
            _codec.Create(
                decomposedQuery,
                rankingKey);

        // Assert
        Assert.Equal(
            composedContinuation.Value,
            decomposedContinuation.Value);
    }

    [Fact]
    public void Decode_WhenSearchTextIsCanonicallyEquivalent_AcceptsContinuation()
    {
        // Arrange
        SearchDocumentsQuery originalQuery =
            CreateQuery(
                searchText: "café");

        SearchDocumentsQuery equivalentQuery =
            CreateQuery(
                searchText: "cafe\u0301");

        SearchDocumentsRankingKey rankingKey =
            CreateRankingKey();

        SearchDocumentsContinuation continuation =
            _codec.Create(
                originalQuery,
                rankingKey);

        // Act
        SearchDocumentsRankingKey decoded =
            _codec.Decode(
                equivalentQuery,
                continuation);

        // Assert
        Assert.Equal(
            rankingKey,
            decoded);
    }

    [Fact]
    public void Decode_WhenContinuationUsesPreviousRankingContractVersion_RejectsContinuation()
    {
        // Arrange
        SearchDocumentsQuery query =
            CreateQuery(
                searchText: "café");

        SearchDocumentsContinuation continuation =
            CreatePreviousRankingContractContinuation();

        // Act
        Action act =
            () =>
                _codec.Decode(
                    query,
                    continuation);

        // Assert
        ArgumentException exception =
            Assert.Throws<ArgumentException>(
                act);

        Assert.Contains(
            "Continuation ranking contract version is not supported.",
            exception.Message);
    }

    private static SearchDocumentsContinuation CreatePreviousRankingContractContinuation()
    {
        string json =
            JsonSerializer.Serialize(
                new
                {
                    FormatVersion = 1,
                    RankingContractVersion = 1,
                    CriteriaFingerprint = "legacy",
                    RelevanceScore = 100,
                    DisplayName = "Security Policy",
                    DocumentId = Guid.Parse(
                        "00000000-0000-0000-0000-000000000001")
                });

        string value =
            Convert.ToBase64String(
                Encoding.UTF8.GetBytes(
                    json))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        return new SearchDocumentsContinuation(
            value);
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
