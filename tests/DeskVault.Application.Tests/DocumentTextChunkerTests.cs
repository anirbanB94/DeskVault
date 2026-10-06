using DeskVault.Application.Documents.Chunking;
using DeskVault.Application.Documents.Content;
using DeskVault.Application.Documents.Normalization;
using DeskVault.Application.Documents.Processing;
using DeskVault.Application.Documents.Provenance;

namespace DeskVault.Application.Tests;

public sealed class DocumentTextChunkerTests
{
    private const int DefaultMaxChunkSize = 1000;
    private const int SmallMaxChunkSize = 20;
    private const string ExpectedRuleVersion = "paragraph-chunker-v1";
    private const int AlternateMaxChunkSize = 500;
    private const int DefaultChunkOverlap = 0;

    [Fact]
    public void RuleVersion_SameChunkingRules_ReturnsSameVersion()
    {
        // Arrange
        DocumentTextChunker firstChunker =
            CreateChunker();

        DocumentTextChunker secondChunker =
            CreateChunker();

        // Act
        DocumentChunkingRuleVersion firstVersion =
            firstChunker.RuleVersion;

        DocumentChunkingRuleVersion secondVersion =
            secondChunker.RuleVersion;

        // Assert
        Assert.Equal(
            firstVersion,
            secondVersion);

        Assert.False(
            string.IsNullOrWhiteSpace(
                firstVersion.Value));
    }

    [Fact]
    public void RuleVersion_DifferentMaxChunkSize_ReturnsDifferentVersion()
    {
        // Arrange
        DocumentTextChunker firstChunker =
            CreateChunker();

        DocumentTextChunker secondChunker =
            CreateChunker(
                AlternateMaxChunkSize);

        // Act
        DocumentChunkingRuleVersion firstVersion =
            firstChunker.RuleVersion;

        DocumentChunkingRuleVersion secondVersion =
            secondChunker.RuleVersion;

        // Assert
        Assert.NotEqual(
            firstVersion,
            secondVersion);
    }

    [Fact]
    public void RuleVersion_DifferentChunkOverlap_ReturnsDifferentVersion()
    {
        // Arrange
        DocumentTextChunker firstChunker =
            CreateChunker(
                DefaultMaxChunkSize,
                chunkOverlap: 0);

        DocumentTextChunker secondChunker =
            CreateChunker(
                DefaultMaxChunkSize,
                chunkOverlap: 100);

        // Act
        DocumentChunkingRuleVersion firstVersion =
            firstChunker.RuleVersion;

        DocumentChunkingRuleVersion secondVersion =
            secondChunker.RuleVersion;

        // Assert
        Assert.NotEqual(
            firstVersion,
            secondVersion);
    }

    [Fact]
    public void RuleVersion_UsesChunkingAlgorithmDefinition()
    {
        // Arrange
        DocumentTextChunker chunker =
            CreateChunker();

        DocumentChunkingRuleVersion expectedVersion =
            DocumentRuleVersionFactory.CreateChunkingRuleVersion(
                ExpectedRuleVersion,
                DefaultMaxChunkSize,
                DefaultChunkOverlap);

        // Act
        DocumentChunkingRuleVersion actualVersion =
            chunker.RuleVersion;

        // Assert
        Assert.Equal(
            expectedVersion,
            actualVersion);
    }

    [Fact]
    public async Task ChunkAsync_ChunksCarryChunkingRuleVersion()
    {
        // Arrange
        DocumentTextChunker chunker =
            CreateChunker();

        var normalizationResult =
            CreateNormalizationResult(
                "First paragraph.");

        // Act
        IReadOnlyList<DocumentChunk> chunks =
            await chunker.ChunkAsync(
                normalizationResult);

        // Assert
        DocumentChunk chunk =
            Assert.Single(chunks);

        Assert.Equal(
            chunker.RuleVersion,
            chunk.ChunkingRuleVersion);
    }

    [Fact]
    public async Task ChunkAsync_StructuredContent_UsesDeterministicSearchableProjection()
    {
        // Arrange
        DocumentContent content =
            new(
            [
                new DocumentContentUnit(
                order: 0,
                kind: DocumentContentUnitKind.TableRow,
                fields:
                [
                    new DocumentContentField("Id", "1001"),
                    new DocumentContentField("Name", "Alice Johnson"),
                    new DocumentContentField("Department", "Engineering")
                ]),

            new DocumentContentUnit(
                order: 1,
                kind: DocumentContentUnitKind.TableRow,
                fields:
                [
                    new DocumentContentField("Id", "1002"),
                    new DocumentContentField("Name", "Bob Smith"),
                    new DocumentContentField("Department", "Design")
                ])
            ]);

        DocumentTextNormalizationResult normalizationResult =
            new(
                content.SearchableText,
                Content: content);

        DocumentTextChunker chunker =
            CreateChunker();

        // Act
        IReadOnlyList<DocumentChunk> firstChunks =
            await chunker.ChunkAsync(
                normalizationResult);

        IReadOnlyList<DocumentChunk> secondChunks =
            await chunker.ChunkAsync(
                normalizationResult);

        // Assert
        Assert.Equal(
            firstChunks,
            secondChunks);

        DocumentChunk chunk =
            Assert.Single(firstChunks);

        Assert.Equal(
            0,
            chunk.Order);

        Assert.Equal(
            content.SearchableText,
            chunk.Text);
    }

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
    public async Task ChunkAsync_DirectTextMapping_WithOverlap_TracksSourceLocation()
    {
        // Arrange
        const string firstParagraph =
            "ABCDEFGH";

        const string secondParagraph =
            "12345";

        string text =
            $"{firstParagraph}\n\n{secondParagraph}";

        var normalizationResult =
            new DocumentTextNormalizationResult(
                text,
                DocumentSourceLocationMappingKind.DirectText);

        // Act
        IReadOnlyList<DocumentChunk> chunks =
            await CreateChunker(
                maxChunkSize: 10,
                chunkOverlap: 2)
            .ChunkAsync(
                normalizationResult);

        // Assert
        Assert.Equal(
            2,
            chunks.Count);

        Assert.Equal(
            "ABCDEFGH",
            chunks[0].Text);

        Assert.Equal(
            "GH12345",
            chunks[1].Text);

        Assert.Equal(
            new DocumentSourceLocation(
                1,
                1),
            chunks[0].SourceLocation);

        Assert.Equal(
            new DocumentSourceLocation(
                1,
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
    public async Task ChunkAsync_ChunkOverlap_ReusesSuffixOfPreviousChunk()
    {
        // Arrange
        const string firstParagraph =
            "ABCDEFGH";

        const string secondParagraph =
            "12345";

        string text =
            $"{firstParagraph}\n\n{secondParagraph}";

        IReadOnlyList<DocumentChunk> chunks =
            await ChunkAsync(
                text,
                maxChunkSize: 10,
                chunkOverlap: 2);

        // Assert
        Assert.Equal(
            2,
            chunks.Count);

        Assert.Equal(
            firstParagraph,
            chunks[0].Text);

        Assert.Equal(
            "GH" + secondParagraph,
            chunks[1].Text);

        Assert.Equal(
            "GH",
            chunks[1].Text[..2]);

        Assert.All(
            chunks,
            chunk =>
                Assert.True(
                    chunk.Text.Length <= 10));
    }

    [Fact]
    public async Task ChunkAsync_ChangingChunkConfiguration_ChangesChunkBoundaries()
    {
        // Arrange
        const string text =
            "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        IReadOnlyList<DocumentChunk> baselineChunks =
            await ChunkAsync(
                text,
                maxChunkSize: 10,
                chunkOverlap: 0);

        IReadOnlyList<DocumentChunk> overlapChunks =
            await ChunkAsync(
                text,
                maxChunkSize: 10,
                chunkOverlap: 3);

        // Act
        string[] baselineTexts =
            baselineChunks
                .Select(chunk => chunk.Text)
                .ToArray();

        string[] overlapTexts =
            overlapChunks
                .Select(chunk => chunk.Text)
                .ToArray();

        // Assert
        Assert.NotEqual(
            baselineTexts,
            overlapTexts);

        Assert.All(
            baselineChunks,
            chunk =>
                Assert.True(
                    chunk.Text.Length <= 10));

        Assert.All(
            overlapChunks,
            chunk =>
                Assert.True(
                    chunk.Text.Length <= 10));
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
    public async Task ChunkAsync_ChunkOverlap_UsesConfiguredCharacterCount()
    {
        // Arrange
        const string text =
            "ABCDEFGHIJKLMNOPQRSTUVWX";

        IReadOnlyList<DocumentChunk> chunks =
            await ChunkAsync(
                text,
                maxChunkSize: 10,
                chunkOverlap: 3);

        // Assert
        Assert.True(
            chunks.Count > 1);

        for (int index = 1;
             index < chunks.Count;
             index++)
        {
            string previousChunk =
                chunks[index - 1].Text;

            string currentChunk =
                chunks[index].Text;

            Assert.Equal(
                previousChunk[^3..],
                currentChunk[..3]);
        }
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
    public async Task ChunkAsync_OversizedParagraph_ChunkOverlap_ReusesPreviousSuffix()
    {
        // Arrange
        IReadOnlyList<DocumentChunk> chunks =
            await ChunkAsync(
                OversizedText,
                maxChunkSize: SmallMaxChunkSize,
                chunkOverlap: 4);

        // Assert
        Assert.True(
            chunks.Count > 1);

        Assert.All(
            chunks,
            chunk =>
                Assert.True(
                    chunk.Text.Length <= SmallMaxChunkSize));

        for (int index = 1;
             index < chunks.Count;
             index++)
        {
            Assert.Equal(
                chunks[index - 1].Text[^4..],
                chunks[index].Text[..4]);
        }
    }

    [Fact]
    public async Task ChunkAsync_ChunkOverlap_MaximumAllowedOverlap_ContinuesForwardProgress()
    {
        // Arrange
        const string text =
            "ABCDEFGHIJ";

        const int maxChunkSize =
            5;

        const int chunkOverlap =
            4;

        IReadOnlyList<DocumentChunk> chunks =
            await ChunkAsync(
                text,
                maxChunkSize,
                chunkOverlap);

        // Assert
        Assert.True(
            chunks.Count > 1);

        Assert.All(
            chunks,
            chunk =>
            {
                Assert.False(
                    string.IsNullOrEmpty(
                        chunk.Text));

                Assert.True(
                    chunk.Text.Length <=
                    maxChunkSize);
            });

        for (int index = 1;
             index < chunks.Count;
             index++)
        {
            Assert.Equal(
                chunks[index - 1].Text[^chunkOverlap..],
                chunks[index].Text[..chunkOverlap]);
        }

        Assert.Contains(
            chunks,
            chunk =>
                chunk.Text.EndsWith(
                    "J",
                    StringComparison.Ordinal));
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
    public async Task ChunkAsync_WithOverlap_IsDeterministic()
    {
        // Arrange
        const string text =
            "First paragraph with enough content.\n\n" +
            "Second paragraph with enough content.\n\n" +
            "Third paragraph with enough content.";

        var normalizationResult =
            CreateNormalizationResult(text);

        var chunker =
            CreateChunker(
                SmallMaxChunkSize,
                chunkOverlap: 4);

        // Act
        IReadOnlyList<DocumentChunk> first =
            await chunker.ChunkAsync(
                normalizationResult);

        IReadOnlyList<DocumentChunk> second =
            await chunker.ChunkAsync(
                normalizationResult);

        // Assert
        Assert.Equal(
            first,
            second);
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

    [Fact]
    public void Constructor_NegativeChunkOverlap_ThrowsArgumentOutOfRangeException()
    {
        ArgumentOutOfRangeException exception =
            Assert.Throws<ArgumentOutOfRangeException>(
                () =>
                    CreateChunker(
                        DefaultMaxChunkSize,
                        chunkOverlap: -1));

        Assert.Equal(
            "chunkOverlap",
            exception.ParamName);
    }

    [Fact]
    public void Constructor_ChunkOverlapEqualToMaxChunkSize_ThrowsArgumentOutOfRangeException()
    {
        ArgumentOutOfRangeException exception =
            Assert.Throws<ArgumentOutOfRangeException>(
                () =>
                    CreateChunker(
                        SmallMaxChunkSize,
                        chunkOverlap: SmallMaxChunkSize));

        Assert.Equal(
            "chunkOverlap",
            exception.ParamName);
    }

    private static readonly string OversizedText =
        "One two three four five six seven eight nine ten.";

    private static DocumentTextChunker CreateChunker(
        int maxChunkSize = DefaultMaxChunkSize,
        int chunkOverlap = DefaultChunkOverlap)
    {
        return new DocumentTextChunker(
            maxChunkSize,
            chunkOverlap);
    }

    private static DocumentTextNormalizationResult CreateNormalizationResult(
        string text)
    {
        return new DocumentTextNormalizationResult(text);
    }

    private static async Task<IReadOnlyList<DocumentChunk>> ChunkAsync(
        string text,
        int maxChunkSize = DefaultMaxChunkSize,
        int chunkOverlap = DefaultChunkOverlap,
        CancellationToken cancellationToken = default)
    {
        return await CreateChunker(
                maxChunkSize,
                chunkOverlap)
            .ChunkAsync(
                CreateNormalizationResult(text),
                cancellationToken);
    }
}
