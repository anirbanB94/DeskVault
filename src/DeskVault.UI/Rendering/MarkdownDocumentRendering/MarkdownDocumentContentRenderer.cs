using Markdig;
using Microsoft.Extensions.Options;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Text;

namespace DeskVault.UI.Rendering.MarkdownDocumentRendering;

public sealed class MarkdownDocumentContentRenderer
    : IDocumentContentRenderer
{
    private readonly MarkdownRenderingOptions _options;

    private readonly MarkdownPipeline _pipeline;

    public int Priority => 0;

    public MarkdownDocumentContentRenderer(
        IOptions<MarkdownRenderingOptions> options)
    {
        _options = options.Value;

        var pipelineBuilder =
            new MarkdownPipelineBuilder()
                .UseAdvancedExtensions();

        if (!_options.AllowRawHtml)
        {
            pipelineBuilder.DisableHtml();
        }

        _pipeline = pipelineBuilder.Build();
    }

    public bool CanRender(string fileName)
    {
        return string.Equals(
            Path.GetExtension(fileName),
            ".md",
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

        string markdown = await reader.ReadToEndAsync(
            cancellationToken);

        string html = Markdown.ToHtml(
            markdown,
            _pipeline);

        html = WrapHtmlDocument(html);

        cancellationToken.ThrowIfCancellationRequested();

        DocumentContentHost.Clear(contentHost);

        var webView = new WebView2
        {
            Dock = DockStyle.Fill
        };

        contentHost.Controls.Add(webView);

        try
        {
            await webView.EnsureCoreWebView2Async();

            cancellationToken.ThrowIfCancellationRequested();

            webView.CoreWebView2.Settings.IsScriptEnabled = false;

            webView.CoreWebView2.AddWebResourceRequestedFilter(
                "*",
                CoreWebView2WebResourceContext.All,
                CoreWebView2WebResourceRequestSourceKinds.Document);

            webView.CoreWebView2.WebResourceRequested +=
                (_, args) =>
                {
                    if (_options.AllowExternalResources)
                    {
                        return;
                    }

                    if (!Uri.TryCreate(
                            args.Request.Uri,
                            UriKind.Absolute,
                            out Uri? resourceUri))
                    {
                        return;
                    }

                    if (resourceUri.Scheme is
                        "http" or
                        "https")
                    {
                        args.Response =
                            webView.CoreWebView2.Environment
                                .CreateWebResourceResponse(
                                    null,
                                    403,
                                    "Blocked",
                                    "Content-Type: text/plain");
                    }
                };

            webView.CoreWebView2.NavigationStarting +=
                (_, args) =>
                {
                    if (!Uri.TryCreate(
                            args.Uri,
                            UriKind.Absolute,
                            out Uri? navigationUri))
                    {
                        args.Cancel = true;
                        return;
                    }

                    if (navigationUri.Scheme is
                        "http" or
                        "https")
                    {
                        if (!_options.AllowExternalNavigation)
                        {
                            args.Cancel = true;
                        }
                    }
                };

            webView.NavigateToString(html);
        }
        catch
        {
            webView.Dispose();
            throw;
        }
    }

    private static string WrapHtmlDocument(
        string content)
    {
        return
            $$"""
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset="utf-8" />
                <meta
                    name="viewport"
                    content="width=device-width, initial-scale=1.0" />

                <style>
                    :root {
                        color-scheme: light;

                        --dv-background: #ffffff;
                        --dv-foreground: #1f1f1f;
                        --dv-muted: #666666;
                        --dv-border: #d9d9d9;
                        --dv-code-background: #f5f5f5;
                        --dv-link: #0067c5;
                    }

                    * {
                        box-sizing: border-box;
                    }

                    html,
                    body {
                        margin: 0;
                        padding: 0;
                        background: var(--dv-background);
                        color: var(--dv-foreground);
                    }

                    body {
                        font-family:
                            "Segoe UI",
                            system-ui,
                            -apple-system,
                            BlinkMacSystemFont,
                            sans-serif;

                        font-size: 15px;
                        line-height: 1.6;
                        padding: 28px 32px;
                    }

                    h1,
                    h2,
                    h3,
                    h4,
                    h5,
                    h6 {
                        line-height: 1.25;
                        margin-top: 1.5em;
                        margin-bottom: 0.6em;
                    }

                    h1 {
                        margin-top: 0;
                        font-size: 2em;
                    }

                    h2 {
                        font-size: 1.6em;
                    }

                    h3 {
                        font-size: 1.3em;
                    }

                    p {
                        margin: 0.7em 0;
                    }

                    a {
                        color: var(--dv-link);
                    }

                    code {
                        font-family:
                            "Cascadia Code",
                            "Consolas",
                            monospace;

                        background: var(--dv-code-background);
                        padding: 0.15em 0.35em;
                        border-radius: 4px;
                    }

                    pre {
                        background: var(--dv-code-background);
                        border: 1px solid var(--dv-border);
                        border-radius: 6px;
                        padding: 16px;
                        overflow-x: auto;
                    }

                    pre code {
                        background: transparent;
                        padding: 0;
                    }

                    blockquote {
                        margin: 1em 0;
                        padding: 0.5em 1em;
                        border-left: 4px solid var(--dv-border);
                        color: var(--dv-muted);
                    }

                    table {
                        border-collapse: collapse;
                        margin: 1em 0;
                        width: auto;
                        max-width: 100%;
                    }

                    th,
                    td {
                        border: 1px solid var(--dv-border);
                        padding: 8px 12px;
                        text-align: left;
                    }

                    th {
                        font-weight: 600;
                        background: var(--dv-code-background);
                    }

                    img {
                        max-width: 100%;
                        height: auto;
                    }

                    hr {
                        border: 0;
                        border-top: 1px solid var(--dv-border);
                        margin: 2em 0;
                    }
                </style>
            </head>

            <body>
                {{content}}
            </body>
            </html>
            """;
    }
}
