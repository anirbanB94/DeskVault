using DeskVault.Application.Interfaces;

namespace DeskVault.Application.Documents.Commands.RemoveDocument;

public sealed class RemoveDocumentHandler
{
    private readonly IDocumentRepository _repository;
    private readonly IStorageService _storageService;

    public RemoveDocumentHandler(
        IDocumentRepository repository,
        IStorageService storageService)
    {
        _repository = repository;
        _storageService = storageService;
    }

    public async Task<RemoveDocumentResult> HandleAsync(
        RemoveDocumentCommand command,
        CancellationToken cancellationToken = default)
    {
        var document = await _repository.GetByIdAsync(
            command.DocumentId,
            cancellationToken);

        if (document is null)
        {
            return new RemoveDocumentResult(
                RemoveDocumentResultStatus.NotFound,
                "The requested document could not be found.");
        }

        try
        {
            await _storageService.DeleteAsync(
                document.StoredFilePath,
                cancellationToken);
        }
        catch (IOException ex)
        {
            return new RemoveDocumentResult(
                RemoveDocumentResultStatus.StorageDeletionFailed,
                ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return new RemoveDocumentResult(
                RemoveDocumentResultStatus.StorageDeletionFailed,
                ex.Message);
        }

        try
        {
            await _repository.DeleteAsync(
                command.DocumentId,
                cancellationToken);

            return new RemoveDocumentResult(
                RemoveDocumentResultStatus.Success,
                "Document removed successfully.");
        }
        catch (Exception ex) when (
            ex is IOException ||
            ex is InvalidOperationException)
        {
            return new RemoveDocumentResult(
                RemoveDocumentResultStatus.MetadataDeletionFailed,
                ex.Message);
        }
    }
}
