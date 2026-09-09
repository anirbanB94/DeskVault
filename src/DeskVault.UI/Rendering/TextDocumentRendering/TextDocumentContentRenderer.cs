using System.Text;

namespace DeskVault.UI.Rendering.TextDocumentRendering;

public sealed class TextDocumentContentRenderer
    : IDocumentContentRenderer
{
    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt",
        ".json",
        ".xml",
        ".yaml",
        ".yml",
        ".ini",
        ".config",
        ".log",
        ".c",
        ".cpp",
        ".h",
        ".hpp",
        ".cs",
        ".java",
        ".py",
        ".js",
        ".ts",
        ".css",
        ".sql",
        ".ps1"
    };

    public int Priority => 0;

    public TextDocumentContentRenderer()
    {
    }

    public bool CanRender(string fileName)
    {
        return SupportedExtensions.Contains(
            Path.GetExtension(fileName));
    }

    public async Task RenderAsync(
        Control contentHost,
        Stream documentStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contentHost);
        ArgumentNullException.ThrowIfNull(documentStream);

        cancellationToken.ThrowIfCancellationRequested();

        using var reader = new StreamReader(
            documentStream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 1024,
            leaveOpen: true);

        string text =
            await reader.ReadToEndAsync(
                cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        string displayText =
            NormalizeLineEndings(text);

        DocumentContentHost.Clear(contentHost);

        var textBox = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            Dock = DockStyle.Fill,
            Text = displayText,
            WordWrap = false
        };

        contentHost.Controls.Add(textBox);
    }

    private static string NormalizeLineEndings(
        string text)
    {
        return text
            .Replace(
                "\r\n",
                "\n")
            .Replace(
                "\r",
                "\n")
            .Replace(
                "\n",
                Environment.NewLine);
    }
}
