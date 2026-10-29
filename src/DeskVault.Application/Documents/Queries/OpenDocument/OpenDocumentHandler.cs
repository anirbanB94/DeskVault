using DeskVault.Application.Configurations;
using DeskVault.Application.Interfaces;
using DeskVault.Shared.Resources;
using Microsoft.Extensions.Logging;

namespace DeskVault.Application.Documents.Queries.OpenDocument;

public sealed class OpenDocumentHandler
{
    private readonly IDocumentRepository _repository;

    private readonly IDocumentReader _documentReader;

    private readonly long _maximumPlaintextBytes;

    private readonly ILogger<OpenDocumentHandler> _logger;

    public OpenDocumentHandler(
        IDocumentRepository repository,
        IDocumentReader documentReader,
        DocumentProcessingOptions processingOptions,
        ILogger<OpenDocumentHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(processingOptions);

        if (processingOptions.MaxDecryptedDocumentBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(processingOptions),
                "Maximum decrypted document bytes must be greater than zero.");
        }

        _repository = repository;
        _documentReader = documentReader;
        _maximumPlaintextBytes = processingOptions.MaxDecryptedDocumentBytes;
        _logger = logger;
    }

    public async Task<OpenDocumentResult> HandleAsync(
        OpenDocumentQuery query,
        CancellationToken cancellationToken = default)
    {
        var document =
            await _repository.GetByIdAsync(
                query.DocumentId,
                cancellationToken);

        if (document is null)
        {
            _logger.LogWarning(
                LogMessages.DocumentOpenNotFound);

            throw new FileNotFoundException(
                "The requested document could not be found.");
        }

        Stream content =
            await _documentReader.OpenReadAsync(
                document.Id,
                document.Sha256Hash,
                _maximumPlaintextBytes,
                cancellationToken);

        _logger.LogInformation(
            LogMessages.DocumentOpenCompleted);

        return new OpenDocumentResult(
            content,
            document.FileName);
    }
}
