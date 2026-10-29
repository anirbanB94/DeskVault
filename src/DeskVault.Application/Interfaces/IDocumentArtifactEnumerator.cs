namespace DeskVault.Application.Interfaces;

/// <summary>
/// Defines operations for enumerating and checking document artifacts.
/// </summary>
public interface IDocumentArtifactEnumerator
{
    /// <summary>
    /// Checks whether the canonical artifact for a document exists.
    /// </summary>
    /// <param name="documentId">The document identifier.</param>
    /// <returns><see langword="true"/> if the canonical artifact exists; otherwise, <see langword="false"/>.</returns>
    bool ExistsForDocument(Guid documentId);

    /// <summary>
    /// Enumerates document artifact paths incrementally without materializing the full inventory.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel enumeration.</param>
    /// <returns>An asynchronous sequence of document artifact paths.</returns>
    IAsyncEnumerable<string> EnumerateIncrementallyAsync(
        CancellationToken cancellationToken = default);
}
