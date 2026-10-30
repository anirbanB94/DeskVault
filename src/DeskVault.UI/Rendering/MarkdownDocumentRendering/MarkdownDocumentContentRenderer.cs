using DeskVault.Application.Documents;
using DeskVault.Application.Documents.Extraction;
using Markdig;
using Microsoft.Extensions.Options;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace DeskVault.UI.Rendering.MarkdownDocumentRendering;

public sealed class MarkdownDocumentContentRenderer
    : IDocumentContentRenderer
{
    private readonly MarkdownRenderingOptions _options;

    private readonly DocumentTextExtractorResolver _extractorResolver;

    private readonly MarkdownPipeline _pipeline;

    public int Priority => 0;

    public MarkdownDocumentContentRenderer(
        IOptions<MarkdownRenderingOptions> options,
        DocumentTextExtractorResolver extractorResolver)
    {
        _options = options.Value;
        _extractorResolver = extractorResolver;

        _pipeline =
            MarkdownRenderingSecurityPolicy.CreatePipeline(
                _options.AllowRawHtml);
    }

    public bool CanRender(string fileName)
    {
        return SupportedFileTypes.GetInAppPreviewKind(fileName)
            == DocumentInAppPreviewKind.Markdown;
    }

    public async Task RenderAsync(
        Control contentHost,
        Stream documentStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {

        IDocumentTextExtractor extractor =
            _extractorResolver.Resolve(fileName);

        DocumentTextExtractionResult extractionResult =
            await extractor.ExtractAsync(
                documentStream,
                fileName,
                cancellationToken);

        string markdown = extractionResult.Text;

        string html = Markdown.ToHtml(
            markdown,
            _pipeline);

        html = WrapHtmlDocument(
            html,
            _options.AllowExternalResources);

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

            var authorizedExternalNavigationIds = new HashSet<ulong>();

            // The token is set immediately before NavigateToString and
            // consumed by the first subsequent top-level navigation event.
            bool initialDocumentNavigationPending = false;

            webView.CoreWebView2.AddWebResourceRequestedFilter(
                "*",
                CoreWebView2WebResourceContext.All,
                CoreWebView2WebResourceRequestSourceKinds.Document);

            webView.CoreWebView2.WebResourceRequested +=
                (_, args) =>
                {
                    if (MarkdownRenderingSecurityPolicy.IsResourceAllowed(
                            args.Request.Uri,
                            _options.AllowExternalResources))
                    {
                        return;
                    }

                    args.Response =
                        webView.CoreWebView2.Environment
                            .CreateWebResourceResponse(
                                null,
                                403,
                                "Blocked",
                                "Content-Type: text/plain");
                };

            webView.CoreWebView2.NavigationStarting +=
                (_, args) =>
                {
                    bool previouslyAuthorized =
                        authorizedExternalNavigationIds.Contains(
                            args.NavigationId);

                    bool isInitialDocumentNavigation =
                        initialDocumentNavigationPending
                        && MarkdownRenderingSecurityPolicy.IsInitialDocumentNavigationAllowed(
                            args.Uri,
                            initialDocumentNavigationPending,
                            args.IsUserInitiated,
                            args.IsRedirected);

                    // Consume the token on the first navigation event,
                    // whether or not that event matches the expected HTML load.
                    initialDocumentNavigationPending = false;

                    bool allowed =
                        isInitialDocumentNavigation
                        || MarkdownRenderingSecurityPolicy.IsNavigationRequestAllowed(
                            args.Uri,
                            _options.AllowExternalNavigation,
                            args.IsUserInitiated,
                            args.IsRedirected,
                            previouslyAuthorized);

                    args.Cancel = !allowed;

                    if (!allowed)
                    {
                        authorizedExternalNavigationIds.Remove(
                            args.NavigationId);
                    }
                    else if (!args.IsRedirected
                             && args.IsUserInitiated)
                    {
                        authorizedExternalNavigationIds.Add(
                            args.NavigationId);
                    }
                };

            webView.CoreWebView2.NavigationCompleted +=
                (_, args) =>
                {
                    authorizedExternalNavigationIds.Remove(
                        args.NavigationId);
                };

            webView.CoreWebView2.FrameCreated +=
                (_, args) =>
                {
                    args.Frame.NavigationStarting +=
                        (_, frameArgs) =>
                        {
                            frameArgs.Cancel = true;
                        };
                };

            webView.CoreWebView2.NewWindowRequested +=
                (_, args) =>
                {
                    args.Handled = true;
                };

            webView.CoreWebView2.LaunchingExternalUriScheme +=
                (_, args) =>
                {
                    args.Cancel = true;
                };

            initialDocumentNavigationPending = true;
            webView.NavigateToString(html);
        }
        catch
        {
            webView.Dispose();
            throw;
        }
    }

    internal static string WrapHtmlDocument(
        string content,
        bool allowExternalResources)
    {
        string contentSecurityPolicy =
            MarkdownRenderingSecurityPolicy.CreateContentSecurityPolicy(
                allowExternalResources);

        return
            $$"""
            <!DOCTYPE html>
            <html>
            <head>
            <meta charset="utf-8" />

            <meta
                http-equiv="Content-Security-Policy"
                content="{{contentSecurityPolicy}}" />

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
