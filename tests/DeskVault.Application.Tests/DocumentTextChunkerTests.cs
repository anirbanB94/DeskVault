using DeskVault.Application.Documents.Chunking;
using DeskVault.Application.Documents.Normalization;
using DeskVault.Application.Documents.Provenance;

namespace DeskVault.Application.Tests;

public sealed class DocumentTextChunkerTests
{
    private const int DefaultMaxChunkSize = 1000;
    private const int SmallMaxChunkSize = 20;

    [Fact]
    public async Task ChunkAsync_EmptyText_ReturnsNoChunks()
    {
        IReadOnlyList<DocumentChunk> chunks =
            await ChunkAsync(string.Empty);

        Assert.Empty(chunks);
    }

    [Fact]
    public async Task ChunkAsync_SingleParagraph_ReturnsSingleChunk()
    {
        const string text =
            "DeskVault keeps documents searchable.";

        IReadOnlyList<DocumentChunk> chunks =
            await ChunkAsync(text);

        DocumentChunk chunk =
            Assert.Single(chunks);

        Assert.Equal(0, chunk.Order);
        Assert.Equal(text, chunk.Text);
    }

    [Fact]
    public async Task ChunkAsync_DefaultSourceLocation_IsUnknown()
    {
        IReadOnlyList<DocumentChunk> chunks =
            await ChunkAsync(
                "DeskVault keeps documents searchable.");

        DocumentChunk chunk =
            Assert.Single(chunks);

        Assert.Null(
            chunk.SourceLocation);
    }

    [Fact]
    public async Task ChunkAsync_DirectTextMapping_AssignsSingleLineSourceLocation()
    {
        var normalizationResult =
            new DocumentTextNormalizationResult(
                "DeskVault searchable text.",
                DocumentSourceLocationMappingKind.DirectText);

        IReadOnlyList<DocumentChunk> chunks =
            await CreateChunker().ChunkAsync(
                normalizationResult);

        DocumentChunk chunk =
            Assert.Single(chunks);

        Assert.Equal(
            new DocumentSourceLocation(
                1,
                1),
            chunk.SourceLocation);
    }

    [Fact]
    public async Task ChunkAsync_DirectTextMapping_TracksMultilineSourceLocation()
    {
        var normalizationResult =
            new DocumentTextNormalizationResult(
                "First line.\nSecond line.\nThird line.",
                DocumentSourceLocationMappingKind.DirectText);

        IReadOnlyList<DocumentChunk> chunks =
            await CreateChunker().ChunkAsync(
                normalizationResult);

        DocumentChunk chunk =
            Assert.Single(chunks);

        Assert.Equal(
            new DocumentSourceLocation(
                1,
                3),
            chunk.SourceLocation);
    }

    [Fact]
    public async Task ChunkAsync_DirectTextMapping_TracksParagraphSourceLocations()
    {
        const string firstParagraph =
            "First paragraph.";

        const string secondParagraph =
            "Second paragraph.";

        string text =
            $"{firstParagraph}\n\n{secondParagraph}";

        var normalizationResult =
            new DocumentTextNormalizationResult(
                text,
                DocumentSourceLocationMappingKind.DirectText);

        IReadOnlyList<DocumentChunk> chunks =
            await CreateChunker(
                secondParagraph.Length)
            .ChunkAsync(
                normalizationResult);

        Assert.Equal(
            2,
            chunks.Count);

        Assert.Equal(
            new DocumentSourceLocation(
                1,
                1),
            chunks[0].SourceLocation);

        Assert.Equal(
            new DocumentSourceLocation(
                3,
                3),
            chunks[1].SourceLocation);
    }

    [Fact]
    public async Task ChunkAsync_UnknownMapping_DoesNotAssignSourceLocation()
    {
        var normalizationResult =
            new DocumentTextNormalizationResult(
                "Generated representation.\nSecond line.",
                DocumentSourceLocationMappingKind.Unknown);

        IReadOnlyList<DocumentChunk> chunks =
            await CreateChunker().ChunkAsync(
                normalizationResult);

        DocumentChunk chunk =
            Assert.Single(chunks);

        Assert.Null(
            chunk.SourceLocation);
    }

    [Fact]
    public async Task ChunkAsync_DirectTextMapping_OversizedParagraph_PreservesSourceLineRange()
    {
        const string text =
            "First line of a long paragraph.\n" +
            "Second line of a long paragraph.";

        var normalizationResult =
            new DocumentTextNormalizationResult(
                text,
                DocumentSourceLocationMappingKind.DirectText);

        IReadOnlyList<DocumentChunk> chunks =
            await CreateChunker(
                SmallMaxChunkSize)
            .ChunkAsync(
                normalizationResult);

        Assert.True(
            chunks.Count > 1);

        Assert.All(
            chunks,
            chunk =>
                Assert.NotNull(
                    chunk.SourceLocation));

        Assert.Equal(
            1,
            chunks[0].SourceLocation!.StartLine);

        Assert.True(
            chunks.All(
                chunk =>
                    chunk.SourceLocation!.StartLine >= 1 &&
                    chunk.SourceLocation!.EndLine <= 2));

        Assert.Contains(
            chunks,
            chunk =>
                chunk.SourceLocation!.EndLine == 2);
    }

    [Fact]
    public async Task ChunkAsync_ParagraphsWithinLimit_ReturnsSingleChunk()
    {
        const string text =
            """
            First paragraph.

            Second paragraph.

            Third paragraph.
            """;

        IReadOnlyList<DocumentChunk> chunks =
            await ChunkAsync(text);

        DocumentChunk chunk =
            Assert.Single(chunks);

        Assert.Equal(text, chunk.Text);
    }

    [Fact]
    public async Task ChunkAsync_ParagraphsExceedLimit_PreservesParagraphBoundaries()
    {
        const string firstParagraph =
            "First paragraph.";

        const string secondParagraph =
            "Second paragraph.";

        string text =
            $"{firstParagraph}\n\n{secondParagraph}";

        IReadOnlyList<DocumentChunk> chunks =
            await ChunkAsync(
                text,
                secondParagraph.Length);

        Assert.Equal(2, chunks.Count);

        Assert.Equal(
            [0, 1],
            chunks.Select(chunk => chunk.Order).ToArray());

        Assert.Equal(
            [firstParagraph, secondParagraph],
            chunks.Select(chunk => chunk.Text).ToArray());
    }

    [Fact]
    public async Task ChunkAsync_OversizedParagraph_SplitsIntoBoundedChunks()
    {
        IReadOnlyList<DocumentChunk> chunks =
            await ChunkAsync(
                OversizedText,
                SmallMaxChunkSize);

        Assert.True(chunks.Count > 1);

        Assert.All(
            chunks,
            chunk =>
                Assert.True(
                    chunk.Text.Length <= SmallMaxChunkSize));
    }

    [Fact]
    public async Task ChunkAsync_OversizedParagraph_PreservesEveryCharacter()
    {
        IReadOnlyList<DocumentChunk> chunks =
            await ChunkAsync(
                OversizedText,
                SmallMaxChunkSize);

        string combined =
            string.Concat(
                chunks.Select(chunk => chunk.Text));

        string expected =
            OversizedText.Replace(
                " ",
                string.Empty,
                StringComparison.Ordinal);

        string actual =
            combined.Replace(
                " ",
                string.Empty,
                StringComparison.Ordinal);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task ChunkAsync_OversizedParagraph_ChunksAreOrdered()
    {
        IReadOnlyList<DocumentChunk> chunks =
            await ChunkAsync(
                OversizedText,
                SmallMaxChunkSize);

        Assert.Equal(
            Enumerable.Range(0, chunks.Count),
            chunks.Select(chunk => chunk.Order));
    }

    [Fact]
    public async Task ChunkAsync_DoesNotCreateEmptyChunks()
    {
        const string text =
            """
            First paragraph.


            Second paragraph.
            """;

        IReadOnlyList<DocumentChunk> chunks =
            await ChunkAsync(text);

        Assert.NotEmpty(chunks);

        Assert.All(
            chunks,
            chunk =>
                Assert.False(
                    string.IsNullOrEmpty(chunk.Text)));
    }

    [Fact]
    public async Task ChunkAsync_IsDeterministic()
    {
        const string text =
            """
            First paragraph.

            Second paragraph.

            Third paragraph.
            """;

        var normalizationResult =
            CreateNormalizationResult(text);

        var chunker =
            CreateChunker(SmallMaxChunkSize);

        IReadOnlyList<DocumentChunk> first =
            await chunker.ChunkAsync(normalizationResult);

        IReadOnlyList<DocumentChunk> second =
            await chunker.ChunkAsync(normalizationResult);

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task ChunkAsync_WhenCancellationRequested_ThrowsOperationCanceledException()
    {
        var normalizationResult =
            CreateNormalizationResult(
                "Cancellation test.");

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                CreateChunker().ChunkAsync(
                    normalizationResult,
                    cancellationTokenSource.Token));
    }

    [Fact]
    public async Task ChunkAsync_OversizedParagraph_PreservesExactCharacters()
    {
        IReadOnlyList<DocumentChunk> chunks =
            await ChunkAsync(
                OversizedText,
                SmallMaxChunkSize);

        string combined =
            string.Concat(
                chunks.Select(chunk => chunk.Text));

        Assert.Equal(
            OversizedText,
            combined);
    }

    [Fact]
    public void Constructor_MaxChunkSizeZero_ThrowsArgumentOutOfRangeException()
    {
        ArgumentOutOfRangeException exception =
            Assert.Throws<ArgumentOutOfRangeException>(
                () =>
                    CreateChunker(0));

        Assert.Equal(
            "maxChunkSize",
            exception.ParamName);
    }

    [Fact]
    public void Constructor_NegativeMaxChunkSize_ThrowsArgumentOutOfRangeException()
    {
        ArgumentOutOfRangeException exception =
            Assert.Throws<ArgumentOutOfRangeException>(
                () =>
                    CreateChunker(-1));

        Assert.Equal(
            "maxChunkSize",
            exception.ParamName);
    }

    private static readonly string OversizedText =
        "One two three four five six seven eight nine ten.";

    private static DocumentTextChunker CreateChunker(
        int maxChunkSize = DefaultMaxChunkSize)
    {
        return new DocumentTextChunker(maxChunkSize);
    }

    private static DocumentTextNormalizationResult CreateNormalizationResult(
        string text)
    {
        return new DocumentTextNormalizationResult(text);
    }

    private static async Task<IReadOnlyList<DocumentChunk>> ChunkAsync(
        string text,
        int maxChunkSize = DefaultMaxChunkSize,
        CancellationToken cancellationToken = default)
    {
        return await CreateChunker(maxChunkSize).ChunkAsync(
            CreateNormalizationResult(text),
            cancellationToken);
    }
}
