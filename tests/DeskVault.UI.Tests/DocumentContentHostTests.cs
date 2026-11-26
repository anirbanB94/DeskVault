using DeskVault.UI.Rendering;

namespace DeskVault.UI.Tests;

/// <summary>
/// Verifies synchronous and asynchronous cleanup of document-host controls.
/// </summary>
public sealed class DocumentContentHostTests
{
    /// <summary>
    /// Verifies that asynchronous resources nested inside intermediate
    /// containers are disposed before their containing control hierarchy.
    /// </summary>
    [Fact]
    public async Task ClearAsync_WithNestedAsyncResources_DisposesAllResources()
    {
        // Arrange
        List<string> disposalOrder = [];

        using var contentHost =
            new Panel();

        var renderHost =
            new Panel();

        var previewSurface =
            new AsyncDisposablePanel(
                "preview",
                disposalOrder);

        renderHost.Controls.Add(previewSurface);
        contentHost.Controls.Add(renderHost);

        // Act
        await DocumentContentHost.ClearAsync(
            contentHost);

        // Assert
        Assert.Equal(
            ["preview"],
            disposalOrder);

        Assert.Empty(contentHost.Controls);
        Assert.True(previewSurface.IsDisposed);
        Assert.True(renderHost.IsDisposed);
    }

    /// <summary>
    /// Verifies that cleanup continues for remaining resources after
    /// an asynchronous child-disposal operation fails.
    /// </summary>
    [Fact]
    public async Task ClearAsync_WhenResourceCleanupFails_ContinuesAndReportsFailure()
    {
        // Arrange
        List<string> disposalOrder = [];

        using var contentHost =
            new Panel();

        var failingPreviewSurface =
            new AsyncDisposablePanel(
                "failing-preview",
                disposalOrder,
                throwOnDispose: true);

        var remainingPreviewSurface =
            new AsyncDisposablePanel(
                "remaining-preview",
                disposalOrder);

        contentHost.Controls.Add(failingPreviewSurface);
        contentHost.Controls.Add(remainingPreviewSurface);

        // Act
        AggregateException exception =
            await Assert.ThrowsAsync<AggregateException>(
                () => DocumentContentHost.ClearAsync(
                    contentHost));

        // Assert
        Assert.Single(exception.InnerExceptions);

        Assert.Equal(
            ["failing-preview", "remaining-preview"],
            disposalOrder);

        Assert.Empty(contentHost.Controls);
        Assert.True(failingPreviewSurface.IsDisposed);
        Assert.True(remainingPreviewSurface.IsDisposed);
    }

    /// <summary>
    /// Verifies that synchronous cleanup continues to dispose every
    /// direct child control.
    /// </summary>
    [Fact]
    public void Clear_DisposesAllDirectChildControls()
    {
        // Arrange
        using var contentHost =
            new Panel();

        var firstControl =
            new Panel();

        var secondControl =
            new Panel();

        contentHost.Controls.Add(firstControl);
        contentHost.Controls.Add(secondControl);

        // Act
        DocumentContentHost.Clear(
            contentHost);

        // Assert
        Assert.Empty(contentHost.Controls);
        Assert.True(firstControl.IsDisposed);
        Assert.True(secondControl.IsDisposed);
    }

    /// <summary>
    /// Provides a control with observable asynchronous disposal behavior.
    /// </summary>
    private sealed class AsyncDisposablePanel : Panel, IAsyncDisposable
    {
        private readonly string _name;

        private readonly ICollection<string> _disposalOrder;

        private readonly bool _throwOnDispose;

        /// <summary>
        /// Initializes the test control with its cleanup behavior.
        /// </summary>
        internal AsyncDisposablePanel(
            string name,
            ICollection<string> disposalOrder,
            bool throwOnDispose = false)
        {
            _name = name;
            _disposalOrder = disposalOrder;
            _throwOnDispose = throwOnDispose;
        }

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            _disposalOrder.Add(_name);

            if (_throwOnDispose)
            {
                return ValueTask.FromException(
                    new InvalidOperationException(
                        $"Cleanup failed for '{_name}'."));
            }

            Dispose();

            return ValueTask.CompletedTask;
        }
    }
}
