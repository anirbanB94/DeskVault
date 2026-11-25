using DeskVault.UI.Rendering.HtmlDocumentRendering;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Buffers.Binary;
using System.IO.Compression;
using System.Runtime.ExceptionServices;
using System.Text;
using WindowsForms = System.Windows.Forms;

namespace DeskVault.UI.Tests;

/// <summary>
/// Verifies HTML preview security enforcement against the actual WebView2 runtime.
/// </summary>
public sealed class HtmlWebView2RuntimeSecurityTests
{
    private static readonly TimeSpan BrowserEventTimeout =
        TimeSpan.FromSeconds(15);

    /// <summary>
    /// Verifies that the configured WebView2 instance disables active browser
    /// capabilities, accepts the one authorized initial HTML document load,
    /// prevents script execution and rejects subsequent external navigation.
    /// </summary>
    [Fact]
    public Task Configure_RestrictsBrowserCapabilitiesAndExternalNavigation()
    {
        return RunOnStaThreadAsync(
            async form =>
            {
                // Arrange
                await using HtmlDocumentWebView2ProfileSession profileSession =
                    await CreateInitializedWebViewAsync(form);

                WebView2 webView =
                    profileSession.WebView;

                CoreWebView2 coreWebView2 =
                    webView.CoreWebView2;

                using var securityPolicy =
                    new HtmlDocumentWebView2SecurityPolicy(
                        coreWebView2);

                var initialNavigation =
                    new TaskCompletionSource<
                        (string Uri, bool IsRedirected, bool CancelledByPolicy)>(
                        TaskCreationOptions.RunContinuationsAsynchronously);

                // Observe the initial HTML navigation after the production
                // policy handler so the captured cancellation value reflects
                // the actual security-policy decision.
                coreWebView2.NavigationStarting +=
                    (_, args) =>
                    {
                        if (!args.Uri.StartsWith(
                                "data:text/html",
                                StringComparison.OrdinalIgnoreCase))
                        {
                            return;
                        }

                        initialNavigation.TrySetResult(
                            (
                                args.Uri,
                                args.IsRedirected,
                                args.Cancel
                            ));
                    };

                var externalNavigation =
                    new TaskCompletionSource<bool>(
                        TaskCreationOptions.RunContinuationsAsynchronously);

                // Observe subsequent external navigation after the production
                // handler. The fallback prevents network access if the policy
                // unexpectedly fails to cancel the request.
                coreWebView2.NavigationStarting +=
                    (_, args) =>
                    {
                        if (!args.Uri.StartsWith(
                                "https://example.invalid/",
                                StringComparison.OrdinalIgnoreCase))
                        {
                            return;
                        }

                        bool cancelledByPolicy =
                            args.Cancel;

                        // Fail safely if the production navigation policy regresses.
                        args.Cancel = true;

                        externalNavigation.TrySetResult(
                            cancelledByPolicy);
                    };

                const string html =
                    """
                    <!DOCTYPE html>
                    <html>
                    <head>
                        <meta charset="utf-8" />
                    </head>
                    <body>
                        <p>HTML security runtime verification</p>
                        <script>
                            document.body.dataset.dvHtmlScript = "executed";
                        </script>
                    </body>
                    </html>
                    """;

                // Act
                // Authorize exactly one host-controlled initial document navigation.
                securityPolicy.AuthorizeInitialDocumentNavigation();

                webView.NavigateToString(html);

                (string Uri, bool IsRedirected, bool CancelledByPolicy)
                    initialNavigationObservation =
                        await initialNavigation.Task.WaitAsync(
                            BrowserEventTimeout);

                // Assert: verify that the production policy accepted only
                // the expected, non-redirected initial HTML document navigation.
                Assert.True(
                    initialNavigationObservation.Uri.StartsWith(
                        "data:text/html",
                        StringComparison.OrdinalIgnoreCase),
                    $"Unexpected initial document URI: {initialNavigationObservation.Uri}");

                Assert.False(
                    initialNavigationObservation.IsRedirected);

                Assert.False(
                    initialNavigationObservation.CancelledByPolicy);

                await WaitForDocumentReadyAsync(
                    coreWebView2);

                // Assert: browser capabilities are restricted.
                CoreWebView2Settings settings =
                    coreWebView2.Settings;

                Assert.False(settings.IsScriptEnabled);
                Assert.False(settings.IsWebMessageEnabled);
                Assert.False(settings.AreHostObjectsAllowed);

                Assert.False(settings.AreDefaultScriptDialogsEnabled);
                Assert.False(settings.AreDefaultContextMenusEnabled);
                Assert.False(settings.AreDevToolsEnabled);
                Assert.False(settings.AreBrowserAcceleratorKeysEnabled);

                Assert.False(settings.IsZoomControlEnabled);
                Assert.False(settings.IsStatusBarEnabled);
                Assert.False(settings.IsBuiltInErrorPageEnabled);

                Assert.False(settings.IsGeneralAutofillEnabled);
                Assert.False(settings.IsPasswordAutosaveEnabled);

                // ExecuteScriptAsync is a host-side inspection API. It does not
                // re-enable JavaScript execution within the untrusted document.
                string scriptMarker =
                    await coreWebView2.ExecuteScriptAsync(
                        "document.body.dataset.dvHtmlScript || 'blocked'");

                Assert.Equal(
                    "\"blocked\"",
                    scriptMarker);

                string documentText =
                    await coreWebView2.ExecuteScriptAsync(
                        "document.body.innerText");

                Assert.Contains(
                    "HTML security runtime verification",
                    documentText);

                // Act: request external navigation after the initial document
                // has rendered. The production handler must cancel this request.
                coreWebView2.Navigate(
                    "https://example.invalid/untrusted-navigation");

                bool cancelledByPolicy =
                    await externalNavigation.Task.WaitAsync(
                        BrowserEventTimeout);

                // Assert: the production handler cancelled navigation before
                // the test's safety fallback could be applied.
                Assert.True(cancelledByPolicy);
            });
    }

    /// <summary>
    /// Verifies that the production WebView2 security policy rejects
    /// top-level navigation to an arbitrary local HTML file.
    /// </summary>
    [Fact]
    public Task LocalFileNavigation_IsCancelledBeforeFileAccess()
    {
        return RunOnStaThreadAsync(
            async form =>
            {
                // Arrange
                string localResourceRoot =
                    Path.Combine(
                        Path.GetTempPath(),
                        "DeskVault.Tests",
                        "HtmlPreviewLocalResource",
                        Guid.NewGuid().ToString("N"));

                Directory.CreateDirectory(localResourceRoot);

                string localHtmlPath =
                    Path.Combine(
                        localResourceRoot,
                        "untrusted-local-document.html");

                const string localDocumentMarker =
                    "DESKVAULT_LOCAL_FILE_ACCESS_MUST_BE_BLOCKED";

                await File.WriteAllTextAsync(
                    localHtmlPath,
                    $"""
                <!DOCTYPE html>
                <html>
                <head>
                    <meta charset="utf-8" />
                </head>
                <body>
                    <p>{localDocumentMarker}</p>
                </body>
                </html>
                """);

                string localFileUri =
                    new Uri(localHtmlPath).AbsoluteUri;

                try
                {
                    await using HtmlDocumentWebView2ProfileSession profileSession =
                        await CreateInitializedWebViewAsync(form);

                    CoreWebView2 coreWebView2 =
                        profileSession.WebView.CoreWebView2;

                    using var securityPolicy =
                        new HtmlDocumentWebView2SecurityPolicy(
                            coreWebView2);

                    var localFileNavigation =
                        new TaskCompletionSource<
                            (string Uri, bool CancelledByPolicy)>(
                            TaskCreationOptions.RunContinuationsAsynchronously);

                    // Observe the navigation after the production policy handler.
                    // The fallback prevents local-file navigation if the policy
                    // unexpectedly fails to cancel the request.
                    coreWebView2.NavigationStarting +=
                        (_, args) =>
                        {
                            if (!args.Uri.StartsWith(
                                    localFileUri,
                                    StringComparison.OrdinalIgnoreCase))
                            {
                                return;
                            }

                            bool cancelledByPolicy =
                                args.Cancel;

                            args.Cancel = true;

                            localFileNavigation.TrySetResult(
                                (
                                    args.Uri,
                                    cancelledByPolicy
                                ));
                        };

                    const string initialHtml =
                        """
                    <!DOCTYPE html>
                    <html>
                    <head>
                        <meta charset="utf-8" />
                    </head>
                    <body>
                        <p>Local filesystem navigation security verification</p>
                    </body>
                    </html>
                    """;

                    // Act
                    securityPolicy.AuthorizeInitialDocumentNavigation();

                    profileSession.WebView.NavigateToString(initialHtml);

                    await WaitForDocumentReadyAsync(
                        coreWebView2);

                    // Attempt to navigate to the actual local fixture.
                    coreWebView2.Navigate(localFileUri);

                    (
                        string Uri,
                        bool CancelledByPolicy
                    ) observation =
                        await localFileNavigation.Task.WaitAsync(
                            BrowserEventTimeout);

                    // Assert: the attempted URI was the expected local file.
                    Assert.True(
                        observation.Uri.StartsWith(
                            localFileUri,
                            StringComparison.OrdinalIgnoreCase),
                        $"Unexpected local navigation URI: {observation.Uri}");

                    // Assert: the production policy cancelled navigation
                    // before the test safety fallback was applied.
                    Assert.True(
                        observation.CancelledByPolicy,
                        "The production navigation policy did not cancel local-file navigation.");

                    // Assert: the local fixture's content was never loaded
                    // into the preview document.
                    string renderedDocumentText =
                        await coreWebView2.ExecuteScriptAsync(
                            "document.documentElement.innerText");

                    Assert.DoesNotContain(
                        localDocumentMarker,
                        renderedDocumentText,
                        StringComparison.Ordinal);
                }
                finally
                {
                    // The profile session is disposed before this cleanup runs.
                    if (Directory.Exists(localResourceRoot))
                    {
                        Directory.Delete(
                            localResourceRoot,
                            recursive: true);
                    }
                }
            });
    }

    /// <summary>
    /// Verifies that the WebView2 runtime rejects an external image request
    /// through the production WebResourceRequested security handler.
    /// </summary>
    [Fact]
    public Task WebResourceRequested_BlocksExternalImageRequests()
    {
        return RunOnStaThreadAsync(
            async form =>
            {
                // Arrange
                await using HtmlDocumentWebView2ProfileSession profileSession =
                    await CreateInitializedWebViewAsync(form);

                WebView2 webView =
                    profileSession.WebView;

                CoreWebView2 coreWebView2 =
                    webView.CoreWebView2;

                using var securityPolicy =
                    new HtmlDocumentWebView2SecurityPolicy(
                        coreWebView2);

                const string blockedImageUri =
                    "https://example.invalid/untrusted-image.png";

                var blockedImageRequest =
                    new TaskCompletionSource<
                        (string Uri, CoreWebView2WebResourceContext Context, int? StatusCode)>(
                        TaskCreationOptions.RunContinuationsAsynchronously);

                // The production handler is subscribed by the security-policy
                // constructor before this observer is registered.
                coreWebView2.WebResourceRequested +=
                    (_, args) =>
                    {
                        if (!string.Equals(
                                args.Request.Uri,
                                blockedImageUri,
                                StringComparison.Ordinal))
                        {
                            return;
                        }

                        int? statusCode =
                            args.Response?.StatusCode;

                        blockedImageRequest.TrySetResult(
                            (
                                args.Request.Uri,
                                args.ResourceContext,
                                statusCode
                            ));

                        // Prevent an unintended network request if the production
                        // security handler regresses. The captured status code
                        // still represents the production handler's decision.
                        args.Response ??=
                            coreWebView2.Environment.CreateWebResourceResponse(
                                null,
                                451,
                                "Test safety fallback",
                                "Content-Type: text/plain; charset=utf-8");
                    };

                const string html =
                    """
                <!DOCTYPE html>
                <html>
                <head>
                    <meta charset="utf-8" />
                </head>
                <body>
                    <img
                        id="untrusted-image"
                        src="https://example.invalid/untrusted-image.png"
                        alt="Untrusted external image" />
                </body>
                </html>
                """;

                // Act
                securityPolicy.AuthorizeInitialDocumentNavigation();

                webView.NavigateToString(html);

                (
                    string Uri,
                    CoreWebView2WebResourceContext Context,
                    int? StatusCode
                ) observation =
                    await blockedImageRequest.Task.WaitAsync(
                        BrowserEventTimeout);

                // Assert
                Assert.Equal(
                    blockedImageUri,
                    observation.Uri);

                Assert.Equal(
                    CoreWebView2WebResourceContext.Image,
                    observation.Context);

                Assert.Equal(
                    403,
                    observation.StatusCode);
            });
    }

    /// <summary>
    /// Verifies that navigation initiated by an iframe is rejected by the
    /// production frame-level NavigationStarting security handler.
    /// </summary>
    [Fact]
    public Task FrameNavigationStarting_BlocksExternalFrameNavigation()
    {
        return RunOnStaThreadAsync(
            async form =>
            {
                // Arrange
                await using HtmlDocumentWebView2ProfileSession profileSession =
                    await CreateInitializedWebViewAsync(form);

                WebView2 webView =
                    profileSession.WebView;

                CoreWebView2 coreWebView2 =
                    webView.CoreWebView2;

                using var securityPolicy =
                    new HtmlDocumentWebView2SecurityPolicy(
                        coreWebView2);

                const string blockedFrameUri =
                    "https://example.invalid/untrusted-frame";

                var frameNavigation =
                    new TaskCompletionSource<
                        (string Uri, bool CancelledByPolicy)>(
                        TaskCreationOptions.RunContinuationsAsynchronously);

                // The production FrameCreated handler is registered by the
                // security-policy constructor before this test observer.
                // Consequently, its frame NavigationStarting handler runs first.
                coreWebView2.FrameCreated +=
                    (_, args) =>
                    {
                        args.Frame.NavigationStarting +=
                            (_, navigationArgs) =>
                            {
                                if (!navigationArgs.Uri.StartsWith(
                                        blockedFrameUri,
                                        StringComparison.OrdinalIgnoreCase))
                                {
                                    return;
                                }

                                bool cancelledByPolicy =
                                    navigationArgs.Cancel;

                                // Prevent network access if the production
                                // frame-navigation policy unexpectedly regresses.
                                navigationArgs.Cancel = true;

                                frameNavigation.TrySetResult(
                                    (
                                        navigationArgs.Uri,
                                        cancelledByPolicy
                                    ));
                            };
                    };

                const string html =
                    """
                <!DOCTYPE html>
                <html>
                <head>
                    <meta charset="utf-8" />
                </head>
                <body>
                    <p>Frame navigation security verification</p>
                    <iframe
                        id="untrusted-frame"
                        src="https://example.invalid/untrusted-frame">
                    </iframe>
                </body>
                </html>
                """;

                // Act
                securityPolicy.AuthorizeInitialDocumentNavigation();

                webView.NavigateToString(html);

                (
                    string Uri,
                    bool CancelledByPolicy
                ) observation =
                    await frameNavigation.Task.WaitAsync(
                        BrowserEventTimeout);

                // Assert
                Assert.True(
                    observation.Uri.StartsWith(
                        blockedFrameUri,
                        StringComparison.OrdinalIgnoreCase),
                    $"Unexpected frame navigation URI: {observation.Uri}");

                Assert.True(
                    observation.CancelledByPolicy,
                    "The production security policy did not cancel iframe navigation.");
            });
    }

    /// <summary>
    /// Verifies that the production WebView2 security policy handles
    /// new-window requests without allowing a popup to be created.
    /// </summary>
    [Fact]
    public Task NewWindowRequested_BlocksExternalPopup()
    {
        return RunOnStaThreadAsync(
            async form =>
            {
                // Arrange
                await using HtmlDocumentWebView2ProfileSession profileSession =
                    await CreateInitializedWebViewAsync(form);

                WebView2 webView =
                    profileSession.WebView;

                CoreWebView2 coreWebView2 =
                    webView.CoreWebView2;

                using var securityPolicy =
                    new HtmlDocumentWebView2SecurityPolicy(
                        coreWebView2);

                const string blockedPopupUri =
                    "https://example.invalid/untrusted-popup";

                var popupRequest =
                    new TaskCompletionSource<
                        (string Uri, bool HandledByPolicy)>(
                        TaskCreationOptions.RunContinuationsAsynchronously);

                // The production handler is registered by the security-policy
                // constructor before this observer. Capture its decision before
                // applying a test-only fallback.
                coreWebView2.NewWindowRequested +=
                    (_, args) =>
                    {
                        if (!args.Uri.StartsWith(
                                blockedPopupUri,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            return;
                        }

                        bool handledByPolicy =
                            args.Handled;

                        // Prevent a popup from being created if the production
                        // policy unexpectedly regresses.
                        args.Handled = true;

                        popupRequest.TrySetResult(
                            (
                                args.Uri,
                                handledByPolicy
                            ));
                    };

                const string html =
                    """
                <!DOCTYPE html>
                <html>
                <head>
                    <meta charset="utf-8" />
                </head>
                <body>
                    <a
                        id="untrusted-popup"
                        href="https://example.invalid/untrusted-popup"
                        target="_blank"
                        rel="noopener">
                        Open untrusted popup
                    </a>
                </body>
                </html>
                """;

                // Act
                securityPolicy.AuthorizeInitialDocumentNavigation();

                webView.NavigateToString(html);

                await WaitForDocumentReadyAsync(
                    coreWebView2);

                // ExecuteScriptAsync is used by the host test to activate the
                // anchor. JavaScript remains disabled for document content.
                await coreWebView2.ExecuteScriptAsync(
                    "document.getElementById('untrusted-popup').click()");

                (
                    string Uri,
                    bool HandledByPolicy
                ) observation =
                    await popupRequest.Task.WaitAsync(
                        BrowserEventTimeout);

                // Assert
                Assert.True(
                    observation.Uri.StartsWith(
                        blockedPopupUri,
                        StringComparison.OrdinalIgnoreCase),
                    $"Unexpected popup URI: {observation.Uri}");

                Assert.True(
                    observation.HandledByPolicy,
                    "The production security policy did not handle the popup request.");
            });
    }

    /// <summary>
    /// Verifies that the production WebView2 security policy cancels a
    /// download initiated by an anchor in the untrusted document.
    /// </summary>
    [Fact]
    public Task DownloadStarting_CancelsUntrustedDownloads()
    {
        return RunOnStaThreadAsync(
            async form =>
            {
                // Arrange
                await using HtmlDocumentWebView2ProfileSession profileSession =
                    await CreateInitializedWebViewAsync(form);

                WebView2 webView =
                    profileSession.WebView;

                CoreWebView2 coreWebView2 =
                    webView.CoreWebView2;

                using var securityPolicy =
                    new HtmlDocumentWebView2SecurityPolicy(
                        coreWebView2);

                var downloadStarting =
                    new TaskCompletionSource<
                        (bool CancelledByPolicy, bool HandledByPolicy)>(
                        TaskCreationOptions.RunContinuationsAsynchronously);

                // The production handler is subscribed before this observer.
                // Capture its decision before applying the test safety fallback.
                coreWebView2.DownloadStarting +=
                    (_, args) =>
                    {
                        bool cancelledByPolicy =
                            args.Cancel;

                        bool handledByPolicy =
                            args.Handled;

                        // Ensure a regression cannot leave a download running
                        // or display a browser-owned download dialog.
                        args.Cancel = true;
                        args.Handled = true;

                        downloadStarting.TrySetResult(
                            (
                                cancelledByPolicy,
                                handledByPolicy
                            ));
                    };

                const string html =
                    """
                <!DOCTYPE html>
                <html>
                <head>
                    <meta charset="utf-8" />
                </head>
                <body>
                    <a
                        id="untrusted-download"
                        href="data:text/plain;charset=utf-8,DeskVault%20untrusted%20download"
                        download="untrusted-download.txt">
                        Download untrusted content
                    </a>
                </body>
                </html>
                """;

                // Act
                securityPolicy.AuthorizeInitialDocumentNavigation();

                webView.NavigateToString(html);

                await WaitForDocumentReadyAsync(
                    coreWebView2);

                await coreWebView2.ExecuteScriptAsync(
                    "document.getElementById('untrusted-download').click()");

                (
                    bool CancelledByPolicy,
                    bool HandledByPolicy
                ) =
                    await downloadStarting.Task.WaitAsync(
                        BrowserEventTimeout);

                // Assert
                Assert.True(
                    CancelledByPolicy,
                    "The production security policy did not cancel the download.");

                Assert.True(
                    HandledByPolicy,
                    "The production security policy did not suppress the default download UI.");
            });
    }

    /// <summary>
    /// Verifies that independent HTML previews use unique InPrivate profiles
    /// and that each profile's user-data folder is physically removed on disposal.
    /// </summary>
    [Fact]
    public Task ProfileSession_UsesUniqueInPrivateUserDataFoldersAndDeletesThem()
    {
        return RunOnStaThreadAsync(
            async form =>
            {
                // Arrange
                string userDataRoot =
                    Path.Combine(
                        Path.GetTempPath(),
                        "DeskVault.Tests",
                        "HtmlPreview",
                        Guid.NewGuid().ToString("N"));

                CoreWebView2Environment defaultEnvironment =
                    await CoreWebView2Environment.CreateAsync(
                        null,
                        null);

                HtmlDocumentWebView2ProfileSession? firstSession = null;
                HtmlDocumentWebView2ProfileSession? secondSession = null;

                string? firstUserDataFolder = null;
                string? secondUserDataFolder = null;

                Exception? primaryException = null;

                var cleanupExceptions = new List<Exception>();

                try
                {
                    firstSession =
                        await CreateInitializedWebViewAsync(
                            form,
                            userDataRoot);

                    secondSession =
                        await CreateInitializedWebViewAsync(
                            form,
                            userDataRoot);

                    firstUserDataFolder =
                        firstSession.UserDataFolderPath;

                    secondUserDataFolder =
                        secondSession.UserDataFolderPath;

                    // Assert: both profiles use InPrivate mode.
                    Assert.True(
                        firstSession.WebView.CoreWebView2
                            .Profile.IsInPrivateModeEnabled);

                    Assert.True(
                        secondSession.WebView.CoreWebView2
                            .Profile.IsInPrivateModeEnabled);

                    // Assert: the runtime used the exact isolated UDF requested
                    // by each independent profile session.
                    Assert.Equal(Path.GetFullPath(firstUserDataFolder), Path.GetFullPath(
                                firstSession.WebView.CoreWebView2.Environment.UserDataFolder), ignoreCase: true);

                    Assert.Equal(Path.GetFullPath(secondUserDataFolder), Path.GetFullPath(
                                secondSession.WebView.CoreWebView2.Environment.UserDataFolder), ignoreCase: true);

                    // Assert: previews do not share a profile with each other
                    // or the default environment used by the Markdown renderer.
                    Assert.NotEqual(
                        firstUserDataFolder,
                        secondUserDataFolder);

                    Assert.NotEqual(
                        Path.GetFullPath(defaultEnvironment.UserDataFolder),
                        Path.GetFullPath(firstUserDataFolder));

                    Assert.NotEqual(
                        Path.GetFullPath(defaultEnvironment.UserDataFolder),
                        Path.GetFullPath(secondUserDataFolder));

                    Assert.True(
                        Directory.Exists(firstUserDataFolder));

                    Assert.True(
                        Directory.Exists(secondUserDataFolder));
                }
                catch (Exception exception)
                {
                    // Preserve the original test failure while allowing both
                    // profile sessions to execute their cleanup paths.
                    primaryException = exception;
                }
                finally
                {
                    // Attempt cleanup for both independent profiles even if one profile
                    // fails to dispose. One failure must not leak the other WebView2 session.
                    if (secondSession is not null)
                    {
                        try
                        {
                            await secondSession.DisposeAsync();
                        }
                        catch (Exception exception)
                        {
                            cleanupExceptions.Add(exception);
                        }
                    }

                    if (firstSession is not null)
                    {
                        try
                        {
                            await firstSession.DisposeAsync();
                        }
                        catch (Exception exception)
                        {
                            cleanupExceptions.Add(exception);
                        }
                    }
                }

                if (primaryException is not null)
                {
                    if (cleanupExceptions.Count > 0)
                    {
                        var exceptions = new List<Exception>
                        {
                            primaryException
                        };

                        exceptions.AddRange(cleanupExceptions);

                        throw new AggregateException(
                            "The profile-isolation test and one or more profile cleanup operations failed.",
                            exceptions);
                    }

                    // Preserve the original test exception and its stack trace.
                    ExceptionDispatchInfo.Capture(primaryException).Throw();
                }

                if (cleanupExceptions.Count > 0)
                {
                    throw new AggregateException(
                        "One or more HTML preview profile sessions failed to clean up.",
                        cleanupExceptions);
                }

                // Assert: cleanup completed physically, not merely by issuing
                // a deferred profile-deletion request.
                Assert.False(
                    Directory.Exists(firstUserDataFolder));

                Assert.False(
                    Directory.Exists(secondUserDataFolder));

                if (Directory.Exists(userDataRoot))
                {
                    Directory.Delete(
                        userDataRoot,
                        recursive: true);
                }
            });
    }

    /// <summary>
    /// Verifies that unique HTML content and embedded-image payload markers
    /// are not retained in disk-backed WebView2 user data after profile cleanup.
    /// </summary>
    [Fact]
    public Task ProfileSession_DoesNotRetainDocumentOrEmbeddedImageMarkers()
    {
        return RunOnStaThreadAsync(
            async form =>
            {
                // Arrange
                string testIdentifier =
                    Guid.NewGuid().ToString("N");

                string userDataRoot =
                    Path.Combine(
                        Path.GetTempPath(),
                        "DeskVault.Tests",
                        "HtmlPreviewRetention",
                        testIdentifier);

                string documentMarker =
                    $"DV_HTML_DOCUMENT_{testIdentifier}";

                string imageMarker =
                    $"DV_HTML_IMAGE_{testIdentifier}";

                byte[] pngPayload =
                    CreatePngWithTextMarker(imageMarker);

                string imageDataUri =
                    "data:image/png;base64,"
                    + Convert.ToBase64String(pngPayload);

                string? profileFolderPath = null;

                bool profileFolderRemains;
                bool documentMarkerRemains;
                bool imageMarkerRemains;

                // Act
                {
                    await using HtmlDocumentWebView2ProfileSession profileSession =
                        await CreateInitializedWebViewAsync(
                            form,
                            userDataRoot);

                    profileFolderPath =
                        profileSession.UserDataFolderPath;

                    using var securityPolicy =
                        new HtmlDocumentWebView2SecurityPolicy(
                            profileSession.WebView.CoreWebView2);

                    string html =
                        $$"""
                    <!DOCTYPE html>
                    <html>
                    <head>
                        <meta charset="utf-8" />
                        <meta
                            http-equiv="Content-Security-Policy"
                            content="default-src 'none'; img-src data:; style-src 'unsafe-inline'; script-src 'none'; object-src 'none'; frame-src 'none'; connect-src 'none'; base-uri 'none'; form-action 'none'" />
                    </head>
                    <body>
                        <p id="retention-marker">{{documentMarker}}</p>
                        <img
                            id="embedded-image"
                            src="{{imageDataUri}}"
                            alt="Embedded preview image" />
                    </body>
                    </html>
                    """;

                    securityPolicy.AuthorizeInitialDocumentNavigation();

                    profileSession.WebView.NavigateToString(html);

                    CoreWebView2 coreWebView2 =
                        profileSession.WebView.CoreWebView2;

                    await WaitForDocumentReadyAsync(
                        coreWebView2);

                    await WaitForImageReadyAsync(
                        coreWebView2);

                    // Assert: verify that the intended synthetic content was
                    // actually presented before the profile is destroyed.
                    string renderedDocumentMarker =
                        await coreWebView2.ExecuteScriptAsync(
                            "document.getElementById('retention-marker').textContent");

                    Assert.Equal(
                        $"\"{documentMarker}\"",
                        renderedDocumentMarker);

                    string imageWidth =
                        await coreWebView2.ExecuteScriptAsync(
                            "document.getElementById('embedded-image').naturalWidth");

                    Assert.Equal(
                        "1",
                        imageWidth);
                }

                // Assert: the await-using scope has disposed the security policy
                // before the profile session and completed its cleanup operation.
                Assert.NotNull(profileFolderPath);

                profileFolderRemains =
                    Directory.Exists(profileFolderPath);

                documentMarkerRemains =
                    DirectoryContainsMarker(
                        userDataRoot,
                        documentMarker);

                imageMarkerRemains =
                    DirectoryContainsMarker(
                        userDataRoot,
                        imageMarker);

                Assert.False(
                    profileFolderRemains,
                    "The HTML preview user-data folder remains after profile cleanup.");

                Assert.False(
                    documentMarkerRemains,
                    "The synthetic HTML document marker remains in disk-backed preview user data.");

                Assert.False(
                    imageMarkerRemains,
                    "The synthetic embedded-image payload marker remains in disk-backed preview user data.");

                // The parent is unique to this test. Remove it only after
                // the production profile-cleanup assertions have succeeded.
                if (Directory.Exists(userDataRoot))
                {
                    Directory.Delete(
                        userDataRoot,
                        recursive: true);
                }
            });
    }

    /// <summary>
    /// Verifies that profile cleanup failures are observable and that
    /// disposal can be retried successfully after the blocking file handle
    /// is released.
    /// </summary>
    [Fact]
    public Task ProfileSession_ReportsCleanupFailureAndSupportsRetry()
    {
        return RunOnStaThreadAsync(
            async form =>
            {
                // Arrange
                string userDataRoot =
                    Path.Combine(
                        Path.GetTempPath(),
                        "DeskVault.Tests",
                        "HtmlPreviewCleanupRetry",
                        Guid.NewGuid().ToString("N"));

                await using HtmlDocumentWebView2ProfileSession profileSession =
                    await CreateInitializedWebViewAsync(
                        form,
                        userDataRoot);

                string profileFolderPath =
                    profileSession.UserDataFolderPath;

                string lockedFilePath =
                    Path.Combine(
                        profileFolderPath,
                        "cleanup-lock.marker");

                await File.WriteAllTextAsync(
                    lockedFilePath,
                    "Cleanup failure verification.");

                HtmlDocumentWebView2ProfileCleanupException cleanupException;

                // Act: keep an exclusive file handle open so recursive directory
                // deletion cannot remove the preview profile.
                using (var fileLock = new FileStream(
                           lockedFilePath,
                           FileMode.Open,
                           FileAccess.ReadWrite,
                           FileShare.None))
                {
                    cleanupException =
                        await Assert.ThrowsAsync<
                            HtmlDocumentWebView2ProfileCleanupException>(
                            async () =>
                                await profileSession.DisposeAsync());

                    // Assert: cleanup failure is explicitly reported and the
                    // profile directory is not incorrectly reported as deleted.
                    Assert.Equal(
                        HtmlDocumentWebView2ProfileCleanupStage.DeleteUserDataFolder,
                        cleanupException.Stage);

                    Assert.True(
                        Directory.Exists(profileFolderPath));

                    Assert.True(
                        File.Exists(lockedFilePath));
                }

                // Act: retry after the blocking file handle has been released.
                await profileSession.DisposeAsync();

                // Assert: the retry completes physical deletion.
                Assert.False(
                    Directory.Exists(profileFolderPath));

                // Assert: an additional disposal call remains idempotent.
                await profileSession.DisposeAsync();

                Assert.False(
                    Directory.Exists(profileFolderPath));
            });
    }

    /// <summary>
    /// Verifies that the production navigation policy rejects an external
    /// URI-scheme navigation before WebView2 can hand it to the operating system.
    /// </summary>
    [Fact]
    public Task ExternalUriSchemeNavigation_IsCancelledBeforeOperatingSystemLaunch()
    {
        return RunOnStaThreadAsync(
            async form =>
            {
                // Arrange
                await using HtmlDocumentWebView2ProfileSession profileSession =
                    await CreateInitializedWebViewAsync(form);

                CoreWebView2 coreWebView2 =
                    profileSession.WebView.CoreWebView2;

                using var securityPolicy =
                    new HtmlDocumentWebView2SecurityPolicy(
                        coreWebView2);

                var externalProtocolNavigation =
                    new TaskCompletionSource<
                        (string Uri, bool CancelledByPolicy)>(
                        TaskCreationOptions.RunContinuationsAsynchronously);

                // Observe the navigation after the production policy handler.
                // The fallback prevents an OS handoff if the production policy
                // unexpectedly fails to cancel the request.
                coreWebView2.NavigationStarting +=
                    (_, args) =>
                    {
                        if (!args.Uri.StartsWith(
                                "mailto:",
                                StringComparison.OrdinalIgnoreCase))
                        {
                            return;
                        }

                        bool cancelledByPolicy =
                            args.Cancel;

                        args.Cancel = true;

                        externalProtocolNavigation.TrySetResult(
                            (
                                args.Uri,
                                cancelledByPolicy
                            ));
                    };

                const string html =
                    """
                <!DOCTYPE html>
                <html>
                <head>
                    <meta charset="utf-8" />
                </head>
                <body>
                    <p>External URI-scheme security verification</p>
                </body>
                </html>
                """;

                // Act
                securityPolicy.AuthorizeInitialDocumentNavigation();

                profileSession.WebView.NavigateToString(html);

                await WaitForDocumentReadyAsync(
                    coreWebView2);

                // Request an external protocol navigation directly through
                // WebView2. The production NavigationStarting handler must
                // cancel it before the OS protocol-launch stage.
                coreWebView2.Navigate(
                    "mailto:desk-vault-test@example.invalid");

                (
                    string Uri,
                    bool CancelledByPolicy
                ) observation =
                    await externalProtocolNavigation.Task.WaitAsync(
                        BrowserEventTimeout);

                // Assert
                Assert.True(
                    observation.Uri.StartsWith(
                        "mailto:",
                        StringComparison.OrdinalIgnoreCase),
                    $"Unexpected external URI scheme: {observation.Uri}");

                Assert.True(
                    observation.CancelledByPolicy,
                    "The production navigation policy did not cancel the external URI-scheme navigation.");
            });
    }

    /// <summary>
    /// Verifies that form submission cannot navigate the WebView2 preview
    /// to an external destination.
    /// </summary>
    [Fact]
    public Task FormSubmission_IsCancelledBeforeExternalNavigation()
    {
        return RunOnStaThreadAsync(
            async form =>
            {
                // Arrange
                await using HtmlDocumentWebView2ProfileSession profileSession =
                    await CreateInitializedWebViewAsync(form);

                CoreWebView2 coreWebView2 =
                    profileSession.WebView.CoreWebView2;

                using var securityPolicy =
                    new HtmlDocumentWebView2SecurityPolicy(
                        coreWebView2);

                const string expectedActionUri =
                    "https://example.invalid/form-submit";

                var formNavigation =
                    new TaskCompletionSource<
                        (string Uri, bool CancelledByPolicy)>(
                        TaskCreationOptions.RunContinuationsAsynchronously);

                // Observe the navigation after the production policy handler.
                // The fallback prevents external navigation if the policy
                // unexpectedly fails to cancel the form submission.
                coreWebView2.NavigationStarting +=
                    (_, args) =>
                    {
                        if (!args.Uri.StartsWith(
                                expectedActionUri,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            return;
                        }

                        bool cancelledByPolicy =
                            args.Cancel;

                        args.Cancel = true;

                        formNavigation.TrySetResult(
                            (
                                args.Uri,
                                cancelledByPolicy
                            ));
                    };

                const string html =
                    """
                <!DOCTYPE html>
                <html>
                <head>
                    <meta charset="utf-8" />
                </head>
                <body>
                    <p>Form submission security verification</p>

                    <form
                        id="untrusted-form"
                        action="https://example.invalid/form-submit"
                        method="get">
                        <input
                            type="text"
                            name="marker"
                            value="desk-vault-form-submission-test" />
                        <button type="submit">Submit</button>
                    </form>
                </body>
                </html>
                """;

                // Act
                securityPolicy.AuthorizeInitialDocumentNavigation();

                profileSession.WebView.NavigateToString(html);

                await WaitForDocumentReadyAsync(
                    coreWebView2);

                // Trigger a normal form submission through the host-side
                // WebView2 script API. This does not enable document scripts.
                await coreWebView2.ExecuteScriptAsync(
                    "document.getElementById('untrusted-form').requestSubmit()");

                (
                    string Uri,
                    bool CancelledByPolicy
                ) observation =
                    await formNavigation.Task.WaitAsync(
                        BrowserEventTimeout);

                // Assert
                Assert.True(
                    observation.Uri.StartsWith(
                        expectedActionUri,
                        StringComparison.OrdinalIgnoreCase),
                    $"Unexpected form action URI: {observation.Uri}");

                Assert.True(
                    observation.CancelledByPolicy,
                    "The production navigation policy did not cancel the form-submission navigation.");
            });
    }

    /// <summary>
    /// Creates a WebView2 control initialized through the dedicated HTML-preview
    /// profile session on the test form's UI thread.
    /// </summary>
    /// <param name="form">The WinForms test host.</param>
    /// <param name="userDataRoot">
    /// Optional isolated parent folder used by the profile-session test.
    /// </param>
    /// <returns>
    /// An initialized profile session that owns the WebView2 control.
    /// </returns>
    private static async Task<HtmlDocumentWebView2ProfileSession>
    CreateInitializedWebViewAsync(
        Form form,
        string? userDataRoot = null)
    {
        var webView = new WebView2
        {
            Dock = DockStyle.Fill
        };

        form.Controls.Add(webView);

        return await HtmlDocumentWebView2ProfileSession.CreateAsync(
            webView,
            userDataRoot);
    }

    /// <summary>
    /// Waits until the WebView2 document has completed rendering non-empty content.
    /// </summary>
    private static async Task WaitForDocumentReadyAsync(
        CoreWebView2 coreWebView2)
    {
        DateTime timeoutAt =
            DateTime.UtcNow.Add(BrowserEventTimeout);

        const string readinessScript =
            """
            (() => {
                const body = document.body;

                return document.readyState === "complete"
                    && body !== null
                    && body.innerText.trim().length > 0;
            })()
            """;

        string lastReadinessResult = "not evaluated";

        while (DateTime.UtcNow < timeoutAt)
        {
            lastReadinessResult =
                await coreWebView2.ExecuteScriptAsync(
                    readinessScript);

            if (string.Equals(
                    lastReadinessResult,
                    "true",
                    StringComparison.Ordinal))
            {
                return;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException(
            "The HTML WebView2 document did not reach the expected rendered state. "
            + $"Source: '{coreWebView2.Source}'. "
            + $"Last readiness result: '{lastReadinessResult}'.");
    }

    /// <summary>
    /// Executes the supplied WebView2 test body on a dedicated STA thread
    /// with a WinForms message loop.
    /// </summary>
    private static Task<bool> RunOnStaThreadAsync(
        Func<Form, Task> testBody)
    {
        var completion =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var thread = new Thread(
            () =>
            {
                try
                {
                    using var form = new Form
                    {
                        Width = 900,
                        Height = 650,
                        ShowInTaskbar = false,
                        StartPosition = FormStartPosition.Manual,
                        Location = new Point(-2000, -2000)
                    };

                    form.Shown +=
                        async (_, _) =>
                        {
                            try
                            {
                                await testBody(form);

                                completion.TrySetResult(true);
                            }
                            catch (Exception exception)
                            {
                                completion.TrySetException(exception);
                            }
                            finally
                            {
                                form.Close();
                            }
                        };

                    WindowsForms.Application.Run(form);
                }
                catch (Exception exception)
                {
                    completion.TrySetException(exception);
                }
            })
        {
            IsBackground = true
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        return completion.Task.WaitAsync(
            TimeSpan.FromSeconds(60));
    }

    /// <summary>
    /// Creates a valid one-pixel RGBA PNG containing a synthetic text marker
    /// in a PNG text metadata chunk.
    /// </summary>
    /// <param name="marker">The unique marker embedded in the PNG payload.</param>
    /// <returns>The complete PNG payload.</returns>
    private static byte[] CreatePngWithTextMarker(
        string marker)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marker);

        using var output = new MemoryStream();

        // PNG signature.
        output.Write(
            [
            137, 80, 78, 71, 13, 10, 26, 10
            ]);

        // Construct a 1x1, 8-bit RGBA image.
        byte[] imageHeader = new byte[13];

        BinaryPrimitives.WriteUInt32BigEndian(
            imageHeader.AsSpan(0, 4),
            1);

        BinaryPrimitives.WriteUInt32BigEndian(
            imageHeader.AsSpan(4, 4),
            1);

        imageHeader[8] = 8;
        imageHeader[9] = 6;
        imageHeader[10] = 0;
        imageHeader[11] = 0;
        imageHeader[12] = 0;

        WritePngChunk(
            output,
            "IHDR",
            imageHeader);

        // PNG tEXt uses a keyword, a null separator, and the text value.
        byte[] textData =
            Encoding.ASCII.GetBytes(
                $"Comment\0{marker}");

        WritePngChunk(
            output,
            "tEXt",
            textData);

        // One scanline: filter byte followed by one opaque red RGBA pixel.
        byte[] scanline =
        [
            0,
        255,
        0,
        0,
        255
        ];

        using var compressedImage = new MemoryStream();

        using (var zlib = new ZLibStream(
                   compressedImage,
                   CompressionLevel.Optimal,
                   leaveOpen: true))
        {
            zlib.Write(scanline);
        }

        WritePngChunk(
            output,
            "IDAT",
            compressedImage.ToArray());

        WritePngChunk(
            output,
            "IEND",
            []);

        return output.ToArray();
    }

    /// <summary>
    /// Writes a PNG chunk with its length, type, payload, and CRC-32 checksum.
    /// </summary>
    /// <param name="output">The destination PNG stream.</param>
    /// <param name="chunkType">The four-character PNG chunk type.</param>
    /// <param name="data">The chunk payload.</param>
    private static void WritePngChunk(
        Stream output,
        string chunkType,
        byte[] data)
    {
        byte[] typeBytes =
            Encoding.ASCII.GetBytes(chunkType);

        if (typeBytes.Length != 4)
        {
            throw new ArgumentException(
                "A PNG chunk type must contain exactly four ASCII characters.",
                nameof(chunkType));
        }

        Span<byte> lengthBuffer = stackalloc byte[4];

        BinaryPrimitives.WriteUInt32BigEndian(
            lengthBuffer,
            checked((uint)data.Length));

        output.Write(lengthBuffer);
        output.Write(typeBytes);
        output.Write(data);

        uint crc =
            CalculatePngCrc(
                typeBytes,
                data);

        Span<byte> crcBuffer = stackalloc byte[4];

        BinaryPrimitives.WriteUInt32BigEndian(
            crcBuffer,
            crc);

        output.Write(crcBuffer);
    }

    /// <summary>
    /// Calculates the PNG CRC-32 checksum for a chunk type and its payload.
    /// </summary>
    /// <param name="chunkType">The four-byte chunk type.</param>
    /// <param name="data">The chunk payload.</param>
    /// <returns>The calculated unsigned CRC-32 value.</returns>
    private static uint CalculatePngCrc(
        ReadOnlySpan<byte> chunkType,
        ReadOnlySpan<byte> data)
    {
        const uint Polynomial = 0xEDB88320;

        uint crc = 0xFFFFFFFF;

        foreach (byte value in chunkType)
        {
            crc = UpdatePngCrc(
                crc,
                value,
                Polynomial);
        }

        foreach (byte value in data)
        {
            crc = UpdatePngCrc(
                crc,
                value,
                Polynomial);
        }

        return crc ^ 0xFFFFFFFF;
    }

    /// <summary>
    /// Updates a PNG CRC-32 accumulator with one byte.
    /// </summary>
    /// <param name="crc">The current CRC accumulator.</param>
    /// <param name="value">The next byte.</param>
    /// <param name="polynomial">The CRC-32 polynomial.</param>
    /// <returns>The updated CRC accumulator.</returns>
    private static uint UpdatePngCrc(
        uint crc,
        byte value,
        uint polynomial)
    {
        crc ^= value;

        for (int bit = 0; bit < 8; bit++)
        {
            crc =
                (crc & 1) != 0
                    ? (crc >> 1) ^ polynomial
                    : crc >> 1;
        }

        return crc;
    }

    /// <summary>
    /// Waits until an embedded image has completed decoding and has the expected
    /// one-pixel intrinsic width.
    /// </summary>
    /// <param name="coreWebView2">The initialized WebView2 instance.</param>
    private static async Task WaitForImageReadyAsync(
        CoreWebView2 coreWebView2)
    {
        DateTime timeoutAt =
            DateTime.UtcNow.Add(BrowserEventTimeout);

        const string imageReadinessScript =
            """
        (() => {
            const image = document.getElementById("embedded-image");

            return image !== null
                && image.complete
                && image.naturalWidth === 1;
        })()
        """;

        while (DateTime.UtcNow < timeoutAt)
        {
            string result =
                await coreWebView2.ExecuteScriptAsync(
                    imageReadinessScript);

            if (string.Equals(
                    result,
                    "true",
                    StringComparison.Ordinal))
            {
                return;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException(
            "The approved embedded PNG did not finish decoding in WebView2.");
    }

    /// <summary>
    /// Searches disk-backed files under a directory for a marker encoded as
    /// UTF-8, UTF-16 little-endian, or UTF-16 big-endian.
    /// </summary>
    /// <param name="directoryPath">The directory to inspect.</param>
    /// <param name="marker">The synthetic marker to find.</param>
    /// <returns>
    /// True when at least one file contains the marker; otherwise false.
    /// </returns>
    private static bool DirectoryContainsMarker(
        string directoryPath,
        string marker)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(marker);

        if (!Directory.Exists(directoryPath))
        {
            return false;
        }

        byte[][] markerEncodings =
        [
            Encoding.UTF8.GetBytes(marker),
        Encoding.Unicode.GetBytes(marker),
        Encoding.BigEndianUnicode.GetBytes(marker)
        ];

        foreach (string filePath in Directory.EnumerateFiles(
                     directoryPath,
                     "*",
                     SearchOption.AllDirectories))
        {
            using var fileStream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);

            foreach (byte[] encodedMarker in markerEncodings)
            {
                fileStream.Position = 0;

                if (StreamContainsByteSequence(
                        fileStream,
                        encodedMarker))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Searches a stream for a byte sequence using a bounded buffer while
    /// preserving matches that span consecutive read operations.
    /// </summary>
    /// <param name="stream">The stream to inspect.</param>
    /// <param name="sequence">The byte sequence to locate.</param>
    /// <returns>True when the sequence is found.</returns>
    private static bool StreamContainsByteSequence(
        Stream stream,
        ReadOnlySpan<byte> sequence)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (sequence.IsEmpty)
        {
            return true;
        }

        const int BufferSize = 8192;

        byte[] buffer =
            new byte[BufferSize + sequence.Length - 1];

        int retainedByteCount = 0;

        while (true)
        {
            int bytesRead =
                stream.Read(
                    buffer,
                    retainedByteCount,
                    buffer.Length - retainedByteCount);

            if (bytesRead == 0)
            {
                return false;
            }

            int availableBytes =
                retainedByteCount + bytesRead;

            if (buffer.AsSpan(0, availableBytes).IndexOf(sequence) >= 0)
            {
                return true;
            }

            retainedByteCount =
                Math.Min(
                    sequence.Length - 1,
                    availableBytes);

            buffer.AsSpan(
                    availableBytes - retainedByteCount,
                    retainedByteCount)
                .CopyTo(buffer);
        }
    }
}
