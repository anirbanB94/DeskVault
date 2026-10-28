namespace DeskVault.Application.Configurations;

public sealed class DocumentProcessingOptions
{
    public const string SectionName = "DocumentProcessing";

    public const int DefaultMaxChunkSize = 4000;

    public const int DefaultChunkOverlap = 0;

    public const long DefaultMaxDecryptedDocumentBytes = 32L * 1024 * 1024;

    public const long DefaultMaxProcessedTextBytes = 32L * 1024 * 1024;

    public const int DefaultMaxParserNestingDepth = 64;

    public const int DefaultMaxStructuredNodeCount = 100_000;

    public const int DefaultMaxCsvRows = 100_000;

    public const int DefaultMaxCsvColumns = 1_024;

    public const int DefaultMaxCsvFieldLength = 1_048_576;

    public const int DefaultMaxCsvCells = 500_000;

    public const int DefaultMaxIniEntries = 100_000;

    public const int DefaultMaxIniLineLength = 1_048_576;

    public int MaxChunkSize { get; init; } = DefaultMaxChunkSize;

    public int ChunkOverlap { get; init; } = DefaultChunkOverlap;

    public long MaxDecryptedDocumentBytes { get; init; } =
        DefaultMaxDecryptedDocumentBytes;

    public long MaxProcessedTextBytes { get; init; } =
        DefaultMaxProcessedTextBytes;

    public int MaxParserNestingDepth { get; init; } =
        DefaultMaxParserNestingDepth;

    public int MaxStructuredNodeCount { get; init; } =
        DefaultMaxStructuredNodeCount;

    public int MaxCsvRows { get; init; } =
        DefaultMaxCsvRows;

    public int MaxCsvColumns { get; init; } =
        DefaultMaxCsvColumns;

    public int MaxCsvFieldLength { get; init; } =
        DefaultMaxCsvFieldLength;

    public int MaxCsvCells { get; init; } =
        DefaultMaxCsvCells;

    public int MaxIniEntries { get; init; } =
        DefaultMaxIniEntries;

    public int MaxIniLineLength { get; init; } =
        DefaultMaxIniLineLength;
}
