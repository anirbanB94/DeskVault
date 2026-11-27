namespace DeskVault.Application.Interfaces;

/// <summary>
/// Provides cross-process coordination for document-artifact lifecycle
/// operations within one configured vault.
/// </summary>
/// <remarks>
/// A coordinator instance is scoped to the vault represented by its
/// Infrastructure configuration. Each acquired lease is scoped to one
/// document identifier within that vault.
///
/// The lease must remain alive for the complete protected document-artifact
/// lifecycle operation. It does not represent exclusive access to the entire
/// vault, database, application session, or document-processing workflow.
/// </remarks>
public interface IDocumentArtifactCoordinator
{
    /// <summary>
    /// Asynchronously acquires an exclusive document-artifact lifecycle
    /// lease for the specified document within the configured vault.
    /// </summary>
    /// <param name="documentId">
    /// The non-empty identity of the document whose artifact lifecycle
    /// requires coordination.
    /// </param>
    /// <param name="cancellationToken">
    /// A token used to cancel acquisition while waiting for the lease.
    /// </param>
    /// <returns>
    /// An asynchronously disposable lease representing ownership of the
    /// requested document-artifact coordination resource.
    /// </returns>
    /// <remarks>
    /// The caller owns the returned lease and must dispose it on every
    /// completion, exception, and cancellation path. The lease must not be
    /// released before the protected lifecycle operation has finished.
    ///
    /// Cancellation while waiting must stop acquisition without granting
    /// ownership to the caller.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// The document identifier is empty.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Acquisition was cancelled before the lease was obtained.
    /// </exception>
    /// <exception cref="IOException">
    /// An unexpected filesystem coordination failure prevented acquisition.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">
    /// Access to the coordination resource was denied.
    /// </exception>
    Task<IAsyncDisposable> AcquireAsync(
        Guid documentId,
        CancellationToken cancellationToken = default);
}
