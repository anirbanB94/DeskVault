using DeskVault.Application.Documents.Normalization;
using DeskVault.Application.Documents.Processing;
using DeskVault.Application.Documents.Provenance;

namespace DeskVault.Application.Documents.Chunking;

public sealed class DocumentTextChunker
    : IDocumentTextChunker
{
    private const string ParagraphSeparator = "\n\n";

    private const string AlgorithmVersion = "paragraph-chunker-v1";

    private readonly int _maxChunkSize;

    private readonly int _chunkOverlap;

    private readonly DocumentChunkingRuleVersion _ruleVersion;

    public DocumentTextChunker(
        int maxChunkSize,
        int chunkOverlap = 0)
    {
        if (maxChunkSize <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxChunkSize),
                "Maximum chunk size must be greater than zero.");
        }

        if (chunkOverlap < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(chunkOverlap),
                "Chunk overlap cannot be negative.");
        }

        if (chunkOverlap >= maxChunkSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(chunkOverlap),
                "Chunk overlap must be smaller than the maximum chunk size.");
        }

        _maxChunkSize =
            maxChunkSize;

        _chunkOverlap =
            chunkOverlap;

        _ruleVersion =
            DocumentRuleVersionFactory.CreateChunkingRuleVersion(
                AlgorithmVersion,
                _maxChunkSize,
                _chunkOverlap);
    }

    public DocumentChunkingRuleVersion RuleVersion =>
        _ruleVersion;

    public Task<IReadOnlyList<DocumentChunk>> ChunkAsync(
        DocumentTextNormalizationResult normalizationResult,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            normalizationResult);

        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrEmpty(normalizationResult.Text))
        {
            return Task.FromResult<IReadOnlyList<DocumentChunk>>(
                []);
        }

        string[] paragraphs =
            normalizationResult.Text.Split(
                ParagraphSeparator,
                StringSplitOptions.None);

        bool hasSourceLocation =
            normalizationResult.SourceLocationMappingKind ==
            DocumentSourceLocationMappingKind.DirectText;

        var chunks =
            new List<DocumentChunk>();

        var currentParagraphs =
            new List<string>();

        int currentLength = 0;

        int sourceLine = 1;

        int? currentChunkStartLine = null;

        int? currentChunkEndLine = null;

        for (int paragraphIndex = 0;
             paragraphIndex < paragraphs.Length;
             paragraphIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string paragraph =
                paragraphs[paragraphIndex];

            int paragraphStartLine =
                sourceLine;

            int paragraphEndLine =
                GetEndLine(
                    paragraphStartLine,
                    paragraph);

            if (paragraph.Length == 0)
            {
                sourceLine =
                    AdvanceSourceLine(
                        paragraphStartLine,
                        paragraph,
                        paragraphIndex <
                        paragraphs.Length - 1);

                continue;
            }

            if (paragraph.Length > _maxChunkSize)
            {
                FlushCurrentChunk(
                    chunks,
                    currentParagraphs,
                    ref currentLength,
                    currentChunkStartLine,
                    currentChunkEndLine);

                currentChunkStartLine = null;
                currentChunkEndLine = null;

                AddOversizedParagraphChunks(
                    chunks,
                    paragraph,
                    paragraphStartLine,
                    hasSourceLocation,
                    cancellationToken);
            }
            else
            {
                int separatorLength =
                    currentParagraphs.Count == 0
                        ? 0
                        : ParagraphSeparator.Length;

                int candidateLength =
                    currentLength +
                    separatorLength +
                    paragraph.Length;

                if (candidateLength > _maxChunkSize)
                {
                    FlushCurrentChunk(
                        chunks,
                        currentParagraphs,
                        ref currentLength,
                        currentChunkStartLine,
                        currentChunkEndLine);

                    currentChunkStartLine = null;
                    currentChunkEndLine = null;
                }

                if (currentParagraphs.Count == 0)
                {
                    if (hasSourceLocation)
                    {
                        currentChunkStartLine =
                            paragraphStartLine;

                        currentChunkEndLine =
                            paragraphEndLine;
                    }
                }
                else if (hasSourceLocation)
                {
                    currentChunkEndLine =
                        paragraphEndLine;
                }

                currentParagraphs.Add(
                    paragraph);

                currentLength =
                    currentParagraphs.Count == 1
                        ? paragraph.Length
                        : currentLength +
                          ParagraphSeparator.Length +
                          paragraph.Length;
            }

            sourceLine =
                AdvanceSourceLine(
                    paragraphStartLine,
                    paragraph,
                    paragraphIndex <
                    paragraphs.Length - 1);
        }

        FlushCurrentChunk(
            chunks,
            currentParagraphs,
            ref currentLength,
            currentChunkStartLine,
            currentChunkEndLine);

        return Task.FromResult<IReadOnlyList<DocumentChunk>>(
            chunks);
    }

    private void AddOversizedParagraphChunks(
        List<DocumentChunk> chunks,
        string paragraph,
        int paragraphStartLine,
        bool hasSourceLocation,
        CancellationToken cancellationToken)
    {
        int start = 0;

        int chunkStartLine =
            paragraphStartLine;

        while (start < paragraph.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int overlapLength =
                GetEffectiveOverlapLength(
                    chunks);

            int availableNewContentLength =
                _maxChunkSize -
                overlapLength;

            int candidateEnd =
                Math.Min(
                    start + availableNewContentLength,
                    paragraph.Length);

            if (candidateEnd < paragraph.Length)
            {
                int whitespaceIndex =
                    FindLastWhitespace(
                        paragraph,
                        start,
                        candidateEnd);

                if (whitespaceIndex > start)
                {
                    candidateEnd =
                        whitespaceIndex + 1;
                }
            }

            if (candidateEnd <= start)
            {
                candidateEnd =
                    Math.Min(
                        start + availableNewContentLength,
                        paragraph.Length);
            }

            string chunkText =
                paragraph[start..candidateEnd];

            int chunkEndLine =
                GetEndLine(
                    chunkStartLine,
                    chunkText);

            AddChunkWithOverlap(
                chunks,
                chunkText,
                chunkStartLine,
                chunkEndLine,
                hasSourceLocation);

            chunkStartLine =
                chunkText[^1] == '\n'
                    ? chunkEndLine + 1
                    : chunkEndLine;

            start =
                candidateEnd;
        }
    }

    private int GetEffectiveOverlapLength(
        List<DocumentChunk> chunks)
    {
        if (_chunkOverlap == 0 ||
            chunks.Count == 0)
        {
            return 0;
        }

        return Math.Min(
            _chunkOverlap,
            chunks[^1].Text.Length);
    }

    private void AddChunkWithOverlap(
        List<DocumentChunk> chunks,
        string chunkText,
        int sourceStartLine,
        int sourceEndLine,
        bool hasSourceLocation)
    {
        int overlapLength =
            Math.Min(
                GetEffectiveOverlapLength(chunks),
                _maxChunkSize - chunkText.Length);

        string overlapText =
            overlapLength == 0
                ? string.Empty
                : chunks[^1]
                    .Text[^overlapLength..];

        string emittedText =
            overlapText +
            chunkText;

        DocumentSourceLocation? sourceLocation =
            null;

        if (hasSourceLocation)
        {
            int emittedStartLine =
                sourceStartLine;

            int emittedEndLine =
                sourceEndLine;

            if (overlapLength > 0 &&
                chunks[^1].SourceLocation is not null)
            {
                DocumentChunk previousChunk =
                    chunks[^1];

                int previousChunkPrefixLength =
                    previousChunk.Text.Length -
                    overlapLength;

                string previousChunkPrefix =
                    previousChunk.Text[..previousChunkPrefixLength];

                int overlapStartLine =
                    previousChunk.SourceLocation!.StartLine +
                    CountLineBreaks(
                        previousChunkPrefix);

                int overlapEndLine =
                    GetEndLine(
                        overlapStartLine,
                        overlapText);

                emittedStartLine =
                    Math.Min(
                        emittedStartLine,
                        overlapStartLine);

                emittedEndLine =
                    Math.Max(
                        emittedEndLine,
                        overlapEndLine);
            }

            sourceLocation =
                new DocumentSourceLocation(
                    emittedStartLine,
                    emittedEndLine);
        }

        chunks.Add(
            new DocumentChunk(
                chunks.Count,
                emittedText,
                sourceLocation,
                _ruleVersion));
    }

    private static int AdvanceSourceLine(
        int startLine,
        string text,
        bool hasFollowingParagraph)
    {
        int nextLine =
            startLine +
            CountLineBreaks(text);

        if (hasFollowingParagraph)
        {
            nextLine += 2;
        }

        return nextLine;
    }

    private static int GetEndLine(
        int startLine,
        string text)
    {
        if (text.Length == 0)
        {
            return startLine;
        }

        int lineBreakCount =
            CountLineBreaks(text);

        if (lineBreakCount == 0)
        {
            return startLine;
        }

        return text[^1] == '\n'
            ? startLine + lineBreakCount - 1
            : startLine + lineBreakCount;
    }

    private static int CountLineBreaks(
        string text)
    {
        int count = 0;

        foreach (char character in text)
        {
            if (character == '\n')
            {
                count++;
            }
        }

        return count;
    }

    private static int FindLastWhitespace(
        string text,
        int start,
        int end)
    {
        for (int index = end - 1;
             index > start;
             index--)
        {
            if (char.IsWhiteSpace(
                    text[index]))
            {
                return index;
            }
        }

        return -1;
    }

    private void FlushCurrentChunk(
        List<DocumentChunk> chunks,
        List<string> currentParagraphs,
        ref int currentLength,
        int? sourceStartLine,
        int? sourceEndLine)
    {
        if (currentParagraphs.Count == 0)
        {
            return;
        }

        string chunkText =
            string.Join(
                ParagraphSeparator,
                currentParagraphs);

        if (sourceStartLine.HasValue &&
            sourceEndLine.HasValue)
        {
            AddChunkWithOverlap(
                chunks,
                chunkText,
                sourceStartLine.Value,
                sourceEndLine.Value,
                hasSourceLocation: true);
        }
        else
        {
            int overlapLength =
                Math.Min(
                    GetEffectiveOverlapLength(chunks),
                    _maxChunkSize - chunkText.Length);

            string overlapText =
                overlapLength == 0
                    ? string.Empty
                    : chunks[^1]
                        .Text[^overlapLength..];

            chunks.Add(
                new DocumentChunk(
                    chunks.Count,
                    overlapText + chunkText,
                    null,
                    _ruleVersion));
        }

        currentParagraphs.Clear();
        currentLength = 0;
    }
}
