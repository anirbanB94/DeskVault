namespace DeskVault.UI.Rendering.HtmlDocumentRendering;

/// <summary>
/// Defines the security policy for presenting untrusted imported HTML
/// inside the restricted DeskVault document-preview surface.
/// </summary>
/// <remarks>
/// This policy is intentionally independent of the Markdown renderer.
/// It does not authorize remote resources, local files, active content,
/// or general-purpose browser navigation.
/// </remarks>
internal static class HtmlDocumentRenderingSecurityPolicy
{
    private const string PngDataUriHeader =
        "data:image/png;base64";

    private const string JpegDataUriHeader =
        "data:image/jpeg;base64";

    private static readonly byte[] PngSignature =
    [
        0x89, 0x50, 0x4E, 0x47,
        0x0D, 0x0A, 0x1A, 0x0A
    ];

    private static readonly byte[] JpegSignature =
    [
        0xFF, 0xD8, 0xFF
    ];

    /// <summary>
    /// Determines whether a navigation event represents the single
    /// renderer-authorized initial HTML document load.
    /// </summary>
    /// <param name="navigationUri">
    /// The URI reported by the WebView2 navigation event.
    /// </param>
    /// <param name="isInitialDocumentNavigationPending">
    /// Indicates whether the renderer's one-time initial-load authorization
    /// is still pending.
    /// </param>
    /// <param name="isRedirected">
    /// Indicates whether the navigation is a redirect.
    /// </param>
    /// <returns>
    /// <see langword="true"/> only for the renderer's initial HTML data URI
    /// while authorization is pending and the navigation is not redirected.
    /// </returns>
    /// <remarks>
    /// NavigateToString creates a document with an about:blank location.
    /// The corresponding initial navigation is handled through the
    /// renderer's one-time data:text/html navigation authorization.
    /// Authorization does not depend on IsUserInitiated because WebView2
    /// can report API-initiated navigations as user initiated.
    /// </remarks>
    internal static bool IsInitialDocumentNavigationAllowed(
        string? navigationUri,
        bool isInitialDocumentNavigationPending,
        bool isRedirected)
    {
        if (!isInitialDocumentNavigationPending
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

        char metadataSeparator =
            navigationUri[HtmlDataUriPrefix.Length];

        if (metadataSeparator is not (';' or ','))
        {
            return false;
        }

        // Require the data-URI payload separator. The host-issued one-time
        // authorization ensures that this exception cannot be reused.
        int payloadSeparatorIndex =
            navigationUri.IndexOf(
                ',',
                HtmlDataUriPrefix.Length);

        return payloadSeparatorIndex >= HtmlDataUriPrefix.Length
               && payloadSeparatorIndex < navigationUri.Length - 1;
    }

    /// <summary>
    /// Determines whether a WebView2 resource request is eligible for
    /// the explicitly supported embedded-image allowance.
    /// </summary>
    /// <param name="resourceUri">
    /// The URI of the requested resource.
    /// </param>
    /// <param name="isImageResource">
    /// Indicates whether WebView2 classified the request as an image resource.
    /// </param>
    /// <returns>
    /// <see langword="true"/> only for a supported Base64 PNG or JPEG
    /// data URI requested as an image resource.
    /// </returns>
    /// <remarks>
    /// The HTML preprocessing boundary must separately reject CSS
    /// <c>url()</c> and <c>@import</c> references. WebView2's image resource
    /// classification alone cannot distinguish an image element from a
    /// CSS-generated image request.
    /// </remarks>
    internal static bool IsResourceAllowed(
        string? resourceUri,
        bool isImageResource)
    {
        return isImageResource
               && IsApprovedEmbeddedImageDataUri(resourceUri);
    }

    /// <summary>
    /// Determines whether a data URI contains a supported Base64-encoded
    /// PNG or JPEG image with a matching binary file signature.
    /// </summary>
    /// <param name="resourceUri">
    /// The candidate embedded-image data URI.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the URI has an explicitly supported
    /// MIME type, valid Base64 payload and matching image signature.
    /// </returns>
    internal static bool IsApprovedEmbeddedImageDataUri(
        string? resourceUri)
    {
        if (string.IsNullOrEmpty(resourceUri)
            || !string.Equals(
                resourceUri,
                resourceUri.Trim(),
                StringComparison.Ordinal)
            || resourceUri.Any(char.IsControl)
            || resourceUri.Contains('\\'))
        {
            return false;
        }

        int separatorIndex =
            resourceUri.IndexOf(',');

        if (separatorIndex < 0
            || separatorIndex == resourceUri.Length - 1)
        {
            return false;
        }

        string metadata =
            resourceUri[..separatorIndex];

        bool isPng =
            string.Equals(
                metadata,
                PngDataUriHeader,
                StringComparison.OrdinalIgnoreCase);

        bool isJpeg =
            string.Equals(
                metadata,
                JpegDataUriHeader,
                StringComparison.OrdinalIgnoreCase);

        if (!isPng && !isJpeg)
        {
            return false;
        }

        string base64Payload =
            resourceUri[(separatorIndex + 1)..];

        // Reject whitespace before decoding because the Base64 decoder
        // accepts whitespace that is not part of the approved data-URI format.
        if (base64Payload.Length == 0
            || base64Payload.Any(char.IsWhiteSpace)
            || base64Payload.Length % 4 != 0)
        {
            return false;
        }

        int paddingLength =
            base64Payload.EndsWith(
                "==",
                StringComparison.Ordinal)
                ? 2
                : base64Payload.EndsWith('=')
                    ? 1
                    : 0;

        int decodedLength =
            (base64Payload.Length / 4 * 3) - paddingLength;

        if (decodedLength <= 0)
        {
            return false;
        }

        byte[] decodedBytes =
            new byte[decodedLength];

        if (!Convert.TryFromBase64String(
                base64Payload,
                decodedBytes,
                out int bytesWritten)
            || bytesWritten != decodedLength)
        {
            return false;
        }

        // Validate the binary signature independently of the declared MIME type.
        if (isPng)
        {
            return decodedBytes.AsSpan().StartsWith(
                PngSignature);
        }

        return decodedBytes.AsSpan().StartsWith(
            JpegSignature);
    }

    /// <summary>
    /// Creates the restrictive Content Security Policy for an HTML
    /// document preview.
    /// </summary>
    /// <returns>
    /// A default-deny policy that permits inline styling and data-URI
    /// image sources while denying other resource categories.
    /// </returns>
    /// <remarks>
    /// The data image source is required for approved embedded PNG/JPEG
    /// images. It is not a substitute for the MIME/signature checks or
    /// the separate rejection of CSS resource references.
    /// </remarks>
    internal static string CreateContentSecurityPolicy()
    {
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
                "img-src data:",
                "font-src 'none'",
                "media-src 'none'",
                "manifest-src 'none'",
                "style-src 'unsafe-inline'"
            ]);
    }
}
