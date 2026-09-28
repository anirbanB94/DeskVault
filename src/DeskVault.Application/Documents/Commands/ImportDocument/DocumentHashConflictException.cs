namespace DeskVault.Application.Documents.Commands.ImportDocument;

public sealed class DocumentHashConflictException
    : InvalidOperationException
{
    public DocumentHashConflictException(
        Exception innerException)
        : base(
            "The document could not be persisted because another document with the same content hash already exists.",
            innerException)
    {
    }
}
