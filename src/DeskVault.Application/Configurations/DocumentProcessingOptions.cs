namespace DeskVault.Application.Configurations;

public sealed class DocumentProcessingOptions
{
    public const string SectionName = "DocumentProcessing";

    public const long DefaultMaxDecryptedDocumentBytes =
        32L * 1024 * 1024;

    public const long DefaultMaxProcessedTextBytes =
        32L * 1024 * 1024;

    public long MaxDecryptedDocumentBytes { get; init; } =
        DefaultMaxDecryptedDocumentBytes;

    public long MaxProcessedTextBytes { get; init; } =
        DefaultMaxProcessedTextBytes;
}
