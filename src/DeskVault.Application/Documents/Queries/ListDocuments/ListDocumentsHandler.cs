using DeskVault.Application.Interfaces;
using DeskVault.Shared.Resources;
using DeskVault.Domain.Documents;
using Microsoft.Extensions.Logging;

namespace DeskVault.Application.Documents.Queries.ListDocuments;

public sealed class ListDocumentsHandler
{
    private readonly IDocumentRepository _repository;
    private readonly ILogger<ListDocumentsHandler> _logger;

    public ListDocumentsHandler(
        IDocumentRepository repository,
        ILogger<ListDocumentsHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Document>> HandleAsync(
        ListDocumentsQuery query,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation(
            LogMessages.DocumentListRefreshStarted);

        var documents =
            await _repository.GetAllAsync(
                cancellationToken);

        _logger.LogInformation(
            LogMessages.DocumentListRefreshCompleted,
            documents.Count);

        return documents;
    }
}
