namespace DeskVault.UI.Rendering;

/// <summary>
/// Provides deterministic cleanup for controls hosting document content.
/// </summary>
public static class DocumentContentHost
{
    /// <summary>
    /// Synchronously disposes all controls hosted directly by the supplied control.
    /// HTML preview surfaces initiate their asynchronous profile cleanup on disposal.
    /// </summary>
    /// <param name="contentHost">The control whose child controls are cleared.</param>
    public static void Clear(
        Control contentHost)
    {
        ArgumentNullException.ThrowIfNull(contentHost);

        Control[] existingControls =
            [.. contentHost.Controls.Cast<Control>()];

        foreach (Control control in existingControls)
        {
            control.Dispose();
        }

        contentHost.Controls.Clear();
    }

    /// <summary>
    /// Awaits asynchronous resource cleanup throughout the hosted control tree
    /// before disposing its direct child controls.
    /// </summary>
    /// <param name="contentHost">The control whose child controls are cleared.</param>
    /// <returns>A task representing completion of the cleanup operation.</returns>
    /// <exception cref="AggregateException">
    /// Thrown after all child cleanup has been attempted when one or more
    /// asynchronous cleanup or control-disposal operations fail.
    /// </exception>
    public static async Task ClearAsync(
        Control contentHost)
    {
        ArgumentNullException.ThrowIfNull(contentHost);

        var failures =
            new List<Exception>();

        Control[] existingControls =
            [.. contentHost.Controls.Cast<Control>()];

        foreach (Control control in existingControls)
        {
            await DisposeAsyncResourcesAsync(
                control,
                failures);

            try
            {
                if (!control.IsDisposed)
                {
                    control.Dispose();
                }
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        contentHost.Controls.Clear();

        if (failures.Count > 0)
        {
            throw new AggregateException(
                "One or more document presentation resources could not be disposed.",
                failures);
        }
    }

    /// <summary>
    /// Awaits asynchronous disposal for every asynchronous resource owner
    /// in the supplied control's descendant hierarchy.
    /// </summary>
    private static async Task DisposeAsyncResourcesAsync(
        Control control,
        ICollection<Exception> failures)
    {
        Control[] childControls =
            [.. control.Controls.Cast<Control>()];

        foreach (Control childControl in childControls)
        {
            await DisposeAsyncResourcesAsync(
                childControl,
                failures);
        }

        if (control is not IAsyncDisposable asyncDisposable)
        {
            return;
        }

        try
        {
            await asyncDisposable.DisposeAsync();
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }
    }
}
