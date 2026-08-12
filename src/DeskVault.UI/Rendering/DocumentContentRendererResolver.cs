namespace DeskVault.UI.Rendering;

public sealed class DocumentContentRendererResolver
    : IDocumentContentRendererResolver
{
    private readonly IReadOnlyList<IDocumentContentRenderer> _renderers;

    public DocumentContentRendererResolver(
        IEnumerable<IDocumentContentRenderer> renderers)
    {
        _renderers = renderers.ToList();
    }

    public IDocumentContentRenderer Resolve(string fileName)
    {
        IDocumentContentRenderer? renderer =
            _renderers.FirstOrDefault(
                candidate => candidate.CanRender(fileName));

        if (renderer is null)
        {
            throw new NotSupportedException(
                $"No document renderer is available for '{fileName}'.");
        }

        return renderer;
    }
}
