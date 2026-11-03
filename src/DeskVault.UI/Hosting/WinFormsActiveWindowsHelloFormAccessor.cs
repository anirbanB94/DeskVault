namespace DeskVault.UI.Hosting;

/// <summary>
/// Resolves the currently active WinForms application form.
/// </summary>
internal sealed class WinFormsActiveWindowsHelloFormAccessor
    : IActiveWindowsHelloFormAccessor
{
    /// <inheritdoc />
    public Form? GetActiveForm()
    {
        return Form.ActiveForm;
    }
}
