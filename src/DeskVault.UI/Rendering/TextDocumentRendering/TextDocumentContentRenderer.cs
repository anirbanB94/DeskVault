using DeskVault.Application.Documents.Extraction;
using DeskVault.Application.Documents.Extraction.TextDocument;

namespace DeskVault.UI.Rendering.TextDocumentRendering;

public sealed class TextDocumentContentRenderer
    : IDocumentContentRenderer
{
    private readonly TextDocumentTextExtractor _extractor;

    public TextDocumentContentRenderer(
        TextDocumentTextExtractor extractor)
    {
        _extractor = extractor;
    }

    public int Priority => 0;

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
        DocumentTextExtractionResult extractionResult =
            await _extractor.ExtractAsync(
                documentStream,
                fileName,
                cancellationToken);

        string content = extractionResult.Text;

        cancellationToken.ThrowIfCancellationRequested();

        DocumentContentHost.Clear(contentHost);

        var textBox = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            Dock = DockStyle.Fill,
            Text = content,
            WordWrap = false
        };

        contentHost.Controls.Add(textBox);
    }
}
