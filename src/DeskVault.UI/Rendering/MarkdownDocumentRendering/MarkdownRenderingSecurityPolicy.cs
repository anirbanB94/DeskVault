using Markdig;

namespace DeskVault.UI.Rendering.MarkdownDocumentRendering;

/// <summary>
/// Defines the security policy applied to untrusted Markdown presentation.
/// </summary>
internal static class MarkdownRenderingSecurityPolicy
{
    private const string InternalDocumentUri = "about:blank";

    /// <summary>
    /// Creates the Markdig pipeline using the configured raw HTML policy.
    /// </summary>
    internal static MarkdownPipeline CreatePipeline(
        bool allowRawHtml)
    {
        var pipelineBuilder =
            new MarkdownPipelineBuilder()
                .UseAdvancedExtensions();

        if (!allowRawHtml)
        {
            pipelineBuilder.DisableHtml();
        }

        return pipelineBuilder.Build();
    }

    /// <summary>
    /// Determines whether a navigation URI is permitted.
    /// Only the internal document URI and explicitly configured
    /// HTTP/HTTPS navigation are permitted.
    /// </summary>
    internal static bool IsNavigationAllowed(
        string? navigationUri,
        bool allowExternalNavigation)
    {
        if (!TryParseAbsoluteUri(
                navigationUri,
                out Uri? uri))
        {
            return false;
        }

        if (IsInternalDocumentUri(navigationUri!, uri!))
        {
            return true;
        }

        return allowExternalNavigation
            && IsSupportedExternalUri(uri!);
    }

    /// <summary>
    /// Determines whether a navigation request is permitted, including
    /// the user-gesture requirement for external navigation and the
    /// authorization of redirects belonging to an approved navigation.
    /// </summary>
    internal static bool IsNavigationRequestAllowed(
        string? navigationUri,
        bool allowExternalNavigation,
        bool isUserInitiated,
        bool isRedirected,
        bool previouslyAuthorizedNavigation)
    {
        if (!IsNavigationAllowed(
                navigationUri,
                allowExternalNavigation))
        {
            return false;
        }

        if (navigationUri is not null
            && TryParseAbsoluteUri(
                navigationUri,
                out Uri? uri)
            && IsInternalDocumentUri(navigationUri, uri!))
        {
            return true;
        }

        if (isRedirected)
        {
            return previouslyAuthorizedNavigation;
        }

        return isUserInitiated;
    }

    /// <summary>
    /// Determines whether the one initial HTML document navigation
    /// initiated by the renderer's NavigateToString call is permitted.
    /// This exception is valid only while the host-side one-time
    /// navigation token is pending. It does not permit general data URIs.
    /// </summary>
    internal static bool IsInitialDocumentNavigationAllowed(
        string? navigationUri,
        bool isInitialDocumentNavigationPending,
        bool isUserInitiated,
        bool isRedirected)
    {
        if (!isInitialDocumentNavigationPending
            || isUserInitiated
            || isRedirected
            || string.IsNullOrWhiteSpace(navigationUri))
        {
            return false;
        }

        if (!string.Equals(
                navigationUri,
                navigationUri.Trim(),
                StringComparison.Ordinal)
            || navigationUri.Any(char.IsControl)
            || navigationUri.Contains('\\'))
        {
            return false;
        }

        const string HtmlDataUriPrefix = "data:text/html";

        if (!navigationUri.StartsWith(
                HtmlDataUriPrefix,
                StringComparison.OrdinalIgnoreCase)
            || navigationUri.Length <= HtmlDataUriPrefix.Length)
        {
            return false;
        }

        char mediaTypeSeparator =
            navigationUri[HtmlDataUriPrefix.Length];

        if (mediaTypeSeparator is not (';' or ','))
        {
            return false;
        }

        // A data URI must contain the comma separating metadata
        // from its payload. Only the renderer's pending initial
        // HTML navigation can reach this exception.
        return navigationUri.IndexOf(
            ',',
            HtmlDataUriPrefix.Length) >= HtmlDataUriPrefix.Length;
    }

    /// <summary>
    /// Determines whether a resource URI is permitted.
    /// Only explicitly configured HTTP/HTTPS resources are permitted.
    /// The internal about:blank document URI is navigation-only.
    /// </summary>
    internal static bool IsResourceAllowed(
        string? resourceUri,
        bool allowExternalResources)
    {
        if (!TryParseAbsoluteUri(
                resourceUri,
                out Uri? uri))
        {
            return false;
        }

        // about:blank is an internal navigation target,
        // not an approved resource origin.
        return allowExternalResources
            && IsSupportedExternalUri(uri!);
    }

    /// <summary>
    /// Creates a restrictive Content Security Policy for the
    /// generated Markdown presentation document.
    /// </summary>
    internal static string CreateContentSecurityPolicy(
        bool allowExternalResources)
    {
        string externalSources =
            allowExternalResources
                ? "http: https:"
                : "'none'";

        string externalStyleSources =
            allowExternalResources
                ? "'unsafe-inline' http: https:"
                : "'unsafe-inline'";

        return string.Join(
            "; ",
            [
                "default-src 'none'",
            "script-src 'none'",
            "object-src 'none'",
            "frame-src 'none'",
            "child-src 'none'",
            "worker-src 'none'",
            "connect-src 'none'",
            "base-uri 'none'",
            "form-action 'none'",
            $"img-src {externalSources}",
            $"font-src {externalSources}",
            $"media-src {externalSources}",
            $"style-src {externalStyleSources}"
            ]);
    }

    private static bool TryParseAbsoluteUri(
        string? value,
        out Uri? uri)
    {
        uri = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (!string.Equals(
                value,
                value.Trim(),
                StringComparison.Ordinal))
        {
            return false;
        }

        if (value.Any(char.IsControl)
            || value.Contains('\\'))
        {
            return false;
        }

        if (!Uri.TryCreate(
                value,
                UriKind.Absolute,
                out Uri? parsedUri)
            || parsedUri is null
            || !parsedUri.IsWellFormedOriginalString())
        {
            return false;
        }

        uri = parsedUri;
        return true;
    }

    private static bool IsInternalDocumentUri(
        string originalValue,
        Uri uri)
    {
        if (!string.Equals(
                uri.Scheme,
                "about",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        int fragmentIndex =
            originalValue.IndexOf('#');

        string documentUri =
            fragmentIndex >= 0
                ? originalValue[..fragmentIndex]
                : originalValue;

        return string.Equals(
            documentUri,
            InternalDocumentUri,
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSupportedExternalUri(
        Uri uri)
    {
        bool supportedScheme =
            string.Equals(
                uri.Scheme,
                Uri.UriSchemeHttp,
                StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                uri.Scheme,
                Uri.UriSchemeHttps,
                StringComparison.OrdinalIgnoreCase);

        if (!supportedScheme)
        {
            return false;
        }

        // Avoid accepting credentials embedded in an external URI.
        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(uri.Host);
    }
}
