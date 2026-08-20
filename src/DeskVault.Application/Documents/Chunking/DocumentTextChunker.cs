using DeskVault.Application.Documents.Normalization;

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

        var chunks =
            new List<DocumentChunk>();

        var currentParagraphs =
            new List<string>();

        int currentLength = 0;

        foreach (string paragraph in paragraphs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (paragraph.Length == 0)
            {
                continue;
            }

            if (paragraph.Length > _maxChunkSize)
            {
                FlushCurrentChunk(
                    chunks,
                    currentParagraphs,
                    ref currentLength);

                AddOversizedParagraphChunks(
                    chunks,
                    paragraph,
                    cancellationToken);

                continue;
            }

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
                    ref currentLength);
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

        FlushCurrentChunk(
            chunks,
            currentParagraphs,
            ref currentLength);

        return Task.FromResult<IReadOnlyList<DocumentChunk>>(
            chunks);
    }

    private void AddOversizedParagraphChunks(
        List<DocumentChunk> chunks,
        string paragraph,
        CancellationToken cancellationToken)
    {
        int start = 0;

        while (start < paragraph.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int remaining =
                paragraph.Length - start;

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

            chunks.Add(
                new DocumentChunk(
                    chunks.Count,
                    chunkText));

            start = candidateEnd;
        }
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
        ref int currentLength)
    {
        if (currentParagraphs.Count == 0)
        {
            return;
        }

        chunks.Add(
            new DocumentChunk(
                chunks.Count,
                string.Join(
                    ParagraphSeparator,
                    currentParagraphs)));

        currentParagraphs.Clear();
        currentLength = 0;
    }
}
