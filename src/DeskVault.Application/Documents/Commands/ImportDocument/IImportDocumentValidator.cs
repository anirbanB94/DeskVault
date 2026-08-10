namespace DeskVault.Application.Documents.Commands.ImportDocument;

public interface IImportDocumentValidator
{
    ImportDocumentResult Validate(ImportDocumentCommand command);
}