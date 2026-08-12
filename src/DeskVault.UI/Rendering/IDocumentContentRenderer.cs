namespace DeskVault.UI.Rendering;

public interface IDocumentContentRenderer
{
    bool CanRender(string fileName);

    Task RenderAsync(
        Control contentHost,
        Stream documentStream,
        string fileName,
        CancellationToken cancellationToken = default);
}
