namespace DeskVault.UI.Rendering;

public interface IDocumentContentRenderer
{
    int Priority { get; }

    bool CanRender(string fileName);

    Task RenderAsync(
        Control contentHost,
        Stream documentStream,
        string fileName,
        CancellationToken cancellationToken = default);
}
