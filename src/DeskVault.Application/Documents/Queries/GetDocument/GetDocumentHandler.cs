using DeskVault.Application.Interfaces;
using DeskVault.Shared.Resources;
using Microsoft.Extensions.Logging;

namespace DeskVault.Application.Documents.Queries.GetDocument;

public sealed class GetDocumentHandler
{
    private readonly IDocumentRepository _repository;
    private readonly ILogger<GetDocumentHandler> _logger;

    public GetDocumentHandler(
        IDocumentRepository repository,
        ILogger<GetDocumentHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<GetDocumentResult> HandleAsync(
        GetDocumentQuery query,
        CancellationToken cancellationToken = default)
    {
        var document = await _repository.GetByIdAsync(
            query.DocumentId,
            cancellationToken);

        if (document is null)
        {
            _logger.LogWarning(
                LogMessages.DocumentRetrievalNotFound);

            throw new FileNotFoundException(
                "The requested document could not be found.");
        }

        return new GetDocumentResult(
            document.Id,
            document.FileName,
            document.DisplayName,
            document.Sha256Hash,
            document.ImportedAt,
            document.Status);
    }
}
