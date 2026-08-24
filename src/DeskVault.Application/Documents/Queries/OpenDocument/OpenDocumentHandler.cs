using DeskVault.Application.Interfaces;
using DeskVault.Shared.Resources;
using Microsoft.Extensions.Logging;

namespace DeskVault.Application.Documents.Queries.OpenDocument;

public sealed class OpenDocumentHandler
{
    private readonly IDocumentRepository _repository;
    private readonly IDocumentReader _documentReader;
    private readonly ILogger<OpenDocumentHandler> _logger;

    public OpenDocumentHandler(
        IDocumentRepository repository,
        IDocumentReader documentReader,
        ILogger<OpenDocumentHandler> logger)
    {
        _repository = repository;
        _documentReader = documentReader;
        _logger = logger;
    }

    public async Task<OpenDocumentResult> HandleAsync(
        OpenDocumentQuery query,
        CancellationToken cancellationToken = default)
    {
        var document = await _repository.GetByIdAsync(
            query.DocumentId,
            cancellationToken);

        if (document is null)
        {
            _logger.LogWarning(
                LogMessages.DocumentOpenNotFound);

            throw new FileNotFoundException(
                "The requested document could not be found.");
        }

        Stream content = await _documentReader.OpenReadAsync(
            document.StoredFilePath,
            cancellationToken);

        _logger.LogInformation(
            LogMessages.DocumentOpenCompleted);

        return new OpenDocumentResult(
            content,
            document.FileName);
    }
}
