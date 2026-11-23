namespace DeskVault.UI.Rendering;

/// <summary>
/// Indicates that no registered document content renderer can handle
/// the requested file.
/// </summary>
public sealed class DocumentContentRendererNotFoundException
    : NotSupportedException
{
    public DocumentContentRendererNotFoundException(
        string fileName)
        : base(
            $"No document renderer is available for '{fileName}'.")
    {
    }
}
