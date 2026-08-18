namespace DeskVault.UI.Rendering.MarkdownDocumentRendering;

public sealed class MarkdownRenderingOptions
{
    public const string SectionName = "MarkdownRendering";

    public bool AllowRawHtml { get; init; }

    public bool AllowExternalResources { get; init; }

    public bool AllowExternalNavigation { get; init; }
}
