namespace DeskVault.Application.Documents.Commands.ImportDocument;

public sealed record ImportDocumentResult(
    ImportDocumentResultStatus Status,
    Guid? DocumentId,
    string Description);