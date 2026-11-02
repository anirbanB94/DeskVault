using DeskVault.Application.Documents.Extraction;
using DeskVault.Application.Documents.Extraction.MarkdownDocument;
using DeskVault.UI.Rendering.MarkdownDocumentRendering;
using Microsoft.Extensions.Options;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using WindowsForms = System.Windows.Forms;

namespace DeskVault.UI.Tests;

public sealed class MarkdownWebView2RuntimeSecurityTests
{
    private static readonly TimeSpan BrowserEventTimeout =
        TimeSpan.FromSeconds(15);

    [Fact]
    public Task RenderAsync_UsesWebView2RuntimeAndEnforcesInitialDocumentPolicy()
    {
        return RunOnStaThreadAsync(
            async form =>
            {
                // Arrange
                var renderer = CreateRenderer(
                    allowRawHtml: true,
                    allowExternalResources: false,
                    allowExternalNavigation: false);

                const string markdown =
                    """
                    <script>
                    document.body.dataset.dvRuntimeScript = "executed";
                    </script>

                    # Runtime rendering proof

                    The Markdown document loaded in the real WebView2 runtime.
                    """;

                using var documentStream =
                    new MemoryStream(
                        Encoding.UTF8.GetBytes(markdown));

                // Act
                await renderer.RenderAsync(
                    form,
                    documentStream,
                    "runtime-security.md");

                WebView2 webView =
                    Assert.Single(
                        form.Controls.OfType<WebView2>());

                CoreWebView2 coreWebView2 =
                    webView.CoreWebView2;

                await WaitForDocumentReadyAsync(coreWebView2);

                string scriptMarker =
                    await coreWebView2.ExecuteScriptAsync(
                        "document.body.dataset.dvRuntimeScript || 'blocked'");

                string contentSecurityPolicy =
                    await coreWebView2.ExecuteScriptAsync(
                        "document.querySelector('meta[http-equiv=\"Content-Security-Policy\"]').content");

                // Assert
                Assert.False(
                    coreWebView2.Settings.IsScriptEnabled);

                Assert.Equal(
                    "\"blocked\"",
                    scriptMarker);

                Assert.Contains(
                    "The Markdown document loaded",
                    await coreWebView2.ExecuteScriptAsync(
                        "document.body.innerText"));

                Assert.Contains(
                    "script-src 'none'",
                    contentSecurityPolicy);

                Assert.Contains(
                    "img-src 'none'",
                    contentSecurityPolicy);

                // Navigations initiated through WebView2 APIs are reported as
                // user-initiated. External navigation must still respect the
                // independently configured AllowExternalNavigation option.
                // Cancel as a safety fallback to avoid an external network request.
                var externalNavigation =
                    new TaskCompletionSource<
                        (bool IsUserInitiated, bool CancelledByRenderer)>(
                        TaskCreationOptions.RunContinuationsAsynchronously);

                coreWebView2.NavigationStarting +=
                    (_, args) =>
                    {
                        if (!args.Uri.StartsWith(
                                "https://example.invalid/",
                                StringComparison.OrdinalIgnoreCase))
                        {
                            return;
                        }

                        bool cancelledByRenderer =
                            args.Cancel;

                        args.Cancel = true;

                        externalNavigation.TrySetResult(
                            (
                                args.IsUserInitiated,
                                cancelledByRenderer
                            ));
                    };

                coreWebView2.Navigate(
                    "https://example.invalid/mvp2-runtime-probe");

                (bool IsUserInitiated, bool CancelledByRenderer) navigationResult =
                    await externalNavigation.Task.WaitAsync(
                        BrowserEventTimeout);

                Assert.True(
                    navigationResult.IsUserInitiated);

                Assert.True(
                    navigationResult.CancelledByRenderer);
            });
    }

    [Fact]
    public Task ConfigureSecurityAndNavigate_AllowsHttpNavigationAndAuthorizesSameNavigationRedirect()
    {
        return RunOnStaThreadAsync(
            async form =>
            {
                // Arrange
                await using var server =
                    LocalRedirectTestServer.Start();

                WebView2 webView =
                    await CreateInitializedWebViewAsync(form);

                CoreWebView2 coreWebView2 =
                    webView.CoreWebView2;

                var renderer = CreateRenderer(
                    allowRawHtml: false,
                    allowExternalResources: true,
                    allowExternalNavigation: true);

                renderer.ConfigureSecurityAndNavigate(
                    webView,
                    MarkdownDocumentContentRenderer.WrapHtmlDocument(
                        "<p>Navigation redirect verification</p>",
                        allowExternalResources: true));

                await WaitForDocumentReadyAsync(coreWebView2);

                var navigationEvents =
                    new ConcurrentQueue<NavigationObservation>();

                var navigationCompleted =
                    new TaskCompletionSource<
                        (bool IsSuccess, CoreWebView2WebErrorStatus Status)>(
                        TaskCreationOptions.RunContinuationsAsynchronously);

                coreWebView2.NavigationStarting +=
                    (_, args) =>
                    {
                        if (!args.Uri.StartsWith(
                                server.Origin,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            return;
                        }

                        navigationEvents.Enqueue(
                            new NavigationObservation(
                                args.Uri,
                                args.NavigationId,
                                args.IsRedirected,
                                args.IsUserInitiated,
                                args.Cancel));
                    };

                coreWebView2.NavigationCompleted +=
                    (_, args) =>
                    {
                        navigationCompleted.TrySetResult(
                            (
                                args.IsSuccess,
                                args.WebErrorStatus
                            ));
                    };

                // Act
                // CoreWebView2.Navigate initiates the first HTTP navigation.
                // The local test server redirects that same navigation to /final.
                coreWebView2.Navigate(server.RedirectUri);

                await server.FinalRequestReceived.WaitAsync(
                    BrowserEventTimeout);

                (bool IsSuccess, CoreWebView2WebErrorStatus Status) result =
                    await navigationCompleted.Task.WaitAsync(
                        BrowserEventTimeout);

                NavigationObservation[] observations =
                    navigationEvents.ToArray();

                // Assert
                Assert.Equal(2, observations.Length);

                NavigationObservation initialNavigation =
                    observations[0];

                NavigationObservation redirectedNavigation =
                    observations[1];

                Assert.Equal(
                    server.RedirectUri,
                    initialNavigation.Uri);

                Assert.False(
                    initialNavigation.IsRedirected);

                Assert.True(
                    initialNavigation.IsUserInitiated);

                Assert.False(
                    initialNavigation.CancelledByRenderer);

                Assert.Equal(
                    server.FinalUri,
                    redirectedNavigation.Uri);

                Assert.True(
                    redirectedNavigation.IsRedirected);

                // Redirect authorization is carried by the previously approved
                // navigation ID. Do not depend on IsUserInitiated for the redirect event.

                Assert.Equal(
                    initialNavigation.NavigationId,
                    redirectedNavigation.NavigationId);

                Assert.False(
                    redirectedNavigation.CancelledByRenderer);

                Assert.True(result.IsSuccess);

                Assert.Equal(
                    server.FinalUri,
                    coreWebView2.Source);

                Assert.Contains(
                    "redirect-final",
                    await coreWebView2.ExecuteScriptAsync(
                        "document.body.innerText"));
            });
    }

    [Fact]
    public Task ConfigureSecurityAndNavigate_BlocksResourceRequestsAndFrameNavigation()
    {
        return RunOnStaThreadAsync(
            async form =>
            {
                // Arrange
                WebView2 webView =
                    await CreateInitializedWebViewAsync(form);

                CoreWebView2 coreWebView2 =
                    webView.CoreWebView2;

                var renderer = CreateRenderer(
                    allowRawHtml: false,
                    allowExternalResources: false,
                    allowExternalNavigation: false);

                const string resourceUri =
                    "http://127.0.0.1:9/dv-runtime-resource-probe.png";

                const string frameUri =
                    "http://127.0.0.1:9/dv-runtime-frame-probe";

                string html =
                    $$"""
                    <!DOCTYPE html>
                    <html>
                    <head><meta charset="utf-8" /></head>
                    <body>
                        <p>WebView2 runtime event verification</p>
                        <img src="{{resourceUri}}" />
                        <iframe src="{{frameUri}}"></iframe>
                    </body>
                    </html>
                    """;

                var resourceResponse =
                    new TaskCompletionSource<int>(
                        TaskCreationOptions.RunContinuationsAsynchronously);

                var frameNavigation =
                    new TaskCompletionSource<bool>(
                        TaskCreationOptions.RunContinuationsAsynchronously);

                // Act
                renderer.ConfigureSecurityAndNavigate(
                    webView,
                    html);

                // Subscribe after the production handler so the captured
                // values show the production handler's decisions.
                coreWebView2.WebResourceRequested +=
                    (_, args) =>
                    {
                        if (!string.Equals(
                                args.Request.Uri,
                                resourceUri,
                                StringComparison.Ordinal))
                        {
                            return;
                        }

                        int statusCode =
                            args.Response?.StatusCode ?? 0;

                        resourceResponse.TrySetResult(statusCode);

                        // Fail safely if the production handler did not
                        // provide a response: do not allow a network request.
                        if (args.Response is null)
                        {
                            args.Response =
                                coreWebView2.Environment
                                    .CreateWebResourceResponse(
                                        null,
                                        451,
                                        "Test fallback",
                                        "Content-Type: text/plain");
                        }
                    };

                coreWebView2.FrameCreated +=
                    (_, args) =>
                    {
                        args.Frame.NavigationStarting +=
                            (_, frameArgs) =>
                            {
                                bool cancelledByRenderer =
                                    frameArgs.Cancel;

                                // Prevent a request if the production
                                // frame handler did not cancel it.
                                frameArgs.Cancel = true;

                                frameNavigation.TrySetResult(
                                    cancelledByRenderer);
                            };
                    };

                await Task.WhenAll(
                    resourceResponse.Task.WaitAsync(
                        BrowserEventTimeout),
                    frameNavigation.Task.WaitAsync(
                        BrowserEventTimeout));

                // Assert
                Assert.Equal(
                    403,
                    await resourceResponse.Task);

                Assert.True(
                    await frameNavigation.Task);
            });
    }

    [Fact]
    public Task GeneratedContentSecurityPolicy_BlocksExternalImageRequestsAtRuntime()
    {
        return RunOnStaThreadAsync(
            async form =>
            {
                // Arrange
                await using var server =
                    LocalProbeServer.Start();

                WebView2 webView =
                    await CreateInitializedWebViewAsync(form);

                CoreWebView2 coreWebView2 =
                    webView.CoreWebView2;

                // Mirror the production JavaScript restriction, but deliberately
                // do not register the production resource/navigation event handlers.
                coreWebView2.Settings.IsScriptEnabled = false;

                var navigationCompleted =
                    new TaskCompletionSource<bool>(
                        TaskCreationOptions.RunContinuationsAsynchronously);

                coreWebView2.NavigationCompleted +=
                    (_, args) =>
                    {
                        navigationCompleted.TrySetResult(
                            args.IsSuccess);
                    };

                string content =
                    $$"""
                    <p>Content Security Policy runtime verification</p>
                    <img
                        src="{{server.ProbeUri}}"
                        alt="External resource probe" />
                    """;

                string html =
                    MarkdownDocumentContentRenderer.WrapHtmlDocument(
                        content,
                        allowExternalResources: false);

                // Act
                // Navigate directly to the production-generated HTML shell.
                // No production WebView2 event handlers are registered.
                webView.NavigateToString(html);

                bool navigationSucceeded =
                    await navigationCompleted.Task.WaitAsync(
                        BrowserEventTimeout);

                await WaitForDocumentReadyAsync(coreWebView2);

                string contentSecurityPolicy =
                    await coreWebView2.ExecuteScriptAsync(
                        "document.querySelector('meta[http-equiv=\"Content-Security-Policy\"]').content");

                // Allow any browser-initiated resource request associated with
                // the completed document load to reach the local test listener.
                await Task.Delay(
                    TimeSpan.FromMilliseconds(500));

                // Assert
                Assert.True(navigationSucceeded);

                Assert.False(
                    coreWebView2.Settings.IsScriptEnabled);

                Assert.Contains(
                    "img-src 'none'",
                    contentSecurityPolicy);

                Assert.False(
                    server.RequestReceived.IsCompleted);
            });
    }

    [Fact]
    public Task ConfigureSecurityAndNavigate_HandlesNewWindowsAndExternalUriSchemes()
    {
        return RunOnStaThreadAsync(
            async form =>
            {
                // Arrange: exercise new-window handling in the complete
                // production security configuration.
                var renderer = CreateRenderer(
                    allowRawHtml: false,
                    allowExternalResources: false,
                    allowExternalNavigation: false);

                WebView2 popupWebView =
                    await CreateInitializedWebViewAsync(form);

                CoreWebView2 popupCoreWebView2 =
                    popupWebView.CoreWebView2;

                const string html =
                    """
                <!DOCTYPE html>
                <html>
                <head><meta charset="utf-8" /></head>
                <body>
                    <p>WebView2 external-action verification</p>
                </body>
                </html>
                """;

                var newWindow =
                    new TaskCompletionSource<bool>(
                        TaskCreationOptions.RunContinuationsAsynchronously);

                renderer.ConfigureSecurityAndNavigate(
                    popupWebView,
                    html);

                popupCoreWebView2.NewWindowRequested +=
                    (_, args) =>
                    {
                        bool handledByRenderer =
                            args.Handled;

                        // Safety fallback: never open a test popup.
                        args.Handled = true;

                        newWindow.TrySetResult(
                            handledByRenderer);
                    };

                await WaitForDocumentReadyAsync(
                    popupCoreWebView2);

                // Act: request a new window from the real browser.
                await popupCoreWebView2.ExecuteScriptAsync(
                    "window.open('https://127.0.0.1:9/dv-runtime-popup-probe', '_blank');");

                bool popupHandledByRenderer =
                    await newWindow.Task.WaitAsync(
                        BrowserEventTimeout);

                // Arrange: use a second real WebView2 instance without
                // the earlier NavigationStarting policy handler.
                // This exercises the actual production external-scheme
                // cancellation callback.
                WebView2 schemeWebView =
                    await CreateInitializedWebViewAsync(form);

                CoreWebView2 schemeCoreWebView2 =
                    schemeWebView.CoreWebView2;

                MarkdownDocumentContentRenderer
                    .ConfigureExternalUriSchemeProtection(
                        schemeCoreWebView2);

                var externalScheme =
                    new TaskCompletionSource<
                        (string Uri, bool CancelledByRenderer)>(
                        TaskCreationOptions.RunContinuationsAsynchronously);

                var navigationCompleted =
                    new TaskCompletionSource<bool>(
                        TaskCreationOptions.RunContinuationsAsynchronously);

                // Subscribe after the production cancellation handler,
                // so the observer sees its decision.
                schemeCoreWebView2.LaunchingExternalUriScheme +=
                    (_, args) =>
                    {
                        bool cancelledByRenderer =
                            args.Cancel;

                        // Safety fallback: prevent an OS launch if the
                        // production cancellation handler regresses.
                        args.Cancel = true;

                        externalScheme.TrySetResult(
                            (
                                args.Uri,
                                cancelledByRenderer
                            ));
                    };

                schemeCoreWebView2.NavigationCompleted +=
                    (_, args) =>
                    {
                        navigationCompleted.TrySetResult(
                            !args.IsSuccess);
                    };

                // Act: initiate the registered URI scheme.
                schemeCoreWebView2.Navigate(
                    "ms-settings:privacy");

                (string Uri, bool CancelledByRenderer) schemeResult =
                    await externalScheme.Task.WaitAsync(
                        BrowserEventTimeout);

                bool navigationWasCancelled =
                    await navigationCompleted.Task.WaitAsync(
                        BrowserEventTimeout);

                // Assert
                Assert.True(
                    popupHandledByRenderer);

                Assert.Equal(
                    "ms-settings:privacy",
                    schemeResult.Uri);

                Assert.True(
                    schemeResult.CancelledByRenderer);

                Assert.True(
                    navigationWasCancelled);
            });
    }

    private static MarkdownDocumentContentRenderer CreateRenderer(
        bool allowRawHtml,
        bool allowExternalResources,
        bool allowExternalNavigation)
    {
        return new MarkdownDocumentContentRenderer(
            Options.Create(
                new MarkdownRenderingOptions
                {
                    AllowRawHtml = allowRawHtml,
                    AllowExternalResources = allowExternalResources,
                    AllowExternalNavigation = allowExternalNavigation
                }),
            new DocumentTextExtractorResolver(
                [
                    new MarkdownDocumentTextExtractor()
                ]));
    }

    private static async Task<WebView2> CreateInitializedWebViewAsync(
        Form form)
    {
        var webView = new WebView2
        {
            Dock = DockStyle.Fill
        };

        form.Controls.Add(webView);

        await webView.EnsureCoreWebView2Async();

        return webView;
    }

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
            "The WebView2 document did not reach the expected rendered state. "
            + $"Source: '{coreWebView2.Source}'. "
            + $"Last readiness result: '{lastReadinessResult}'.");
    }

    private static Task RunOnStaThreadAsync(
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
            });

        thread.IsBackground = true;
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        return completion.Task.WaitAsync(
            TimeSpan.FromSeconds(60));
    }

    private sealed record NavigationObservation(
        string Uri,
        ulong NavigationId,
        bool IsRedirected,
        bool IsUserInitiated,
        bool CancelledByRenderer);

    private sealed class LocalProbeServer : IAsyncDisposable
    {
        private readonly TcpListener _listener;

        private readonly CancellationTokenSource _shutdown = new();

        private readonly TaskCompletionSource<bool> _requestReceived =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly Task _serverTask;

        public string ProbeUri { get; }

        public Task<bool> RequestReceived =>
            _requestReceived.Task;

        private LocalProbeServer()
        {
            _listener =
                new TcpListener(
                    IPAddress.Loopback,
                    0);

            _listener.Start();

            int port =
                ((IPEndPoint)_listener.LocalEndpoint).Port;

            ProbeUri =
                $"http://127.0.0.1:{port}/csp-image-probe";

            _serverTask = ServeAsync();
        }

        public static LocalProbeServer Start()
        {
            return new LocalProbeServer();
        }

        private async Task ServeAsync()
        {
            try
            {
                while (true)
                {
                    using TcpClient client =
                        await _listener.AcceptTcpClientAsync(
                            _shutdown.Token);

                    await using NetworkStream stream =
                        client.GetStream();

                    string? request =
                        await ReadRequestAsync(
                            stream,
                            _shutdown.Token);

                    // Ignore abandoned or incomplete browser connections.
                    if (request is null)
                    {
                        continue;
                    }

                    _requestReceived.TrySetResult(true);

                    // Return a harmless response if the browser does make
                    // the request. The test will then fail on RequestReceived.
                    byte[] response =
                        Encoding.ASCII.GetBytes(
                            "HTTP/1.1 204 No Content\r\n"
                            + "Content-Length: 0\r\n"
                            + "Connection: close\r\n"
                            + "\r\n");

                    try
                    {
                        await stream.WriteAsync(
                            response,
                            _shutdown.Token);

                        await stream.FlushAsync(
                            _shutdown.Token);
                    }
                    catch (IOException)
                        when (!_shutdown.IsCancellationRequested)
                    {
                        // The browser may close the connection after the
                        // request has already been recorded.
                    }
                    catch (SocketException)
                        when (!_shutdown.IsCancellationRequested)
                    {
                        // The request has already been recorded.
                    }

                    return;
                }
            }
            catch (OperationCanceledException)
                when (_shutdown.IsCancellationRequested)
            {
                // The test owns and stops this local-only listener.
            }
            catch (SocketException)
                when (_shutdown.IsCancellationRequested)
            {
                // Stopping TcpListener interrupts a pending accept.
            }
        }

        private static async Task<string?> ReadRequestAsync(
            NetworkStream stream,
            CancellationToken cancellationToken)
        {
            using var request = new MemoryStream();

            byte[] buffer = new byte[1024];

            const int MaximumHeaderBytes = 16 * 1024;

            try
            {
                while (request.Length < MaximumHeaderBytes)
                {
                    int bytesRead =
                        await stream.ReadAsync(
                            buffer.AsMemory(),
                            cancellationToken);

                    if (bytesRead == 0)
                    {
                        return null;
                    }

                    request.Write(
                        buffer,
                        0,
                        bytesRead);

                    string received =
                        Encoding.ASCII.GetString(
                            request.GetBuffer(),
                            0,
                            (int)request.Length);

                    if (received.Contains(
                            "\r\n\r\n",
                            StringComparison.Ordinal))
                    {
                        return received;
                    }
                }
            }
            catch (IOException)
                when (!cancellationToken.IsCancellationRequested)
            {
                return null;
            }
            catch (SocketException)
                when (!cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            return null;
        }

        public async ValueTask DisposeAsync()
        {
            _shutdown.Cancel();

            _listener.Stop();

            try
            {
                await _serverTask;
            }
            finally
            {
                _shutdown.Dispose();
            }
        }
    }

    private sealed class LocalRedirectTestServer : IAsyncDisposable
    {
        private readonly TcpListener _listener;

        private readonly CancellationTokenSource _shutdown = new();

        private readonly TaskCompletionSource<bool> _finalRequestReceived =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly Task _serverTask;

        public string Origin { get; }

        public string RedirectUri { get; }

        public string FinalUri { get; }

        public Task FinalRequestReceived =>
            _finalRequestReceived.Task;

        private LocalRedirectTestServer()
        {
            _listener =
                new TcpListener(
                    IPAddress.Loopback,
                    0);

            _listener.Start();

            int port =
                ((IPEndPoint)_listener.LocalEndpoint).Port;

            Origin = $"http://127.0.0.1:{port}";

            RedirectUri = $"{Origin}/redirect";

            FinalUri = $"{Origin}/final";

            _serverTask = ServeAsync();
        }

        public static LocalRedirectTestServer Start()
        {
            return new LocalRedirectTestServer();
        }

        private async Task ServeAsync()
        {
            try
            {
                while (true)
                {
                    using TcpClient client =
                        await _listener.AcceptTcpClientAsync(
                            _shutdown.Token);

                    await using NetworkStream stream =
                        client.GetStream();

                    string? request =
                        await ReadRequestAsync(
                            stream,
                            _shutdown.Token);

                    // WebView2 may open and abandon a connection before sending
                    // complete HTTP headers. Ignore that connection and keep listening.
                    if (request is null)
                    {
                        continue;
                    }

                    string requestLine =
                        request.Split(
                            "\r\n",
                            2,
                            StringSplitOptions.None)[0];

                    string[] requestParts =
                        requestLine.Split(
                            ' ',
                            StringSplitOptions.RemoveEmptyEntries);

                    if (requestParts.Length < 2)
                    {
                        continue;
                    }

                    string requestPath =
                        requestParts[1].Split(
                            '?',
                            2,
                            StringSplitOptions.None)[0];

                    if (string.Equals(
                            requestPath,
                            "/redirect",
                            StringComparison.Ordinal))
                    {
                        await WriteResponseAsync(
                            stream,
                            "302 Found",
                            $"Location: {FinalUri}\r\n",
                            Array.Empty<byte>(),
                            _shutdown.Token);
                    }
                    else if (string.Equals(
                                 requestPath,
                                 "/final",
                                 StringComparison.Ordinal))
                    {
                        byte[] body =
                            Encoding.UTF8.GetBytes(
                                """
                            <!DOCTYPE html>
                            <html>
                            <head><meta charset="utf-8" /></head>
                            <body>redirect-final</body>
                            </html>
                            """);

                        await WriteResponseAsync(
                            stream,
                            "200 OK",
                            "Content-Type: text/html; charset=utf-8\r\n",
                            body,
                            _shutdown.Token);

                        _finalRequestReceived.TrySetResult(true);
                    }
                    else
                    {
                        await WriteResponseAsync(
                            stream,
                            "404 Not Found",
                            "Content-Type: text/plain\r\n",
                            Array.Empty<byte>(),
                            _shutdown.Token);
                    }
                }
            }
            catch (OperationCanceledException)
                when (_shutdown.IsCancellationRequested)
            {
                // The test owns and stops this local-only listener.
            }
            catch (SocketException)
                when (_shutdown.IsCancellationRequested)
            {
                // Stopping TcpListener interrupts a pending accept.
            }
        }

        private static async Task<string?> ReadRequestAsync(
            NetworkStream stream,
            CancellationToken cancellationToken)
        {
            using var request = new MemoryStream();

            byte[] buffer = new byte[1024];

            const int MaximumHeaderBytes = 16 * 1024;

            while (request.Length < MaximumHeaderBytes)
            {
                int bytesRead =
                    await stream.ReadAsync(
                        buffer.AsMemory(),
                        cancellationToken);

                if (bytesRead == 0)
                {
                    // The peer closed the connection before completing headers.
                    return null;
                }

                request.Write(
                    buffer,
                    0,
                    bytesRead);

                string received =
                    Encoding.ASCII.GetString(
                        request.GetBuffer(),
                        0,
                        (int)request.Length);

                if (received.Contains(
                        "\r\n\r\n",
                        StringComparison.Ordinal))
                {
                    return received;
                }
            }

            // Ignore an incomplete or oversized header block. The test's
            // FinalRequestReceived timeout remains the actual success gate.
            return null;
        }

        private static async Task WriteResponseAsync(
            NetworkStream stream,
            string status,
            string additionalHeaders,
            byte[] body,
            CancellationToken cancellationToken)
        {
            string headers =
                $"HTTP/1.1 {status}\r\n"
                + additionalHeaders
                + $"Content-Length: {body.Length}\r\n"
                + "Connection: close\r\n"
                + "\r\n";

            byte[] headerBytes =
                Encoding.ASCII.GetBytes(headers);

            await stream.WriteAsync(
                headerBytes,
                cancellationToken);

            if (body.Length > 0)
            {
                await stream.WriteAsync(
                    body,
                    cancellationToken);
            }

            await stream.FlushAsync(
                cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            _shutdown.Cancel();

            _listener.Stop();

            await _serverTask;

            _shutdown.Dispose();
        }
    }
}
