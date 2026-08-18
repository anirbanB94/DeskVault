namespace DeskVault.UI.Rendering;

public static class DocumentContentHost
{
    public static void Clear(Control contentHost)
    {
        Control[] existingControls =
            contentHost.Controls
                .Cast<Control>()
                .ToArray();

        foreach (Control control in existingControls)
        {
            control.Dispose();
        }

        contentHost.Controls.Clear();
    }
}
