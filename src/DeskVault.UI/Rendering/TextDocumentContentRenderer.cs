using System.Text;

namespace DeskVault.UI.Rendering;

public sealed class TextDocumentContentRenderer
    : IDocumentContentRenderer
{
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
        using var reader = new StreamReader(
            documentStream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 1024,
            leaveOpen: true);

        string content = await reader.ReadToEndAsync(
            cancellationToken);

        var textBox = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            Dock = DockStyle.Fill,
            Text = content,
            WordWrap = false
        };

        contentHost.Controls.Clear();
        contentHost.Controls.Add(textBox);
    }
}
