namespace DeskVault.Application.Documents.Commands.ImportDocument;

public sealed class DocumentImportContentMismatchException
    : InvalidOperationException
{
    public DocumentImportContentMismatchException()
        : base(
            "The document content changed during import and no longer matches the calculated content hash.")
    {
    }
}
