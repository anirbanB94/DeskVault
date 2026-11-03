using DeskVault.Infrastructure.Security.Authentication;

namespace DeskVault.UI.Hosting;

/// <summary>
/// Supplies a valid handle from the currently active WinForms window
/// to the Infrastructure Windows Hello authentication provider.
/// </summary>
internal sealed class WindowsHelloWindowHandleProvider
    : IWindowsHelloWindowHandleProvider
{
    private readonly IActiveWindowsHelloFormAccessor _activeFormAccessor;

    public WindowsHelloWindowHandleProvider(
        IActiveWindowsHelloFormAccessor activeFormAccessor)
    {
        ArgumentNullException.ThrowIfNull(activeFormAccessor);

        _activeFormAccessor = activeFormAccessor;
    }

    /// <inheritdoc />
    public IntPtr GetWindowHandle()
    {
        Form? activeForm =
            _activeFormAccessor.GetActiveForm();

        if (activeForm is null ||
            activeForm.IsDisposed ||
            activeForm.Disposing ||
            !activeForm.IsHandleCreated)
        {
            return IntPtr.Zero;
        }

        try
        {
            return activeForm.Handle;
        }
        catch (ObjectDisposedException)
        {
            // The active form may be disposed between the lifecycle
            // checks and the handle read. A stale handle must never
            // be returned to the authentication provider.
            return IntPtr.Zero;
        }
    }
}
