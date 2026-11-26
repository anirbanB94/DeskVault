using Microsoft.Web.WebView2.WinForms;

namespace DeskVault.UI.Rendering.HtmlDocumentRendering;

/// <summary>
/// Creates the isolated WebView2 profile session used by an HTML preview.
/// </summary>
internal interface IHtmlDocumentWebView2ProfileSessionFactory
{
    /// <summary>
    /// Initializes an uninitialized WebView2 control using the dedicated
    /// HTML preview profile configuration.
    /// </summary>
    /// <param name="webView">The WebView2 control transferred to the session.</param>
    /// <param name="cancellationToken">The initialization cancellation token.</param>
    /// <returns>The initialized profile session that owns the control.</returns>
    Task<HtmlDocumentWebView2ProfileSession> CreateAsync(
        WebView2 webView,
        CancellationToken cancellationToken);
}
