using DeskVault.UI.Rendering.MarkdownDocumentRendering;
using Markdig;

namespace DeskVault.UI.Tests;

public sealed class MarkdownRenderingSecurityPolicyTests
{
    [Theory]
    [InlineData("about:blank", false, true)]
    [InlineData("about:blank", true, true)]
    [InlineData("about:blank#section", false, true)]
    [InlineData("about:blank#section", true, true)]
    [InlineData("http://example.com", false, false)]
    [InlineData("https://example.com", false, false)]
    [InlineData("http://example.com", true, true)]
    [InlineData("https://example.com", true, true)]
    [InlineData("HTTPS://EXAMPLE.COM/path", true, true)]
    [InlineData("HTTP://EXAMPLE.COM/path", true, true)]
    [InlineData("about:blankevil", false, false)]
    [InlineData("about:blank?redirect=https://example.com", false, false)]
    [InlineData("about:%62lank", false, false)]
    [InlineData("file:///C:/secret.txt", true, false)]
    [InlineData("file://server/share/secret.txt", true, false)]
    [InlineData("\\\\server\\share\\secret.txt", true, false)]
    [InlineData("javascript:alert(1)", true, false)]
    [InlineData("data:text/html,<script>alert(1)</script>", true, false)]
    [InlineData("blob:https://example.com/id", true, false)]
    [InlineData("ftp://example.com/file", true, false)]
    [InlineData("mailto:security@example.com", true, false)]
    [InlineData("custom://launch/action", true, false)]
    [InlineData("relative/path", true, false)]
    [InlineData("", true, false)]
    [InlineData("   ", true, false)]
    [InlineData(" https://example.com", true, false)]
    [InlineData("https://user:password@example.com", true, false)]
    [InlineData("https://example.com/%zz", true, false)]
    public void IsNavigationAllowed_AppliesExplicitSchemePolicy(
        string? navigationUri,
        bool allowExternalNavigation,
        bool expected)
    {
        // Arrange

        // Act
        bool actual =
            MarkdownRenderingSecurityPolicy.IsNavigationAllowed(
                navigationUri,
                allowExternalNavigation);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("about:blank", false, false)]
    [InlineData("about:blank#section", false, false)]
    [InlineData("about:blank", true, false)]
    [InlineData("about:blank#section", true, false)]
    [InlineData("http://example.com/image.png", false, false)]
    [InlineData("https://example.com/image.png", false, false)]
    [InlineData("http://example.com/image.png", true, true)]
    [InlineData("https://example.com/image.png", true, true)]
    [InlineData("HTTPS://EXAMPLE.COM/image.png", true, true)]
    [InlineData("HTTP://EXAMPLE.COM/image.png", true, true)]
    [InlineData("file:///C:/secret.txt", true, false)]
    [InlineData("file://server/share/secret.txt", true, false)]
    [InlineData("\\\\server\\share\\secret.txt", true, false)]
    [InlineData("javascript:alert(1)", true, false)]
    [InlineData("data:image/png;base64,AAAA", true, false)]
    [InlineData("blob:https://example.com/id", true, false)]
    [InlineData("ftp://example.com/file", true, false)]
    [InlineData("mailto:security@example.com", true, false)]
    [InlineData("custom://resource/item", true, false)]
    [InlineData("images/logo.png", true, false)]
    [InlineData("", true, false)]
    [InlineData("   ", true, false)]
    [InlineData("https://example.com/%zz", true, false)]
    public void IsResourceAllowed_AppliesExplicitSchemePolicy(
        string? resourceUri,
        bool allowExternalResources,
        bool expected)
    {
        // Arrange

        // Act
        bool actual =
            MarkdownRenderingSecurityPolicy.IsResourceAllowed(
                resourceUri,
                allowExternalResources);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(
        "https://example.com",
        true, true, false, false, true)]
    [InlineData(
        "https://example.com",
        true, false, false, false, false)]
    [InlineData(
        "https://example.com",
        true, false, true, true, true)]
    [InlineData(
        "https://example.com",
        true, false, true, false, false)]
    [InlineData(
        "https://example.com",
        false, true, false, false, false)]
    [InlineData(
        "about:blank",
        false, false, false, false, true)]
    [InlineData(
        "about:blank#section",
        false, false, false, false, true)]
    [InlineData(
        "javascript:alert(1)",
        true, true, false, false, false)]
    [InlineData(
        "file:///C:/secret.txt",
        true, true, false, false, false)]
    public void IsNavigationRequestAllowed_EnforcesUserInitiationAndRedirectAuthorization(
        string navigationUri,
        bool allowExternalNavigation,
        bool isUserInitiated,
        bool isRedirected,
        bool previouslyAuthorized,
        bool expected)
    {
        // Arrange

        // Act
        bool actual =
            MarkdownRenderingSecurityPolicy.IsNavigationRequestAllowed(
                navigationUri,
                allowExternalNavigation,
                isUserInitiated,
                isRedirected,
                previouslyAuthorized);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(
        "data:text/html;charset=utf-8,%3Chtml%3E",
        true, false, false, true)]
    [InlineData(
        "DATA:TEXT/HTML;CHARSET=UTF-8,%3Chtml%3E",
        true, false, false, true)]
    [InlineData(
        "data:text/html,%3Chtml%3E",
        true, false, false, true)]
    [InlineData(
        "data:text/html;charset=utf-8,%3Chtml%3E",
        false, false, false, false)]
    [InlineData(
        "data:text/html;charset=utf-8,%3Chtml%3E",
        true, true, false, false)]
    [InlineData(
        "data:text/html;charset=utf-8,%3Chtml%3E",
        true, false, true, false)]
    [InlineData(
        "data:text/plain,content",
        true, false, false, false)]
    [InlineData(
        "data:image/svg+xml,%3Csvg%3E",
        true, false, false, false)]
    [InlineData(
        "data:text/htmlx,%3Chtml%3E",
        true, false, false, false)]
    [InlineData(
        "data:text/html",
        true, false, false, false)]
    [InlineData(
        "https://example.com",
        true, false, false, false)]
    [InlineData(
        "javascript:alert(1)",
        true, false, false, false)]
    public void IsInitialDocumentNavigationAllowed_OnlyAllowsPendingHostHtmlLoad(
        string? navigationUri,
        bool isInitialDocumentNavigationPending,
        bool isUserInitiated,
        bool isRedirected,
        bool expected)
    {
        // Arrange

        // Act
        bool actual =
            MarkdownRenderingSecurityPolicy.IsInitialDocumentNavigationAllowed(
                navigationUri,
                isInitialDocumentNavigationPending,
                isUserInitiated,
                isRedirected);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ExternalNavigationAndResources_UseIndependentSettings()
    {
        // Arrange
        const string externalUri = "https://example.com/resource";

        // Act
        bool navigationAllowed =
            MarkdownRenderingSecurityPolicy.IsNavigationAllowed(
                externalUri,
                allowExternalNavigation: true);

        bool resourceAllowed =
            MarkdownRenderingSecurityPolicy.IsResourceAllowed(
                externalUri,
                allowExternalResources: false);

        // Assert
        Assert.True(navigationAllowed);
        Assert.False(resourceAllowed);
    }

    [Fact]
    public void RawHtmlDisabled_MarkdownPipelineTreatsHtmlAsText()
    {
        // Arrange
        const string markdown =
            "<div>untrusted content</div>";

        MarkdownPipeline pipeline =
            MarkdownRenderingSecurityPolicy.CreatePipeline(
                allowRawHtml: false);

        // Act
        string html =
            Markdown.ToHtml(
                markdown,
                pipeline);

        // Assert
        Assert.DoesNotContain("<div>", html);
        Assert.Contains("&lt;div&gt;", html);
    }

    [Fact]
    public void RawHtmlEnabled_MarkdownPipelinePreservesConfiguredHtml()
    {
        // Arrange
        const string markdown =
            "<div>configured HTML content</div>";

        MarkdownPipeline pipeline =
            MarkdownRenderingSecurityPolicy.CreatePipeline(
                allowRawHtml: true);

        // Act
        string html =
            Markdown.ToHtml(
                markdown,
                pipeline);

        // Assert
        Assert.Contains("<div>", html);
        Assert.Contains("</div>", html);
    }

    [Fact]
    public void CreateContentSecurityPolicy_WhenExternalResourcesDisabled_BlocksRemoteContent()
    {
        // Arrange

        // Act
        string policy =
            MarkdownRenderingSecurityPolicy.CreateContentSecurityPolicy(
                allowExternalResources: false);

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
        Assert.Contains("img-src 'none'", policy);
        Assert.Contains("font-src 'none'", policy);
        Assert.Contains("media-src 'none'", policy);
        Assert.Contains("style-src 'unsafe-inline'", policy);
        Assert.DoesNotContain("http:", policy);
        Assert.DoesNotContain("https:", policy);
    }

    [Fact]
    public void CreateContentSecurityPolicy_WhenExternalResourcesEnabled_AllowsOnlyConfiguredResourceSchemes()
    {
        // Arrange

        // Act
        string policy =
            MarkdownRenderingSecurityPolicy.CreateContentSecurityPolicy(
                allowExternalResources: true);

        // Assert
        Assert.Contains("img-src http: https:", policy);
        Assert.Contains("font-src http: https:", policy);
        Assert.Contains("media-src http: https:", policy);
        Assert.Contains(
            "style-src 'unsafe-inline' http: https:",
            policy);

        Assert.Contains("script-src 'none'", policy);
        Assert.Contains("object-src 'none'", policy);
        Assert.Contains("frame-src 'none'", policy);
        Assert.Contains("connect-src 'none'", policy);
        Assert.Contains("form-action 'none'", policy);

        Assert.DoesNotContain("data:", policy);
        Assert.DoesNotContain("blob:", policy);
        Assert.DoesNotContain("file:", policy);
    }

    [Fact]
    public void RawHtmlEnabled_ContentSecurityPolicyStillRestrictsActiveContent()
    {
        // Arrange
        const string markdown =
            """
        <script>alert('untrusted')</script>
        <iframe src="https://example.com"></iframe>
        <img src="data:image/png;base64,AAAA" />
        """;

        MarkdownPipeline pipeline =
            MarkdownRenderingSecurityPolicy.CreatePipeline(
                allowRawHtml: true);

        // Act
        string html =
            Markdown.ToHtml(markdown, pipeline);

        string policy =
            MarkdownRenderingSecurityPolicy.CreateContentSecurityPolicy(
                allowExternalResources: false);

        // Assert
        Assert.Contains("<script>", html);
        Assert.Contains("<iframe", html);
        Assert.Contains("data:image/png", html);

        Assert.Contains("script-src 'none'", policy);
        Assert.Contains("frame-src 'none'", policy);
        Assert.Contains("img-src 'none'", policy);
    }

    [Fact]
    public void UriPolicies_ApplyConsistentRulesToDeterministicallyGeneratedVariants()
    {
        // Arrange
        string[] supportedExternalUris =
        [
            "http://example.com",
            "HTTP://EXAMPLE.COM/path",
            "hTtP://example.com:8080/resource?x=1#top",
            "https://example.com",
            "HTTPS://EXAMPLE.COM/path",
            "hTtPs://cdn.example.com:8443/assets/logo.png"
        ];

        string[] unsupportedSchemes =
        [
            "javascript",
            "data",
            "blob",
            "file",
            "ftp",
            "mailto",
            "vbscript",
            "custom",
            "ms-settings"
        ];

        string[] deceptiveUris =
        [
            "java%73cript:alert(1)",
            "javascript%3Aalert(1)",
            "data%3Atext/html,payload",
            "file%3A///C:/secret.txt",
            "h%74tps://example.com/resource",
            "http%3A//example.com/resource",
            "about:%62lank",
            "about:blankevil",
            "about:blank?next=https://example.com",
            "about:blank%23section",
            "//example.com/resource",
            "/local/resource",
            "relative/resource",
            "#section",
            "C:\\secret.txt",
            "\\\\server\\share\\secret.txt",
            "https://user@example.com/resource",
            "https://example.com/%zz",
            "https://example.com\\@evil.example/"
        ];

        // Act
        // Assert: supported external HTTP(S) URIs respect both independent settings.
        foreach (string uri in supportedExternalUris)
        {
            Assert.True(
                MarkdownRenderingSecurityPolicy.IsNavigationAllowed(
                    uri,
                    allowExternalNavigation: true),
                $"Expected supported navigation URI to be allowed: {uri}");

            Assert.False(
                MarkdownRenderingSecurityPolicy.IsNavigationAllowed(
                    uri,
                    allowExternalNavigation: false),
                $"Expected navigation URI to be blocked when disabled: {uri}");

            Assert.True(
                MarkdownRenderingSecurityPolicy.IsResourceAllowed(
                    uri,
                    allowExternalResources: true),
                $"Expected supported resource URI to be allowed: {uri}");

            Assert.False(
                MarkdownRenderingSecurityPolicy.IsResourceAllowed(
                    uri,
                    allowExternalResources: false),
                $"Expected resource URI to be blocked when disabled: {uri}");
        }

        // Unsupported schemes are generated in lower, upper and mixed case.
        foreach (string scheme in unsupportedSchemes)
        {
            char[] mixedCaseCharacters = scheme.ToCharArray();

            for (int index = 0; index < mixedCaseCharacters.Length; index++)
            {
                mixedCaseCharacters[index] =
                    index % 2 == 0
                        ? char.ToUpperInvariant(mixedCaseCharacters[index])
                        : char.ToLowerInvariant(mixedCaseCharacters[index]);
            }

            string[] schemeVariants =
            [
                scheme,
                scheme.ToUpperInvariant(),
                new string(mixedCaseCharacters)
            ];

            foreach (string schemeVariant in schemeVariants)
            {
                foreach (string suffix in new[]
                {
                    "payload",
                    "//example.com/payload"
                })
                {
                    string uri = $"{schemeVariant}:{suffix}";

                    Assert.False(
                        MarkdownRenderingSecurityPolicy.IsNavigationAllowed(
                            uri,
                            allowExternalNavigation: true),
                        $"Unsupported navigation scheme was allowed: {uri}");

                    Assert.False(
                        MarkdownRenderingSecurityPolicy.IsResourceAllowed(
                            uri,
                            allowExternalResources: true),
                        $"Unsupported resource scheme was allowed: {uri}");
                }
            }
        }

        // Encoded, relative, malformed, credential-bearing and local URI forms
        // must not become allowed merely because external access is enabled.
        foreach (string uri in deceptiveUris)
        {
            Assert.False(
                MarkdownRenderingSecurityPolicy.IsNavigationAllowed(
                    uri,
                    allowExternalNavigation: true),
                $"Deceptive navigation URI was allowed: {uri}");

            Assert.False(
                MarkdownRenderingSecurityPolicy.IsResourceAllowed(
                    uri,
                    allowExternalResources: true),
                $"Deceptive resource URI was allowed: {uri}");
        }

        // about:blank is permitted as an internal navigation target only.
        foreach (string uri in new[]
        {
            "about:blank",
            "ABOUT:BLANK",
            "about:blank#section",
            "ABOUT:BLANK#section"
        })
        {
            Assert.True(
                MarkdownRenderingSecurityPolicy.IsNavigationAllowed(
                    uri,
                    allowExternalNavigation: false),
                $"Internal document navigation was blocked: {uri}");

            Assert.False(
                MarkdownRenderingSecurityPolicy.IsResourceAllowed(
                    uri,
                    allowExternalResources: true),
                $"Internal document URI was incorrectly allowed as a resource: {uri}");
        }
    }

    [Fact]
    public void WrapHtmlDocument_EmbedsRestrictivePolicyBeforeUntrustedContent()
    {
        // Arrange
        const string content =
            "<script>alert('untrusted')</script>";

        // Act
        string html =
            MarkdownDocumentContentRenderer.WrapHtmlDocument(
                content,
                allowExternalResources: false);

        int policyIndex =
            html.IndexOf(
                "Content-Security-Policy",
                StringComparison.Ordinal);

        int contentIndex =
            html.IndexOf(
                content,
                StringComparison.Ordinal);

        // Assert
        Assert.True(policyIndex >= 0);
        Assert.True(contentIndex > policyIndex);
        Assert.Contains("default-src 'none'", html);
        Assert.Contains("script-src 'none'", html);
        Assert.Contains("img-src 'none'", html);
        Assert.Contains(content, html);
    }

    [Fact]
    public void WrapHtmlDocument_WhenExternalResourcesEnabled_PreservesOtherRestrictions()
    {
        // Arrange
        const string content =
            "External resource preview";

        // Act
        string html =
            MarkdownDocumentContentRenderer.WrapHtmlDocument(
                content,
                allowExternalResources: true);

        // Assert
        Assert.Contains("img-src http: https:", html);
        Assert.Contains("font-src http: https:", html);
        Assert.Contains("media-src http: https:", html);
        Assert.Contains(
            "style-src 'unsafe-inline' http: https:",
            html);

        Assert.Contains("script-src 'none'", html);
        Assert.Contains("object-src 'none'", html);
        Assert.Contains("frame-src 'none'", html);
        Assert.Contains("connect-src 'none'", html);
        Assert.Contains("form-action 'none'", html);
        Assert.Contains(content, html);
    }
}
