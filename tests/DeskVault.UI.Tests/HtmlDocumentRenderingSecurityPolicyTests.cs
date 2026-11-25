using DeskVault.UI.Rendering.HtmlDocumentRendering;

namespace DeskVault.UI.Tests;

/// <summary>
/// Verifies the isolated security decisions for untrusted HTML preview.
/// </summary>
public sealed class HtmlDocumentRenderingSecurityPolicyTests
{

    /// <summary>
    /// Verifies that only the pending, non-redirected renderer HTML data URI
    /// is authorized for the initial document navigation.
    /// </summary>
    [Theory]
    [InlineData(
        "data:text/html;charset=utf-8,%3Chtml%3E",
        true, false, true)]
    [InlineData(
        "DATA:TEXT/HTML;CHARSET=UTF-8,%3Chtml%3E",
        true, false, true)]
    [InlineData(
        "data:text/html,%3Chtml%3E",
        true, false, true)]
    [InlineData(
        "data:text/html;charset=utf-8,%3Chtml%3E",
        false, false, false)]
    [InlineData(
        "data:text/html;charset=utf-8,%3Chtml%3E",
        true, true, false)]
    [InlineData(
        "data:text/htmlx,%3Chtml%3E",
        true, false, false)]
    [InlineData(
        "data:text/plain,%3Chtml%3E",
        true, false, false)]
    [InlineData(
        "data:image/png;base64,iVBORw0KGgo=",
        true, false, false)]
    [InlineData(
        "data:text/html",
        true, false, false)]
    [InlineData(
        "data:text/html,",
        true, false, false)]
    [InlineData(
        "about:blank",
        true, false, false)]
    [InlineData(
        "https://example.com/document",
        true, false, false)]
    [InlineData(
        "javascript:alert(1)",
        true, false, false)]
    [InlineData(
        " data:text/html,%3Chtml%3E",
        true, false, false)]
    [InlineData(
        "data:text/html,%3Chtml%3E\\",
        true, false, false)]
    public void IsInitialDocumentNavigationAllowed_OnlyAllowsPendingHostNavigation(
        string? navigationUri,
        bool isInitialDocumentNavigationPending,
        bool isRedirected,
        bool expected)
    {
        // Arrange

        // Act
        bool actual =
            HtmlDocumentRenderingSecurityPolicy
                .IsInitialDocumentNavigationAllowed(
                    navigationUri,
                    isInitialDocumentNavigationPending,
                    isRedirected);

        // Assert
        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Verifies that the embedded-image allowance is restricted to
    /// supported image data URIs and image resource requests.
    /// </summary>
    [Theory]
    [InlineData(
        "data:image/png;base64,iVBORw0KGgo=",
        true, true)]
    [InlineData(
        "data:image/jpeg;base64,/9j/",
        true, true)]
    [InlineData(
        "DATA:IMAGE/PNG;BASE64,iVBORw0KGgo=",
        true, true)]
    [InlineData(
        "data:image/png;base64,iVBORw0KGgo=",
        false, false)]
    [InlineData(
        "data:image/jpeg;base64,/9j/",
        false, false)]
    [InlineData(
        "https://example.com/image.png",
        true, false)]
    [InlineData(
        "file:///C:/image.png",
        true, false)]
    [InlineData(
        "data:image/svg+xml;base64,PHN2Zz4=",
        true, false)]
    [InlineData(
        "data:image/gif;base64,R0lGODlh",
        true, false)]
    [InlineData(
        "data:image/png,not-base64",
        true, false)]
    [InlineData(
        "data:image/png;base64,AAAA",
        true, false)]
    [InlineData(
        "data:image/jpeg;base64,AAAA",
        true, false)]
    [InlineData(
        "data:image/png;base64,",
        true, false)]
    [InlineData(
        "data:image/jpg;base64,/9j/",
        true, false)]
    [InlineData(
        "data:image/png;base64,iVBORw0KGgo= ",
        true, false)]
    public void IsResourceAllowed_OnlyAllowsApprovedImageDataUrisInImageContext(
        string? resourceUri,
        bool isImageResource,
        bool expected)
    {
        // Arrange

        // Act
        bool actual =
            HtmlDocumentRenderingSecurityPolicy.IsResourceAllowed(
                resourceUri,
                isImageResource);

        // Assert
        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Verifies that the HTML preview Content Security Policy denies
    /// active content and all resource categories except embedded images
    /// and inline styling.
    /// </summary>
    [Fact]
    public void CreateContentSecurityPolicy_UsesDefaultDenyDirectives()
    {
        // Arrange

        // Act
        string policy =
            HtmlDocumentRenderingSecurityPolicy
                .CreateContentSecurityPolicy();

        // Assert
        Assert.Contains("default-src 'none'", policy);
        Assert.Contains("script-src 'none'", policy);
        Assert.Contains("object-src 'none'", policy);
        Assert.Contains("frame-src 'none'", policy);
        Assert.Contains("child-src 'none'", policy);
        Assert.Contains("worker-src 'none'", policy);
        Assert.Contains("connect-src 'none'", policy);
        Assert.Contains("base-uri 'none'", policy);
        Assert.Contains("form-action 'none'", policy);
        Assert.Contains("img-src data:", policy);
        Assert.Contains("font-src 'none'", policy);
        Assert.Contains("media-src 'none'", policy);
        Assert.Contains("manifest-src 'none'", policy);
        Assert.Contains("style-src 'unsafe-inline'", policy);

        Assert.DoesNotContain("http:", policy);
        Assert.DoesNotContain("https:", policy);
        Assert.DoesNotContain("file:", policy);
        Assert.DoesNotContain("blob:", policy);
        Assert.DoesNotContain("javascript:", policy);
    }
}
