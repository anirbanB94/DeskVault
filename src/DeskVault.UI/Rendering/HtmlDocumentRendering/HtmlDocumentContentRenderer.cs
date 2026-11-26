using Microsoft.Extensions.Logging;
using DeskVault.Application.Documents;

namespace DeskVault.UI.Rendering.HtmlDocumentRendering;

/// <summary>
/// Renders supported HTML and HTM documents inside an isolated WebView2
/// preview surface.
/// </summary>
/// <remarks>
/// The renderer consumes the caller-owned source stream through the bounded
/// HTML payload builder. The preview surface owns the WebView2 profile,
/// security policy, and browser-resource lifecycle.
/// </remarks>
internal sealed class HtmlDocumentContentRenderer : IDocumentContentRenderer
{
    private readonly IHtmlDocumentWebView2ProfileSessionFactory
        _profileSessionFactory;

    private readonly ILogger<HtmlDocumentPreviewSurface> _surfaceLogger;

    public int Priority => 0;

    public HtmlDocumentContentRenderer(
        IHtmlDocumentWebView2ProfileSessionFactory profileSessionFactory,
        ILogger<HtmlDocumentPreviewSurface> surfaceLogger)
    {
        ArgumentNullException.ThrowIfNull(profileSessionFactory);
        ArgumentNullException.ThrowIfNull(surfaceLogger);

        _profileSessionFactory = profileSessionFactory;
        _surfaceLogger = surfaceLogger;
    }

    public bool CanRender(string fileName)
    {
        return SupportedFileTypes.GetInAppPreviewKind(fileName)
            == DocumentInAppPreviewKind.Html;
    }

    public async Task RenderAsync(
        Control contentHost,
        Stream documentStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contentHost);
        ArgumentNullException.ThrowIfNull(documentStream);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        if (!CanRender(fileName))
        {
            throw new ArgumentException(
                "The supplied file is not supported by the HTML document renderer.",
                nameof(fileName));
        }

        cancellationToken.ThrowIfCancellationRequested();

        // Build and validate the complete payload before modifying the
        // existing presentation. The builder leaves the source stream open.
        string htmlPayload =
            await HtmlDocumentContentPayloadBuilder.BuildAsync(
                documentStream,
                cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        var previewSurface = new HtmlDocumentPreviewSurface(
            _surfaceLogger)
        {
            Dock = DockStyle.Fill
        };

        try
        {
            await DocumentContentHost.ClearAsync(contentHost);
            contentHost.Controls.Add(previewSurface);

            // Initialization creates the dedicated InPrivate profile and
            // applies the #404 WebView2 security policy before navigation.
            await previewSurface.InitializeAsync(
                _profileSessionFactory,
                cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            await previewSurface.NavigateAsync(
                htmlPayload,
                cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (Exception renderingException)
        {
            // The surface coordinates initialization, navigation and
            // WebView2 teardown. Await cleanup so failure does not leave
            // a partially initialized preview session behind.
            try
            {
                await previewSurface.DisposeAsync();
            }
            catch (Exception cleanupException)
            {
                throw new AggregateException(
                    "HTML document rendering failed and preview-resource cleanup also failed.",
                    renderingException,
                    cleanupException);
            }

            throw;
        }
    }
}
