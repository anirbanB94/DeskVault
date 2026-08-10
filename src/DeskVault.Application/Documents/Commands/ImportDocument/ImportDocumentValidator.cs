using DeskVault.Application.Documents;

namespace DeskVault.Application.Documents.Commands.ImportDocument;

public sealed class ImportDocumentValidator : IImportDocumentValidator
{
    public ImportDocumentResult Validate(ImportDocumentCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.FilePath))
        {
            return new ImportDocumentResult(
                ImportDocumentResultStatus.ValidationFailed,
                null,
                "File path is required.");
        }

        if (!File.Exists(command.FilePath))
        {
            return new ImportDocumentResult(
                ImportDocumentResultStatus.FileNotFound,
                null,
                "The specified file does not exist.");
        }

        string extension = Path.GetExtension(command.FilePath);

        if (!SupportedFileTypes.IsSupported(extension))
        {
            return new ImportDocumentResult(
                ImportDocumentResultStatus.UnsupportedFileType,
                null,
                $"The file type '{extension}' is not supported.");
        }

        FileInfo fileInfo = new(command.FilePath);

        if (fileInfo.Length == 0)
        {
            return new ImportDocumentResult(
                ImportDocumentResultStatus.ValidationFailed,
                null,
                "The file is empty.");
        }

        return new ImportDocumentResult(
            ImportDocumentResultStatus.Success,
            null,
            "Validation successful.");
    }
}