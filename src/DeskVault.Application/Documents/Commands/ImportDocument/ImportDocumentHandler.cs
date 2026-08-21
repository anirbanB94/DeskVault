using DeskVault.Application.Interfaces;
using DeskVault.Domain.Documents;

namespace DeskVault.Application.Documents.Commands.ImportDocument;

public sealed class ImportDocumentHandler
{
    private readonly IImportDocumentValidator _validator;
    private readonly IHashService _hashService;
    private readonly IStorageService _storageService;
    private readonly IDocumentRepository _repository;
    private readonly IDocumentProcessingService _documentProcessingService;

    public ImportDocumentHandler(
        IImportDocumentValidator validator,
        IHashService hashService,
        IStorageService storageService,
        IDocumentRepository repository,
        IDocumentProcessingService documentProcessingService)
    {
        _validator = validator;
        _hashService = hashService;
        _storageService = storageService;
        _repository = repository;
        _documentProcessingService = documentProcessingService;
    }

    public async Task<ImportDocumentResult> HandleAsync(
        ImportDocumentCommand command,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validationResult =
            _validator.Validate(command);

        if (validationResult.Status !=
            ImportDocumentResultStatus.Success)
        {
            return validationResult;
        }

        var sha256Hash =
            await _hashService.ComputeSha256Async(
                command.FilePath,
                cancellationToken);

        var exists =
            await _repository.ExistsByHashAsync(
                sha256Hash,
                cancellationToken);

        if (exists)
        {
            return new ImportDocumentResult(
                ImportDocumentResultStatus.Duplicate,
                null,
                "The document has already been imported.");
        }

        var documentId = Guid.NewGuid();

        var fileName =
            Path.GetFileName(command.FilePath);

        var displayName =
            string.IsNullOrWhiteSpace(command.DisplayName)
                ? Path.GetFileNameWithoutExtension(
                    command.FilePath)
                : command.DisplayName;

        try
        {
            var storedFilePath =
                await _storageService.StoreAsync(
                    command.FilePath,
                    documentId,
                    cancellationToken);

            var document =
                Document.Create(
                    documentId,
                    fileName,
                    displayName,
                    sha256Hash,
                    storedFilePath);

            await _repository.AddAsync(
                document,
                cancellationToken);

            await _documentProcessingService.ProcessAsync(
                document.Id,
                cancellationToken);

            return new ImportDocumentResult(
                ImportDocumentResultStatus.Success,
                document.Id,
                "Document imported successfully.");
        }
        catch (IOException ex)
        {
            return new ImportDocumentResult(
                ImportDocumentResultStatus.StorageFailed,
                null,
                ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return new ImportDocumentResult(
                ImportDocumentResultStatus.StorageFailed,
                null,
                ex.Message);
        }
    }
}
