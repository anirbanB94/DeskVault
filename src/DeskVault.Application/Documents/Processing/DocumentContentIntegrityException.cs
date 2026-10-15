namespace DeskVault.Application.Documents.Processing;

/// <summary>
/// Indicates that decrypted document content does not match its persisted
/// SHA-256 content identity.
/// </summary>
public sealed class DocumentContentIntegrityException : InvalidOperationException
{
    public DocumentContentIntegrityException()
        : base(
            "The decrypted document content does not match the persisted document content identity.")
    {
    }
}
