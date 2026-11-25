using DeskVault.UI.Rendering.HtmlDocumentRendering;

namespace DeskVault.UI.Tests;

/// <summary>
/// Verifies that untrusted HTML markup complies with the embedded-resource
/// restrictions required by the in-app document preview.
/// </summary>
public sealed class HtmlDocumentEmbeddedResourcePolicyTests
{
    /// <summary>
    /// Verifies that ordinary inline styling and approved embedded PNG/JPEG
    /// image data URIs remain supported.
    /// </summary>
    [Fact]
    public void IsMarkupAllowed_WithInlineStylesAndApprovedImages_ReturnsTrue()
    {
        // Arrange
        const string markup =
            """
            <!DOCTYPE html>
            <html>
            <head>
                <style>
                    body {
                        color: #222222;
                        background-color: #ffffff;
                    }

                    .content {
                        display: flex;
                        gap: 12px;
                    }

                    @media (min-width: 600px) {
                        .content {
                            padding: 16px;
                        }
                    }
                </style>
            </head>
            <body>
                <div class="content" style="font-weight: 600; border-radius: 4px;">
                    <p>Static HTML content</p>
                    <img
                        alt="PNG image"
                        src="data:image/png;base64,iVBORw0KGgo=" />
                    <img
                        alt="JPEG image"
                        src="data:image/jpeg;base64,/9j/" />
                    <a href="#details">Local section</a>
                </div>
                <section id="details">Details</section>
            </body>
            </html>
            """;

        // Act
        bool actual =
            HtmlDocumentEmbeddedResourcePolicy.IsMarkupAllowed(markup);

        // Assert
        Assert.True(actual);
    }

    /// <summary>
    /// Verifies that URL functions are rejected in CSS declarations,
    /// including escaped function identifiers.
    /// </summary>
    [Theory]
    [InlineData(
        "<style>body { background-image: url(https://example.invalid/a.png); }</style>")]
    [InlineData(
        "<style>body { background-image: URL(https://example.invalid/a.png); }</style>")]
    [InlineData(
        "<style>body { background-image: u\\72l(https://example.invalid/a.png); }</style>")]
    [InlineData(
        "<style>body { background: image-set(url(https://example.invalid/a.png) 1x); }</style>")]
    [InlineData(
        "<p style=\"background-image: url(https://example.invalid/a.png)\">Content</p>")]
    [InlineData(
        "<p style=\"background-image: url(data:image/png;base64,iVBORw0KGgo=)\">Content</p>")]
    public void IsMarkupAllowed_WithCssUrlFunction_ReturnsFalse(
        string markup)
    {
        // Arrange

        // Act
        bool actual =
            HtmlDocumentEmbeddedResourcePolicy.IsMarkupAllowed(markup);

        // Assert
        Assert.False(actual);
    }

    /// <summary>
    /// Verifies that stylesheet imports are rejected, including escaped
    /// at-rule identifiers.
    /// </summary>
    [Theory]
    [InlineData(
        "<style>@import url(https://example.invalid/theme.css);</style>")]
    [InlineData(
        "<style>@import \"https://example.invalid/theme.css\";</style>")]
    [InlineData(
        "<style>@\\69mport url(https://example.invalid/theme.css);</style>")]
    public void IsMarkupAllowed_WithCssImportRule_ReturnsFalse(
        string markup)
    {
        // Arrange

        // Act
        bool actual =
            HtmlDocumentEmbeddedResourcePolicy.IsMarkupAllowed(markup);

        // Assert
        Assert.False(actual);
    }

    /// <summary>
    /// Verifies that ordinary CSS string content containing the literal
    /// characters "url(" is not mistaken for a resource function.
    /// </summary>
    [Fact]
    public void IsMarkupAllowed_WithUrlTextInsideCssString_ReturnsTrue()
    {
        // Arrange
        const string markup =
            """
            <!DOCTYPE html>
            <html>
            <head>
                <style>
                    p::after {
                        content: "The literal text url(https://example.invalid/image.png)";
                    }
                </style>
            </head>
            <body>
                <p>Static document content</p>
            </body>
            </html>
            """;

        // Act
        bool actual =
            HtmlDocumentEmbeddedResourcePolicy.IsMarkupAllowed(markup);

        // Assert
        Assert.True(actual);
    }

    /// <summary>
    /// Verifies that unsupported HTML resource-bearing elements and
    /// attributes are rejected.
    /// </summary>
    [Theory]
    [InlineData(
        "<img src=\"https://example.invalid/image.png\" />")]
    [InlineData(
        "<img src=\"file:///C:/secret.png\" />")]
    [InlineData(
        "<img src=\"data:image/svg+xml;base64,PHN2Zz4=\" />")]
    [InlineData(
        "<img src=\"data:image/gif;base64,R0lGODlh\" />")]
    [InlineData(
        "<img src=\"data:image/png;base64,iVBORw0KGgo=\" srcset=\"https://example.invalid/image.png 2x\" />")]
    [InlineData(
        "<link rel=\"stylesheet\" href=\"styles.css\" />")]
    [InlineData(
        "<iframe src=\"https://example.invalid/frame\"></iframe>")]
    [InlineData(
        "<object data=\"document.pdf\"></object>")]
    [InlineData(
        "<video poster=\"poster.jpg\"></video>")]
    [InlineData(
        "<svg><image href=\"https://example.invalid/image.png\" /></svg>")]
    [InlineData(
        "<form action=\"https://example.invalid/submit\"><button>Submit</button></form>")]
    [InlineData(
        "<script>document.body.innerHTML = 'changed';</script>")]
    [InlineData(
        "<meta http-equiv=\"refresh\" content=\"0;url=https://example.invalid/\" />")]
    public void IsMarkupAllowed_WithUnsupportedHtmlResource_ReturnsFalse(
        string markup)
    {
        // Arrange

        // Act
        bool actual =
            HtmlDocumentEmbeddedResourcePolicy.IsMarkupAllowed(markup);

        // Assert
        Assert.False(actual);
    }

    /// <summary>
    /// Verifies that links to external documents are rejected while
    /// links confined to the current document remain supported.
    /// </summary>
    [Theory]
    [InlineData(
        "<a href=\"https://example.invalid/document\">External</a>",
        false)]
    [InlineData(
        "<a href=\"file:///C:/secret.txt\">Local file</a>",
        false)]
    [InlineData(
        "<a href=\"relative/document.html\">Relative document</a>",
        false)]
    [InlineData(
        "<a href=\"#section\">Local section</a>",
        true)]
    [InlineData(
        "<area href=\"#section\" />",
        true)]
    public void IsMarkupAllowed_AppliesLocalFragmentNavigationPolicy(
        string markup,
        bool expected)
    {
        // Arrange

        // Act
        bool actual =
            HtmlDocumentEmbeddedResourcePolicy.IsMarkupAllowed(markup);

        // Assert
        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Verifies that inline event-handler attributes are rejected
    /// independently of the WebView2 JavaScript setting.
    /// </summary>
    [Theory]
    [InlineData("<img onerror=\"alert(1)\" />")]
    [InlineData("<body onload=\"alert(1)\"></body>")]
    [InlineData("<div onclick=\"alert(1)\">Content</div>")]
    public void IsMarkupAllowed_WithInlineEventHandler_ReturnsFalse(
        string markup)
    {
        // Arrange

        // Act
        bool actual =
            HtmlDocumentEmbeddedResourcePolicy.IsMarkupAllowed(markup);

        // Assert
        Assert.False(actual);
    }
}
