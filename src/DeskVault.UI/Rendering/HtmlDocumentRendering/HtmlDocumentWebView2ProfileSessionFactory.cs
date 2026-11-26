using Microsoft.Web.WebView2.WinForms;

namespace DeskVault.UI.Rendering.HtmlDocumentRendering;

/// <summary>
/// Creates profile sessions through the dedicated HTML preview lifecycle
/// implementation established by the HTML security work.
/// </summary>
internal sealed class HtmlDocumentWebView2ProfileSessionFactory
    : IHtmlDocumentWebView2ProfileSessionFactory
{
    /// <inheritdoc />
    public Task<HtmlDocumentWebView2ProfileSession> CreateAsync(
        WebView2 webView,
        CancellationToken cancellationToken)
    {
        return HtmlDocumentWebView2ProfileSession.CreateAsync(
            webView,
            cancellationToken: cancellationToken);
    }
}
