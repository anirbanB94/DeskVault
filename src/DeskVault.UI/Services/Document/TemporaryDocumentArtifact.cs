namespace DeskVault.UI.Services.Document;

/// <summary>
/// Describes the outcome of a temporary document artifact cleanup attempt.
/// </summary>
/// <param name="Succeeded">Indicates whether the artifact was successfully cleaned up.</param>
/// <param name="FailureCategory">
/// The exception type associated with the first cleanup failure, or <see langword="null"/>
/// when cleanup succeeds.
/// </param>
internal readonly record struct TemporaryDocumentArtifactCleanupResult(
    bool Succeeded,
    string? FailureCategory)
{
    /// <summary>
    /// Creates a successful cleanup result.
    /// </summary>
    public static TemporaryDocumentArtifactCleanupResult Success()
    {
        return new TemporaryDocumentArtifactCleanupResult(
            Succeeded: true,
            FailureCategory: null);
    }

    /// <summary>
    /// Creates a failed cleanup result without retaining exception details or file paths.
    /// </summary>
    /// <param name="failureCategory">The exception type associated with the failure.</param>
    public static TemporaryDocumentArtifactCleanupResult Failure(
        string failureCategory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(failureCategory);

        return new TemporaryDocumentArtifactCleanupResult(
            Succeeded: false,
            FailureCategory: failureCategory);
    }
}

/// <summary>
/// Owns the writable stream and cleanup lifecycle of one temporary plaintext document artifact.
/// </summary>
/// <remarks>
/// The artifact is created by <see cref="TemporaryDocumentArtifactStore"/> with an explicit
/// access-control policy. The source document stream supplied to <see cref="CopyFromAsync"/>
/// remains owned by the caller.
/// </remarks>
internal sealed class TemporaryDocumentArtifact
{
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);

    private FileStream? _writeStream;
    private bool _cleanupCompleted;

    /// <summary>
    /// Initializes an artifact using its assigned identity, path and exclusively owned write stream.
    /// </summary>
    /// <param name="id">The artifact identity.</param>
    /// <param name="filePath">The artifact's fully qualified file path.</param>
    /// <param name="writeStream">The writable stream owned by this artifact.</param>
    internal TemporaryDocumentArtifact(
        Guid id,
        string filePath,
        FileStream writeStream)
    {
        ArgumentNullException.ThrowIfNull(writeStream);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "A non-empty artifact identity is required.",
                nameof(id));
        }

        Id = id;
        FilePath = filePath;
        _writeStream = writeStream;
    }

    /// <summary>
    /// Gets the stable identity assigned to this artifact.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Gets the fully qualified path of the temporary artifact.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Copies document content into the artifact and closes its write stream.
    /// </summary>
    /// <param name="documentStream">The caller-owned source document stream.</param>
    /// <param name="cancellationToken">The cancellation token for the copy operation.</param>
    /// <remarks>
    /// The write stream is flushed and closed before this operation completes successfully.
    /// Closing the stream prevents the external viewer from observing partially written content.
    /// </remarks>
    public async Task CopyFromAsync(
        Stream documentStream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documentStream);

        await _lifecycleGate.WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            ObjectDisposedException.ThrowIf(
                _cleanupCompleted,
                this);

            FileStream writeStream =
                _writeStream ??
                throw new InvalidOperationException(
                    "The temporary artifact is no longer writable.");

            try
            {
                await documentStream.CopyToAsync(
                    writeStream,
                    cancellationToken).ConfigureAwait(false);

                await writeStream.FlushAsync(
                    cancellationToken).ConfigureAwait(false);

                await writeStream.DisposeAsync()
                    .ConfigureAwait(false);

                _writeStream = null;
            }
            catch
            {
                try
                {
                    await writeStream.DisposeAsync()
                        .ConfigureAwait(false);

                    _writeStream = null;
                }
                catch
                {
                    // Retain the stream reference when disposal fails.
                    // TryCleanup must remain able to retry releasing the owned handle.
                }

                // Preserve the original copy or flush exception.
                throw;
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    /// <summary>
    /// Attempts to close the owned write stream and delete the artifact.
    /// </summary>
    /// <returns>
    /// A cleanup result that identifies success or the category of the first failure.
    /// </returns>
    /// <remarks>
    /// Cleanup is serialized with copy operations and can be retried after a failure.
    /// Stream ownership is retained when disposal fails so a later attempt can retry it.
    /// </remarks>
    public TemporaryDocumentArtifactCleanupResult TryCleanup()
    {
        _lifecycleGate.Wait();

        try
        {
            if (_cleanupCompleted)
            {
                return TemporaryDocumentArtifactCleanupResult.Success();
            }

            string? failureCategory = null;

            if (_writeStream is not null)
            {
                try
                {
                    _writeStream.Dispose();
                    _writeStream = null;
                }
                catch (Exception exception)
                {
                    // Keep the stream reference: disposal may be retried by the owner.
                    failureCategory = exception.GetType().Name;
                }
            }

            try
            {
                // Avoid a separate existence check, which could hide access failures.
                File.Delete(FilePath);
            }
            catch (Exception exception)
            {
                failureCategory ??= exception.GetType().Name;
            }

            if (failureCategory is not null)
            {
                return TemporaryDocumentArtifactCleanupResult.Failure(
                    failureCategory);
            }

            _cleanupCompleted = true;

            return TemporaryDocumentArtifactCleanupResult.Success();
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }
}
