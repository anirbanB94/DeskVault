namespace DeskVault.Application.Interfaces;

/// <summary>
/// Defines operations for opening document content and validating document content integrity.
/// </summary>
public interface IDocumentReader
{
    /// <summary>
    /// Opens a document's decrypted content for reading after validating its integrity.
    /// </summary>
    /// <param name="documentId">The document identifier.</param>
    /// <param name="expectedSha256Hash">The expected SHA-256 hash of the document content.</param>
    /// <param name="maximumPlaintextBytes">The positive maximum number of plaintext bytes allowed.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A readable stream containing the complete decrypted document content after integrity validation.</returns>
    Task<Stream> OpenReadAsync(
        Guid documentId,
        string expectedSha256Hash,
        long maximumPlaintextBytes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Decrypts and validates the document content identity incrementally,
    /// without returning a plaintext stream.
    /// </summary>
    /// <param name="documentId">The document identifier.</param>
    /// <param name="expectedSha256Hash">The expected SHA-256 hash of the document content.</param>
    /// <param name="maximumPlaintextBytes">The maximum number of plaintext bytes allowed during validation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous validation operation.</returns>
    Task ValidateContentHashAsync(
        Guid documentId,
        string expectedSha256Hash,
        long maximumPlaintextBytes,
        CancellationToken cancellationToken = default);
}
