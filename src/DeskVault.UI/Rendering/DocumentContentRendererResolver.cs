namespace DeskVault.UI.Rendering;

public sealed class DocumentContentRendererResolver
    : IDocumentContentRendererResolver
{
    private readonly IReadOnlyList<IDocumentContentRenderer> _renderers;

    public DocumentContentRendererResolver(
        IEnumerable<IDocumentContentRenderer> renderers)
    {
        _renderers =
            [.. renderers.OrderByDescending(renderer => renderer.Priority)];
    }

    public IDocumentContentRenderer Resolve(string fileName)
    {
        IDocumentContentRenderer? renderer =
            _renderers.FirstOrDefault(
                candidate => candidate.CanRender(fileName)) ?? throw new DocumentContentRendererNotFoundException(
                fileName);
        return renderer;
    }
}
