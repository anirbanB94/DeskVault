namespace DeskVault.Application.Configurations;

public sealed class DocumentProcessingOptions
{
    public const string SectionName = "DocumentProcessing";

    public const int DefaultMaxChunkSize = 4000;

    public const int DefaultChunkOverlap = 0;

    public const long DefaultMaxDecryptedDocumentBytes = 32L * 1024 * 1024;

    public const long DefaultMaxProcessedTextBytes = 32L * 1024 * 1024;

    public int MaxChunkSize { get; init; } = DefaultMaxChunkSize;

    public int ChunkOverlap { get; init; } = DefaultChunkOverlap;

    public long MaxDecryptedDocumentBytes { get; init; } = DefaultMaxDecryptedDocumentBytes;

    public long MaxProcessedTextBytes { get; init; } = DefaultMaxProcessedTextBytes;
}
