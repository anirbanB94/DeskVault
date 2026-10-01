using DeskVault.Application.Documents.Normalization;
using DeskVault.Application.Documents.Provenance;

namespace DeskVault.Application.Documents.Chunking;

public sealed class DocumentTextChunker
    : IDocumentTextChunker
{
    private const string ParagraphSeparator = "\n\n";

    private readonly int _maxChunkSize;

    public DocumentTextChunker(
        int maxChunkSize)
    {
        if (maxChunkSize <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxChunkSize),
                "Maximum chunk size must be greater than zero.");
        }

        _maxChunkSize = maxChunkSize;
    }

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

            int candidateEnd =
                Math.Min(
                    start + _maxChunkSize,
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
                        start + _maxChunkSize,
                        paragraph.Length);
            }

            string chunkText =
                paragraph[start..candidateEnd];

            DocumentSourceLocation? sourceLocation =
                null;

            if (hasSourceLocation)
            {
                sourceLocation =
                    new DocumentSourceLocation(
                        chunkStartLine,
                        GetEndLine(
                            chunkStartLine,
                            chunkText));
            }

            chunks.Add(
                new DocumentChunk(
                    chunks.Count,
                    chunkText,
                    sourceLocation));

            int chunkEndLine =
                GetEndLine(
                    chunkStartLine,
                    chunkText);

            chunkStartLine =
                chunkText[^1] == '\n'
                    ? chunkEndLine + 1
                    : chunkEndLine;

            start = candidateEnd;
        }
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

    private static void FlushCurrentChunk(
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

        DocumentSourceLocation? sourceLocation =
            sourceStartLine.HasValue &&
            sourceEndLine.HasValue
                ? new DocumentSourceLocation(
                    sourceStartLine.Value,
                    sourceEndLine.Value)
                : null;

        chunks.Add(
            new DocumentChunk(
                chunks.Count,
                string.Join(
                    ParagraphSeparator,
                    currentParagraphs),
                sourceLocation));

        currentParagraphs.Clear();
        currentLength = 0;
    }
}
