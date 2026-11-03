namespace DeskVault.Infrastructure.Security.Authentication;

/// <summary>
/// Supplies the active DeskVault window handle required to own
/// the Windows Hello desktop verification prompt.
/// </summary>
public interface IWindowsHelloWindowHandleProvider
{
    /// <summary>
    /// Gets the active DeskVault window handle.
    /// </summary>
    /// <returns>
    /// A valid window handle, or <see cref="IntPtr.Zero"/> when
    /// no suitable application window is currently available.
    /// </returns>
    IntPtr GetWindowHandle();
}
