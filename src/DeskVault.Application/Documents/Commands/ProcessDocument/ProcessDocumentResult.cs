namespace DeskVault.Application.Documents.Commands.ProcessDocument;

public sealed record ProcessDocumentResult(
    ProcessDocumentResultStatus Status,
    Guid? DocumentId,
    string Description);
