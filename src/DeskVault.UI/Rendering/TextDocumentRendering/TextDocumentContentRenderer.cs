using DeskVault.Application.Documents.Extraction;
using DeskVault.Application.Documents.Extraction.TextDocument;

namespace DeskVault.UI.Rendering.TextDocumentRendering;

public sealed class TextDocumentContentRenderer
    : IDocumentContentRenderer
{
    private readonly DocumentTextExtractorResolver _extractorResolver;

    public int Priority => 0;

    public TextDocumentContentRenderer(
        DocumentTextExtractorResolver extractorResolver)
    {
        _extractorResolver = extractorResolver;
    }

    public bool CanRender(string fileName)
    {
        return string.Equals(
            Path.GetExtension(fileName),
            ".txt",
            StringComparison.OrdinalIgnoreCase);
    }

    public async Task RenderAsync(
        Control contentHost,
        Stream documentStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {

        IDocumentTextExtractor extractor =
            _extractorResolver.Resolve(fileName);

        DocumentTextExtractionResult result =
            await _extractorResolver.Resolve(fileName).ExtractAsync(
                documentStream,
                fileName,
                cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        DocumentContentHost.Clear(contentHost);

        var textBox = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            Dock = DockStyle.Fill,
            Text = result.Text,
            WordWrap = false
        };

        contentHost.Controls.Add(textBox);
    }
}
