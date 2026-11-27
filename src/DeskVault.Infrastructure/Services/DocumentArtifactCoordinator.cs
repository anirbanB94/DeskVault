using DeskVault.Application.Interfaces;

namespace DeskVault.Infrastructure.Services;

/// <summary>
/// Coordinates document-artifact lifecycle operations across processes
/// using exclusive operating-system file handles.
/// </summary>
/// <remarks>
/// Each coordinator instance is scoped to the configured vault through its
/// document-artifact lease path resolver. Ownership is specific to one
/// document ID within that vault.
///
/// The coordination file is persistent and contains no document content,
/// encryption keys, or ownership metadata. The exclusive operating-system
/// file handle is the authoritative ownership boundary.
/// </remarks>
public sealed class DocumentArtifactCoordinator : IDocumentArtifactCoordinator
{
    private const int ErrorSharingViolation = 32;

    private const int LeaseRetryDelayMilliseconds = 100;
    private const int LeaseFileBufferSize = 1;

    private static readonly TimeSpan LeaseRetryDelay =
        TimeSpan.FromMilliseconds(
            LeaseRetryDelayMilliseconds);

    private readonly DocumentArtifactLeasePathResolver _leasePathResolver;

    /// <summary>
    /// Initializes the coordinator for the configured vault.
    /// </summary>
    /// <param name="leasePathResolver">
    /// Resolves the persistent coordination-file path for a document.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// The supplied lease path resolver is null.
    /// </exception>
    public DocumentArtifactCoordinator(
        DocumentArtifactLeasePathResolver leasePathResolver)
    {
        ArgumentNullException.ThrowIfNull(leasePathResolver);

        _leasePathResolver = leasePathResolver;
    }

    /// <summary>
    /// Asynchronously acquires an exclusive lease for a document-artifact
    /// lifecycle operation.
    /// </summary>
    /// <param name="documentId">
    /// The non-empty identity of the document being coordinated.
    /// </param>
    /// <param name="cancellationToken">
    /// A token used to cancel acquisition while waiting for the lease.
    /// </param>
    /// <returns>
    /// An asynchronously disposable lease representing exclusive ownership
    /// of the document-artifact coordination resource.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// The document identifier is empty.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Acquisition was cancelled before the lease was granted.
    /// </exception>
    /// <exception cref="IOException">
    /// An unexpected filesystem coordination failure prevented acquisition.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">
    /// Access to the coordination resource was denied.
    /// </exception>
    /// <remarks>
    /// Only the expected Windows sharing violation is treated as contention.
    /// All other filesystem failures propagate to the caller so that a
    /// protected operation cannot continue without a lease.
    ///
    /// The returned lease must remain alive for the complete protected
    /// lifecycle operation and must be disposed on every exit path.
    /// </remarks>
    public async Task<IAsyncDisposable> AcquireAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        if (documentId == Guid.Empty)
        {
            throw new ArgumentException(
                "Document ID cannot be empty.",
                nameof(documentId));
        }

        cancellationToken.ThrowIfCancellationRequested();

        string leaseFilePath =
            _leasePathResolver.GetPath(
                documentId);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            FileStream leaseStream;

            try
            {
                // The exclusive share mode is the cross-process ownership
                // boundary. The file is opened without writing any content.
                leaseStream =
                    new FileStream(
                        leaseFilePath,
                        FileMode.OpenOrCreate,
                        FileAccess.ReadWrite,
                        FileShare.None,
                        LeaseFileBufferSize,
                        FileOptions.None);
            }
            catch (IOException exception) when (IsSharingViolation(exception))
            {
                // An existing owner holds the exclusive handle. Wait without
                // blocking the calling thread and observe cancellation.
                await Task.Delay(
                    LeaseRetryDelay,
                    cancellationToken).ConfigureAwait(false);

                continue;
            }

            // Do not grant ownership if cancellation arrived immediately
            // after the operating-system handle was successfully acquired.
            if (cancellationToken.IsCancellationRequested)
            {
                await leaseStream.DisposeAsync().ConfigureAwait(false);

                cancellationToken.ThrowIfCancellationRequested();
            }

            return new DocumentArtifactLease(
                leaseStream);
        }
    }

    /// <summary>
    /// Determines whether an I/O exception represents the Windows sharing
    /// violation used for exclusive-file-handle contention.
    /// </summary>
    /// <param name="exception">
    /// The filesystem exception raised while opening the coordination file.
    /// </param>
    /// <returns>
    /// True only when the native Windows error code is 32.
    /// </returns>
    private static bool IsSharingViolation(
        IOException exception)
    {
        // Windows ERROR_SHARING_VIOLATION is 32. Other I/O failures must
        // propagate instead of being retried as though another owner exists.
        return
            (exception.HResult & 0xFFFF) ==
            ErrorSharingViolation;
    }

    /// <summary>
    /// Represents ownership of one document-artifact coordination file.
    /// </summary>
    /// <remarks>
    /// Ownership lasts while the underlying file handle remains open.
    /// Disposing the lease releases that handle. The persistent coordination
    /// file itself is intentionally not deleted.
    /// </remarks>
    private sealed class DocumentArtifactLease : IAsyncDisposable
    {
        private FileStream? _leaseStream;

        /// <summary>
        /// Initializes a lease around an exclusively opened coordination stream.
        /// </summary>
        /// <param name="leaseStream">
        /// The stream whose handle represents lease ownership.
        /// </param>
        public DocumentArtifactLease(
            FileStream leaseStream)
        {
            ArgumentNullException.ThrowIfNull(leaseStream);

            _leaseStream = leaseStream;
        }

        /// <summary>
        /// Releases the exclusive operating-system handle.
        /// </summary>
        /// <returns>
        /// A value task representing completion of handle disposal.
        /// </returns>
        /// <remarks>
        /// Interlocked exchange ensures that repeated disposal releases
        /// the underlying stream at most once.
        /// </remarks>
        public ValueTask DisposeAsync()
        {
            FileStream? leaseStream =
                Interlocked.Exchange(
                    ref _leaseStream,
                    null);

            return leaseStream is null
                ? ValueTask.CompletedTask
                : leaseStream.DisposeAsync();
        }
    }
}
