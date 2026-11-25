using Microsoft.Web.WebView2.Core;

namespace DeskVault.UI.Rendering.HtmlDocumentRendering;

/// <summary>
/// Applies and owns the WebView2 runtime restrictions required for
/// presenting untrusted HTML in the DeskVault document preview.
/// </summary>
/// <remarks>
/// The instance is bound to the WebView2 UI thread. It must be created
/// after CoreWebView2 initialization and disposed before its WebView2
/// control is disposed.
/// </remarks>
internal sealed class HtmlDocumentWebView2SecurityPolicy : IDisposable
{
    private const string AllResourceUris = "*";

    private readonly CoreWebView2 _coreWebView2;

    private readonly List<FrameNavigationHandler> _frameNavigationHandlers = [];

    private bool _disposed;

    private bool _initialDocumentNavigationAuthorizationIssued;

    private bool _initialDocumentNavigationPending;

    /// <summary>
    /// Initializes the HTML preview security policy and applies all
    /// WebView2 settings and event handlers before document navigation.
    /// </summary>
    /// <param name="coreWebView2">
    /// The initialized WebView2 core instance dedicated to this preview.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="coreWebView2"/> is null.
    /// </exception>
    public HtmlDocumentWebView2SecurityPolicy(
        CoreWebView2 coreWebView2)
    {
        ArgumentNullException.ThrowIfNull(coreWebView2);

        _coreWebView2 = coreWebView2;

        ApplyBrowserSettings();

        // Register the resource filter before the host loads the initial
        // document. All request source kinds are included so document,
        // iframe and other request origins are not excluded by the filter.
        _coreWebView2.AddWebResourceRequestedFilter(
            AllResourceUris,
            CoreWebView2WebResourceContext.All,
            CoreWebView2WebResourceRequestSourceKinds.All);

        SubscribeToSecurityEvents();
    }

    /// <summary>
    /// Authorizes the host's one-time initial NavigateToString navigation.
    /// </summary>
    /// <remarks>
    /// Call this method immediately before the host invokes NavigateToString.
    /// The corresponding authorization is consumed by the first top-level
    /// NavigationStarting event, whether that event is accepted or rejected.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">
    /// Thrown when this policy has already been disposed.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when initial-navigation authorization has already been issued.
    /// </exception>
    public void AuthorizeInitialDocumentNavigation()
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);

        if (_initialDocumentNavigationAuthorizationIssued)
        {
            throw new InvalidOperationException(
                "Initial HTML document navigation authorization has already been issued.");
        }

        _initialDocumentNavigationAuthorizationIssued = true;
        _initialDocumentNavigationPending = true;
    }

    /// <summary>
    /// Releases this policy's WebView2 event subscriptions and resource filter.
    /// </summary>
    /// <remarks>
    /// Dispose this policy on the owning WebView2 UI thread and before
    /// disposing the associated WebView2 control.
    /// </remarks>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _initialDocumentNavigationAuthorizationIssued = false;
        _initialDocumentNavigationPending = false;

        UnsubscribeFromSecurityEvents();

        foreach (FrameNavigationHandler registration
                 in _frameNavigationHandlers)
        {
            registration.Frame.NavigationStarting -=
                registration.Handler;
        }

        _frameNavigationHandlers.Clear();

        _coreWebView2.RemoveWebResourceRequestedFilter(
            AllResourceUris,
            CoreWebView2WebResourceContext.All,
            CoreWebView2WebResourceRequestSourceKinds.All);
    }

    /// <summary>
    /// Disables browser capabilities that are not required to display
    /// a static imported HTML document.
    /// </summary>
    private void ApplyBrowserSettings()
    {
        CoreWebView2Settings settings =
            _coreWebView2.Settings;

        settings.IsScriptEnabled = false;
        settings.IsWebMessageEnabled = false;
        settings.AreHostObjectsAllowed = false;

        settings.AreDefaultScriptDialogsEnabled = false;
        settings.AreDefaultContextMenusEnabled = false;
        settings.AreDevToolsEnabled = false;
        settings.AreBrowserAcceleratorKeysEnabled = false;

        settings.IsZoomControlEnabled = false;
        settings.IsStatusBarEnabled = false;
        settings.IsBuiltInErrorPageEnabled = false;

        settings.IsGeneralAutofillEnabled = false;
        settings.IsPasswordAutosaveEnabled = false;
    }

    /// <summary>
    /// Subscribes the security handlers before the host loads
    /// the initial HTML document.
    /// </summary>
    private void SubscribeToSecurityEvents()
    {
        _coreWebView2.NavigationStarting +=
            OnNavigationStarting;

        _coreWebView2.WebResourceRequested +=
            OnWebResourceRequested;

        _coreWebView2.FrameCreated +=
            OnFrameCreated;

        _coreWebView2.NewWindowRequested +=
            OnNewWindowRequested;

        _coreWebView2.DownloadStarting +=
            OnDownloadStarting;

        _coreWebView2.PermissionRequested +=
            OnPermissionRequested;

        _coreWebView2.LaunchingExternalUriScheme +=
            OnLaunchingExternalUriScheme;

        _coreWebView2.BasicAuthenticationRequested +=
            OnBasicAuthenticationRequested;

        _coreWebView2.ClientCertificateRequested +=
            OnClientCertificateRequested;
    }

    /// <summary>
    /// Removes the security handlers from the WebView2 core instance.
    /// </summary>
    private void UnsubscribeFromSecurityEvents()
    {
        _coreWebView2.NavigationStarting -=
            OnNavigationStarting;

        _coreWebView2.WebResourceRequested -=
            OnWebResourceRequested;

        _coreWebView2.FrameCreated -=
            OnFrameCreated;

        _coreWebView2.NewWindowRequested -=
            OnNewWindowRequested;

        _coreWebView2.DownloadStarting -=
            OnDownloadStarting;

        _coreWebView2.PermissionRequested -=
            OnPermissionRequested;

        _coreWebView2.LaunchingExternalUriScheme -=
            OnLaunchingExternalUriScheme;

        _coreWebView2.BasicAuthenticationRequested -=
            OnBasicAuthenticationRequested;

        _coreWebView2.ClientCertificateRequested -=
            OnClientCertificateRequested;
    }

    /// <summary>
    /// Allows only the host-authorized initial HTML document navigation.
    /// All subsequent top-level navigation requests are rejected.
    /// </summary>
    /// <remarks>
    /// The initial authorization is consumed on the first navigation event,
    /// even when its URI or redirect state is invalid. This prevents an
    /// unexpected navigation from leaving the authorization available
    /// for reuse by untrusted document content.
    /// </remarks>
    private void OnNavigationStarting(
        object? sender,
        CoreWebView2NavigationStartingEventArgs args)
    {
        bool initialNavigationPending =
            _initialDocumentNavigationPending;

        // Consume authorization before evaluating the navigation.
        _initialDocumentNavigationPending = false;

        bool allowed =
            HtmlDocumentRenderingSecurityPolicy
                .IsInitialDocumentNavigationAllowed(
                    args.Uri,
                    initialNavigationPending,
                    args.IsRedirected);

        args.Cancel = !allowed;
    }

    /// <summary>
    /// Rejects all resource requests except approved embedded PNG/JPEG
    /// image data URIs.
    /// </summary>
    /// <remarks>
    /// CSS-originated resource references must also be rejected by the
    /// HTML preprocessing boundary. WebView2 image resource classification
    /// alone cannot distinguish an HTML image element from a CSS-generated
    /// image request.
    /// </remarks>
    private void OnWebResourceRequested(
        object? sender,
        CoreWebView2WebResourceRequestedEventArgs args)
    {
        bool isImageResource =
            args.ResourceContext == CoreWebView2WebResourceContext.Image;

        if (HtmlDocumentRenderingSecurityPolicy.IsResourceAllowed(
                args.Request.Uri,
                isImageResource))
        {
            return;
        }

        // Return an explicit forbidden response to prevent the blocked
        // resource request from falling through to normal network handling.
        args.Response =
            _coreWebView2.Environment.CreateWebResourceResponse(
                null,
                403,
                "Blocked",
                "Content-Type: text/plain; charset=utf-8");
    }

    /// <summary>
    /// Installs a frame-level navigation cancellation handler for every
    /// frame created by the HTML document.
    /// </summary>
    private void OnFrameCreated(
        object? sender,
        CoreWebView2FrameCreatedEventArgs args)
    {
        EventHandler<CoreWebView2NavigationStartingEventArgs> handler =
            OnFrameNavigationStarting;

        args.Frame.NavigationStarting += handler;

        // Retain the exact frame-handler association so it can be
        // unsubscribed when this security policy is disposed.
        _frameNavigationHandlers.Add(
            new FrameNavigationHandler(
                args.Frame,
                handler));
    }

    /// <summary>
    /// Rejects every navigation request originating inside a frame.
    /// </summary>
    private static void OnFrameNavigationStarting(
        object? sender,
        CoreWebView2NavigationStartingEventArgs args)
    {
        args.Cancel = true;
    }

    /// <summary>
    /// Prevents the document from opening a new browser window.
    /// </summary>
    private static void OnNewWindowRequested(
        object? sender,
        CoreWebView2NewWindowRequestedEventArgs args)
    {
        args.Handled = true;
    }

    /// <summary>
    /// Cancels every download initiated by the untrusted document.
    /// </summary>
    private static void OnDownloadStarting(
        object? sender,
        CoreWebView2DownloadStartingEventArgs args)
    {
        args.Cancel = true;
        args.Handled = true;
    }

    /// <summary>
    /// Denies every browser permission request made by untrusted content
    /// without persisting the permission decision in the browser profile.
    /// </summary>
    private static void OnPermissionRequested(
        object? sender,
        CoreWebView2PermissionRequestedEventArgs args)
    {
        args.SavesInProfile = false;
        args.State = CoreWebView2PermissionState.Deny;
        args.Handled = true;
    }

    /// <summary>
    /// Prevents WebView2 from delegating unsupported URI schemes to
    /// operating-system handlers.
    /// </summary>
    private static void OnLaunchingExternalUriScheme(
        object? sender,
        CoreWebView2LaunchingExternalUriSchemeEventArgs args)
    {
        args.Cancel = true;
    }

    /// <summary>
    /// Prevents browser authentication prompts from being displayed.
    /// </summary>
    private static void OnBasicAuthenticationRequested(
        object? sender,
        CoreWebView2BasicAuthenticationRequestedEventArgs args)
    {
        args.Cancel = true;
    }

    /// <summary>
    /// Prevents client-certificate selection prompts from being displayed.
    /// </summary>
    private static void OnClientCertificateRequested(
        object? sender,
        CoreWebView2ClientCertificateRequestedEventArgs args)
    {
        args.Cancel = true;
    }

    /// <summary>
    /// Retains the frame and its navigation handler for deterministic
    /// unsubscription when this policy is disposed.
    /// </summary>
    private sealed record FrameNavigationHandler(
        CoreWebView2Frame Frame,
        EventHandler<CoreWebView2NavigationStartingEventArgs> Handler);
}
