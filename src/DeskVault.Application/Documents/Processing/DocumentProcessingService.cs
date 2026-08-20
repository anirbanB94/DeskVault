using DeskVault.Application.Documents.Commands.ProcessDocument;
using DeskVault.Application.Interfaces;

namespace DeskVault.Application.Documents.Processing;

public sealed class DocumentProcessingService
    : IDocumentProcessingService
{
    private readonly ProcessDocumentHandler _handler;

    public DocumentProcessingService(
        ProcessDocumentHandler handler)
    {
        _handler = handler;
    }

    public async Task ProcessAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        ProcessDocumentResult result =
            await _handler.HandleAsync(
                new ProcessDocumentCommand(documentId),
                cancellationToken);

        if (result.Status ==
            ProcessDocumentResultStatus.NotFound)
        {
            throw new FileNotFoundException(
                result.Description);
        }
    }
}
