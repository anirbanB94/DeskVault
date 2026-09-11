using DeskVault.Application.Documents.Chunking;

namespace DeskVault.Application.Tests;

public sealed class DocumentChunkIdentityTests
{
    private static readonly Guid DocumentId =
        Guid.Parse(
            "11111111-2222-3333-4444-555555555555");

    private const int ChunkOrder = 3;

    [Fact]
    public void CreateLogicalId_IsDeterministicForSameDocumentAndOrder()
    {
        // Arrange
        Guid documentId = DocumentId;
        int order = ChunkOrder;

        // Act
        Guid first =
            DocumentChunkIdentity.CreateLogicalId(
                documentId,
                order);

        Guid second =
            DocumentChunkIdentity.CreateLogicalId(
                documentId,
                order);

        // Assert
        Assert.Equal(
            first,
            second);
    }

    [Fact]
    public void CreateLogicalId_DiffersForDifferentDocuments()
    {
        // Arrange
        Guid firstDocumentId =
            DocumentId;

        Guid secondDocumentId =
            Guid.Parse(
                "66666666-7777-8888-9999-000000000000");

        // Act
        Guid first =
            DocumentChunkIdentity.CreateLogicalId(
                firstDocumentId,
                ChunkOrder);

        Guid second =
            DocumentChunkIdentity.CreateLogicalId(
                secondDocumentId,
                ChunkOrder);

        // Assert
        Assert.NotEqual(
            first,
            second);
    }

    [Fact]
    public void CreateLogicalId_DiffersForDifferentOrders()
    {
        // Arrange
        int firstOrder = 0;
        int secondOrder = 1;

        // Act
        Guid first =
            DocumentChunkIdentity.CreateLogicalId(
                DocumentId,
                firstOrder);

        Guid second =
            DocumentChunkIdentity.CreateLogicalId(
                DocumentId,
                secondOrder);

        // Assert
        Assert.NotEqual(
            first,
            second);
    }

    [Fact]
    public void CreateLogicalId_IsIndependentOfChunkText()
    {
        // Arrange
        Guid documentId = DocumentId;
        int order = ChunkOrder;

        const string firstText =
            "Original canonical chunk text.";

        const string secondText =
            "Changed canonical chunk text.";

        // Act
        Guid first =
            DocumentChunkIdentity.CreateLogicalId(
                documentId,
                order);

        Guid second =
            DocumentChunkIdentity.CreateLogicalId(
                documentId,
                order);

        string firstContentHash =
            DocumentChunkIdentity.ComputeContentHash(
                firstText);

        string secondContentHash =
            DocumentChunkIdentity.ComputeContentHash(
                secondText);

        // Assert
        Assert.Equal(
            first,
            second);

        Assert.NotEqual(
            firstContentHash,
            secondContentHash);
    }

    [Fact]
    public void ComputeContentHash_IsDeterministicForSameText()
    {
        // Arrange
        const string text =
            "Canonical chunk text.";

        // Act
        string first =
            DocumentChunkIdentity.ComputeContentHash(
                text);

        string second =
            DocumentChunkIdentity.ComputeContentHash(
                text);

        // Assert
        Assert.Equal(
            first,
            second);
    }

    [Fact]
    public void ComputeContentHash_DiffersForDifferentText()
    {
        // Arrange
        const string firstText =
            "Canonical chunk text.";

        const string secondText =
            "Changed canonical chunk text.";

        // Act
        string first =
            DocumentChunkIdentity.ComputeContentHash(
                firstText);

        string second =
            DocumentChunkIdentity.ComputeContentHash(
                secondText);

        // Assert
        Assert.NotEqual(
            first,
            second);
    }

    [Fact]
    public void ComputeContentHash_ReturnsLowercaseSha256Hex()
    {
        // Arrange
        const string text =
            "Canonical chunk text.";

        // Act
        string hash =
            DocumentChunkIdentity.ComputeContentHash(
                text);

        // Assert
        Assert.Equal(
            64,
            hash.Length);

        Assert.Equal(
            hash.ToLowerInvariant(),
            hash);

        Assert.All(
            hash,
            character =>
                Assert.True(
                    char.IsAsciiHexDigit(
                        character)));
    }
}
