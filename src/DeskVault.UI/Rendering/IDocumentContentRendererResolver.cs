namespace DeskVault.UI.Rendering;

public interface IDocumentContentRendererResolver
{
    IDocumentContentRenderer Resolve(string fileName);
}
