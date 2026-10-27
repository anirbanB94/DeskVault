using DeskVault.Application.Interfaces;
using DeskVault.Shared.Extensions;
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
                document.Id,
                cancellationToken);
        }
        catch (IOException ex)
        {
            _logger.LogSafeFailure(
                LogLevel.Error,
                DiagnosticOperationCodes.DocumentRemovalStorageDeletionFailure,
                LogMessages.DocumentStorageDeletionFailed,
                ex);

            return new RemoveDocumentResult(
                RemoveDocumentResultStatus.StorageDeletionFailed,
                "The document could not be removed because its stored artifact could not be deleted.");
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogSafeFailure(
                LogLevel.Error,
                DiagnosticOperationCodes.DocumentRemovalStorageDeletionFailure,
                LogMessages.DocumentStorageDeletionFailed,
                ex);

            return new RemoveDocumentResult(
                RemoveDocumentResultStatus.StorageDeletionFailed,
                "The document could not be removed because its stored artifact could not be deleted.");
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
            _logger.LogSafeFailure(
                LogLevel.Error,
                DiagnosticOperationCodes.DocumentRemovalWorkspaceMembershipCleanupFailure,
                LogMessages.DocumentWorkspaceMembershipCleanupFailed,
                ex);

            return new RemoveDocumentResult(
                RemoveDocumentResultStatus.WorkspaceMembershipCleanupFailed,
                "Document removal is incomplete because workspace membership cleanup failed.");
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
            _logger.LogSafeFailure(
                LogLevel.Error,
                DiagnosticOperationCodes.DocumentRemovalMetadataDeletionFailure,
                LogMessages.DocumentMetadataDeletionFailed,
                ex);

            return new RemoveDocumentResult(
                RemoveDocumentResultStatus.MetadataDeletionFailed,
                "Document removal is incomplete because metadata cleanup failed.");
        }

        _logger.LogInformation(
            LogMessages.DocumentRemovalCompleted);

        return new RemoveDocumentResult(
            RemoveDocumentResultStatus.Success,
            "Document removed successfully.");
    }
}
