using DeskVault.Application.Interfaces;

namespace DeskVault.UI.Tests.TestDoubles;

/// <summary>
/// Provides a no-op implementation of document-artifact coordination
/// for UI tests that do not exercise cross-process coordination behavior.
/// </summary>
/// <remarks>
/// This test double allows UI tests to construct application handlers
/// without depending on filesystem-based coordination infrastructure.
/// Tests that verify actual coordination behavior must use the real
/// coordinator through the integration-test composition.
/// </remarks>
internal sealed class NoOpDocumentArtifactCoordinator : IDocumentArtifactCoordinator
{
    /// <summary>
    /// Gets the shared stateless instance of the no-op coordinator.
    /// </summary>
    public static NoOpDocumentArtifactCoordinator Instance { get; } = new();

    private NoOpDocumentArtifactCoordinator()
    {
    }

    /// <summary>
    /// Returns a no-op lease for the requested document.
    /// </summary>
    /// <param name="documentId">
    /// The document identifier for which coordination would normally be acquired.
    /// </param>
    /// <param name="cancellationToken">
    /// The token used to cancel the acquisition operation.
    /// </param>
    /// <returns>
    /// A completed task containing a no-op asynchronous lease.
    /// </returns>
    /// <remarks>
    /// The document identifier is intentionally not used because this
    /// implementation does not provide mutual exclusion or filesystem locking.
    /// Cancellation is still honored before returning the lease.
    /// </remarks>
    public Task<IAsyncDisposable> AcquireAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        // Preserve the coordinator contract's cancellation behavior.
        cancellationToken.ThrowIfCancellationRequested();

        // UI tests do not require a real coordination resource.
        return Task.FromResult<IAsyncDisposable>(
            NoOpLease.Instance);
    }

    /// <summary>
    /// Represents a lease whose asynchronous disposal performs no operation.
    /// </summary>
    private sealed class NoOpLease : IAsyncDisposable
    {
        /// <summary>
        /// Gets the shared stateless no-op lease instance.
        /// </summary>
        public static NoOpLease Instance { get; } = new();

        /// <summary>
        /// Completes lease disposal without releasing an external resource.
        /// </summary>
        /// <returns>A successfully completed value task.</returns>
        public ValueTask DisposeAsync()
        {
            // No resource was acquired, so there is nothing to release.
            return ValueTask.CompletedTask;
        }
    }
}
