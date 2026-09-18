namespace DeskVault.Application.Documents.Commands.RemoveDocument;

public sealed record RemoveDocumentResult(
    RemoveDocumentResultStatus Status,
    string Message);
