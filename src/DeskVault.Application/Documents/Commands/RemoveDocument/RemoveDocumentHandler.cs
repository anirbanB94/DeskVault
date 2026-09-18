using DeskVault.Application.Interfaces;
using DeskVault.Shared.Resources;
using Microsoft.Extensions.Logging;

namespace DeskVault.Application.Documents.Commands.RemoveDocument;

public sealed class RemoveDocumentHandler
{
    private readonly IDocumentRepository _repository;
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IStorageService _storageService;
    private readonly ILogger<RemoveDocumentHandler> _logger;

    public RemoveDocumentHandler(
        IDocumentRepository repository,
        IWorkspaceRepository workspaceRepository,
        IStorageService storageService,
        ILogger<RemoveDocumentHandler> logger)
    {
        _repository = repository;
        _workspaceRepository = workspaceRepository;
        _storageService = storageService;
        _logger = logger;
    }

    public async Task<RemoveDocumentResult> HandleAsync(
        RemoveDocumentCommand command,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            LogMessages.DocumentRemovalStarted);

        var document = await _repository.GetByIdAsync(
            command.DocumentId,
            cancellationToken);

        if (document is null)
        {
            _logger.LogWarning(
                LogMessages.DocumentRemovalNotFound);

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
            _logger.LogError(
                ex,
                LogMessages.DocumentStorageDeletionFailed);

            return new RemoveDocumentResult(
                RemoveDocumentResultStatus.StorageDeletionFailed,
                ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(
                ex,
                LogMessages.DocumentStorageDeletionFailed);

            return new RemoveDocumentResult(
                RemoveDocumentResultStatus.StorageDeletionFailed,
                ex.Message);
        }

        try
        {
            await _repository.DeleteAsync(
                command.DocumentId,
                cancellationToken);
        }
        catch (Exception ex) when (
            ex is IOException ||
            ex is InvalidOperationException)
        {
            _logger.LogError(
                ex,
                LogMessages.DocumentMetadataDeletionFailed);

            return new RemoveDocumentResult(
                RemoveDocumentResultStatus.MetadataDeletionFailed,
                ex.Message);
        }

        try
        {
            await _workspaceRepository.RemoveDocumentFromAllWorkspacesAsync(
                command.DocumentId,
                cancellationToken);
        }
        catch (Exception ex) when (
            ex is IOException ||
            ex is InvalidOperationException)
        {
            _logger.LogError(
                ex,
                "Workspace membership cleanup failed during document removal.");

            return new RemoveDocumentResult(
                RemoveDocumentResultStatus.WorkspaceMembershipCleanupFailed,
                ex.Message);
        }

        _logger.LogInformation(
            LogMessages.DocumentRemovalCompleted);

        return new RemoveDocumentResult(
            RemoveDocumentResultStatus.Success,
            "Document removed successfully.");
    }
}
