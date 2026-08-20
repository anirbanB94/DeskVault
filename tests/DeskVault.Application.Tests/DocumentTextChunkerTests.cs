using DeskVault.Application.Documents.Chunking;
using DeskVault.Application.Documents.Normalization;

namespace DeskVault.Application.Tests;

public sealed class DocumentTextChunkerTests
{
    [Fact]
    public async Task ChunkAsync_EmptyText_ReturnsNoChunks()
    {
        var normalizationResult =
            new DocumentTextNormalizationResult(
                string.Empty);

        var chunker =
            new DocumentTextChunker(
                maxChunkSize: 1000);

        IReadOnlyList<DocumentChunk> chunks =
            await chunker.ChunkAsync(
                normalizationResult);

        Assert.Empty(chunks);
    }

    [Fact]
    public async Task ChunkAsync_SingleParagraph_ReturnsSingleChunk()
    {
        const string text =
            "DeskVault keeps documents searchable.";

        var normalizationResult =
            new DocumentTextNormalizationResult(text);

        var chunker =
            new DocumentTextChunker(
                maxChunkSize: 1000);

        IReadOnlyList<DocumentChunk> chunks =
            await chunker.ChunkAsync(
                normalizationResult);

        var chunk =
            Assert.Single(chunks);

        Assert.Equal(
            0,
            chunk.Order);

        Assert.Equal(
            text,
            chunk.Text);
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

        var normalizationResult =
            new DocumentTextNormalizationResult(text);

        var chunker =
            new DocumentTextChunker(
                maxChunkSize: 1000);

        IReadOnlyList<DocumentChunk> chunks =
            await chunker.ChunkAsync(
                normalizationResult);

        var chunk =
            Assert.Single(chunks);

        Assert.Equal(
            text,
            chunk.Text);
    }

    [Fact]
    public async Task ChunkAsync_ParagraphsExceedLimit_PreservesParagraphBoundaries()
    {
        const string firstParagraph =
            "First paragraph.";

        const string secondParagraph =
            "Second paragraph.";

        string text =
            firstParagraph +
            "\n\n" +
            secondParagraph;

        var normalizationResult =
            new DocumentTextNormalizationResult(text);

        var chunker =
            new DocumentTextChunker(
                maxChunkSize: secondParagraph.Length);

        IReadOnlyList<DocumentChunk> chunks =
            await chunker.ChunkAsync(
                normalizationResult);

        Assert.Equal(
            2,
            chunks.Count);

        Assert.Equal(
            0,
            chunks[0].Order);

        Assert.Equal(
            1,
            chunks[1].Order);

        Assert.Equal(
            firstParagraph,
            chunks[0].Text);

        Assert.Equal(
            secondParagraph,
            chunks[1].Text);
    }

    [Fact]
    public async Task ChunkAsync_OversizedParagraph_SplitsIntoBoundedChunks()
    {
        const string text =
            "One two three four five six seven eight nine ten.";

        var normalizationResult =
            new DocumentTextNormalizationResult(text);

        var chunker =
            new DocumentTextChunker(
                maxChunkSize: 20);

        IReadOnlyList<DocumentChunk> chunks =
            await chunker.ChunkAsync(
                normalizationResult);

        Assert.True(
            chunks.Count > 1);

        Assert.All(
            chunks,
            chunk =>
                Assert.True(
                    chunk.Text.Length <= 20));
    }

    [Fact]
    public async Task ChunkAsync_OversizedParagraph_PreservesEveryCharacter()
    {
        const string text =
            "One two three four five six seven eight nine ten.";

        var normalizationResult =
            new DocumentTextNormalizationResult(text);

        var chunker =
            new DocumentTextChunker(
                maxChunkSize: 20);

        IReadOnlyList<DocumentChunk> chunks =
            await chunker.ChunkAsync(
                normalizationResult);

        string combined =
            string.Concat(
                chunks.Select(
                    chunk => chunk.Text));

        string expected =
            text.Replace(
                " ",
                string.Empty,
                StringComparison.Ordinal);

        string actual =
            combined.Replace(
                " ",
                string.Empty,
                StringComparison.Ordinal);

        Assert.Equal(
            expected,
            actual);
    }

    [Fact]
    public async Task ChunkAsync_OversizedParagraph_ChunksAreOrdered()
    {
        const string text =
            "One two three four five six seven eight nine ten.";

        var normalizationResult =
            new DocumentTextNormalizationResult(text);

        var chunker =
            new DocumentTextChunker(
                maxChunkSize: 20);

        IReadOnlyList<DocumentChunk> chunks =
            await chunker.ChunkAsync(
                normalizationResult);

        for (int index = 0;
             index < chunks.Count;
             index++)
        {
            Assert.Equal(
                index,
                chunks[index].Order);
        }
    }

    [Fact]
    public async Task ChunkAsync_DoesNotCreateEmptyChunks()
    {
        const string text =
            """
            First paragraph.


            Second paragraph.
            """;

        var normalizationResult =
            new DocumentTextNormalizationResult(text);

        var chunker =
            new DocumentTextChunker(
                maxChunkSize: 1000);

        IReadOnlyList<DocumentChunk> chunks =
            await chunker.ChunkAsync(
                normalizationResult);

        Assert.NotEmpty(chunks);

        Assert.All(
            chunks,
            chunk =>
                Assert.False(
                    string.IsNullOrEmpty(
                        chunk.Text)));
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
            new DocumentTextNormalizationResult(text);

        var chunker =
            new DocumentTextChunker(
                maxChunkSize: 20);

        IReadOnlyList<DocumentChunk> first =
            await chunker.ChunkAsync(
                normalizationResult);

        IReadOnlyList<DocumentChunk> second =
            await chunker.ChunkAsync(
                normalizationResult);

        Assert.Equal(
            first.Count,
            second.Count);

        for (int index = 0;
             index < first.Count;
             index++)
        {
            Assert.Equal(
                first[index].Order,
                second[index].Order);

            Assert.Equal(
                first[index].Text,
                second[index].Text);
        }
    }

    [Fact]
    public async Task ChunkAsync_WhenCancellationRequested_ThrowsOperationCanceledException()
    {
        var normalizationResult =
            new DocumentTextNormalizationResult(
                "Cancellation test.");

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        var chunker =
            new DocumentTextChunker(
                maxChunkSize: 1000);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                chunker.ChunkAsync(
                    normalizationResult,
                    cancellationTokenSource.Token));
    }

    [Fact]
    public async Task ChunkAsync_OversizedParagraph_PreservesExactCharacters()
    {
        const string text =
            "One two three four five six seven eight nine ten.";

        var normalizationResult =
            new DocumentTextNormalizationResult(text);

        var chunker =
            new DocumentTextChunker(
                maxChunkSize: 20);

        IReadOnlyList<DocumentChunk> chunks =
            await chunker.ChunkAsync(
                normalizationResult);

        string combined =
            string.Concat(
                chunks.Select(
                    chunk => chunk.Text));

        Assert.Equal(
            text,
            combined);
    }

    [Fact]
    public void Constructor_MaxChunkSizeZero_ThrowsArgumentOutOfRangeException()
    {
        ArgumentOutOfRangeException exception =
            Assert.Throws<ArgumentOutOfRangeException>(
                () =>
                    new DocumentTextChunker(
                        maxChunkSize: 0));

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
                    new DocumentTextChunker(
                        maxChunkSize: -1));

        Assert.Equal(
            "maxChunkSize",
            exception.ParamName);
    }
}
