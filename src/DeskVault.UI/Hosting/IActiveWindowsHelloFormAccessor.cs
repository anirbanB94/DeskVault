namespace DeskVault.UI.Hosting;

/// <summary>
/// Provides the currently active WinForms window for Windows Hello
/// verification prompt ownership.
/// </summary>
internal interface IActiveWindowsHelloFormAccessor
{
    /// <summary>
    /// Gets the currently active DeskVault form, if one exists.
    /// </summary>
    Form? GetActiveForm();
}
