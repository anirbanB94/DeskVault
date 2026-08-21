using DeskVault.Application.Documents.Chunking;
using DeskVault.Application.Documents.Extraction;
using DeskVault.Application.Documents.Normalization;
using DeskVault.Application.Interfaces;
using DeskVault.Domain.Documents;

namespace DeskVault.Application.Documents.Commands.ProcessDocument;

public sealed class ProcessDocumentHandler
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IDocumentReader _documentReader;
    private readonly DocumentTextExtractorResolver _extractorResolver;
    private readonly IDocumentTextNormalizer _normalizer;
    private readonly IDocumentTextChunker _chunker;
    private readonly IDocumentProcessingStore _processingStore;

    public ProcessDocumentHandler(
        IDocumentRepository documentRepository,
        IDocumentReader documentReader,
        DocumentTextExtractorResolver extractorResolver,
        IDocumentTextNormalizer normalizer,
        IDocumentTextChunker chunker,
        IDocumentProcessingStore processingStore)
    {
        _documentRepository = documentRepository;
        _documentReader = documentReader;
        _extractorResolver = extractorResolver;
        _normalizer = normalizer;
        _chunker = chunker;
        _processingStore = processingStore;
    }

    public async Task<ProcessDocumentResult> HandleAsync(
        ProcessDocumentCommand command,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var document =
            await _documentRepository.GetByIdAsync(
                command.DocumentId,
                cancellationToken);

        if (document is null)
        {
            return new ProcessDocumentResult(
                ProcessDocumentResultStatus.NotFound,
                null,
                "The requested document could not be found.");
        }

        document.MarkProcessing();

        await _documentRepository.UpdateAsync(
            document,
            cancellationToken);

        var extractor =
            _extractorResolver.Resolve(
                document.FileName);

        await using var documentStream =
            await _documentReader.OpenReadAsync(
                document.StoredFilePath,
                cancellationToken);

        var extractionResult =
            await extractor.ExtractAsync(
                documentStream,
                document.FileName,
                cancellationToken);

        var normalizationResult =
            await _normalizer.NormalizeAsync(
                extractionResult,
                cancellationToken);

        var chunks =
            await _chunker.ChunkAsync(
                normalizationResult,
                cancellationToken);

        await _processingStore.ReplaceChunksAsync(
            document.Id,
            chunks,
            cancellationToken);

        document.MarkIndexed();

        await _documentRepository.UpdateAsync(
            document,
            cancellationToken);

        document.MarkAvailable();

        await _documentRepository.UpdateAsync(
            document,
            cancellationToken);

        return new ProcessDocumentResult(
            ProcessDocumentResultStatus.Success,
            document.Id,
            "Document processed successfully.");
    }
}
