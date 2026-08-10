namespace DeskVault.Application.Documents.Commands.ImportDocument;

public sealed record ImportDocumentCommand(
    string FilePath,
    string? DisplayName);