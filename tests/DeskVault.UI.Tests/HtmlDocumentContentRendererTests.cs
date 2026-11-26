using DeskVault.UI.Rendering;
using DeskVault.UI.Rendering.HtmlDocumentRendering;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Text;
using System.Text.Json;
using WindowsForms = System.Windows.Forms;

namespace DeskVault.UI.Tests;

/// <summary>
/// Verifies HTML document renderer selection, bounded failure handling,
/// source-stream ownership, and runtime presentation.
/// </summary>
public sealed class HtmlDocumentContentRendererTests
{
    /// <summary>
    /// Verifies that HTML and HTM extensions resolve to the HTML renderer
    /// without treating unrelated formats as HTML.
    /// </summary>
    [Theory]
    [InlineData("document.html")]
    [InlineData("document.htm")]
    [InlineData("DOCUMENT.HTML")]
    [InlineData("DOCUMENT.HTM")]
    public void CanRender_ReturnsTrueForSupportedHtmlExtensions(
        string fileName)
    {
        // Arrange
        HtmlDocumentContentRenderer renderer =
            CreateRenderer();

        // Act
        bool canRender =
            renderer.CanRender(fileName);

        // Assert
        Assert.True(canRender);
    }

    /// <summary>
    /// Verifies that unrelated document formats are not selected by
    /// the HTML renderer.
    /// </summary>
    [Theory]
    [InlineData("document.txt")]
    [InlineData("document.md")]
    [InlineData("document.csv")]
    [InlineData("document.pdf")]
    [InlineData("document")]
    public void CanRender_ReturnsFalseForUnsupportedExtensions(
        string fileName)
    {
        // Arrange
        HtmlDocumentContentRenderer renderer =
            CreateRenderer();

        // Act
        bool canRender =
            renderer.CanRender(fileName);

        // Assert
        Assert.False(canRender);
    }

    /// <summary>
    /// Verifies that source input larger than the configured limit is
    /// rejected before the presentation host is modified.
    /// </summary>
    [Fact]
    public async Task RenderAsync_WhenSourceExceedsLimit_RejectsWithoutChangingHost()
    {
        // Arrange
        HtmlDocumentContentRenderer renderer =
            CreateRenderer();

        using var contentHost = new Panel();

        byte[] oversizedContent =
            Encoding.UTF8.GetBytes(
                new string(
                    'A',
                    HtmlDocumentContentPayloadBuilder.MaximumSourceBytes + 1));

        using var documentStream =
            new MemoryStream(oversizedContent);

        // Act
        await Assert.ThrowsAsync<InvalidDataException>(
            () => renderer.RenderAsync(
                contentHost,
                documentStream,
                "oversized.html",
                TestContext.Current.CancellationToken));

        // Assert
        Assert.True(documentStream.CanRead);
        Assert.Empty(contentHost.Controls);
    }

    /// <summary>
    /// Verifies that cancellation requested before rendering does not
    /// modify the presentation host or dispose the caller-owned stream.
    /// </summary>
    [Fact]
    public async Task RenderAsync_WhenAlreadyCancelled_LeavesHostAndSourceStreamUnchanged()
    {
        // Arrange
        HtmlDocumentContentRenderer renderer =
            CreateRenderer();

        using var contentHost = new Panel();

        using var documentStream =
            new MemoryStream(
                Encoding.UTF8.GetBytes(
                    "<!DOCTYPE html><html><body>Cancelled</body></html>"));

        using var cancellationSource =
            new CancellationTokenSource();

        await cancellationSource.CancelAsync();

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => renderer.RenderAsync(
                contentHost,
                documentStream,
                "cancelled.html",
                cancellationSource.Token));

        // Assert
        Assert.True(documentStream.CanRead);
        Assert.Empty(contentHost.Controls);
    }

    /// <summary>
    /// Verifies that supported static HTML is rendered in the existing
    /// host using the dedicated InPrivate profile and light color scheme,
    /// and that disposing the renderer-owned surface physically removes
    /// its profile directory.
    /// </summary>
    [Fact]
    public Task RenderAsync_RendersOfflineHtmlUsingDedicatedProfile()
    {
        return RunOnStaThreadAsync(
            async form =>
            {
                // Arrange
                var contentHost = new Panel
                {
                    Dock = DockStyle.Fill
                };

                form.Controls.Add(contentHost);

                var profileSessionFactory =
                    new CapturingHtmlDocumentWebView2ProfileSessionFactory();

                HtmlDocumentContentRenderer renderer =
                    CreateRenderer(profileSessionFactory);

                const string marker =
                    "DESKVAULT_HTML_RENDERER_RUNTIME_MARKER";

                string embeddedImageDataUri =
                    CreateEmbeddedPngDataUri();

                string html =
                    $$"""
                    <!DOCTYPE html>
                    <html>
                    <head>
                        <meta charset="utf-8" />
                        <title>DeskVault HTML preview test</title>
                        <style>
                            body { font-family: sans-serif; }
                            h1 { color: rgb(1, 2, 3); }
                        </style>
                    </head>
                    <body>
                        <h1>{{marker}}</h1>
                        <p>Offline static HTML preview.</p>
                        <img
                            id="embedded-image"
                            alt="Embedded PNG fixture"
                            src="{{embeddedImageDataUri}}" />
                    </body>
                    </html>
                    """;

                using var documentStream =
                    new MemoryStream(
                        Encoding.UTF8.GetBytes(html));

                // Act
                await renderer.RenderAsync(
                    contentHost,
                    documentStream,
                    "runtime-preview.html");

                // Assert: the caller still owns the source stream.
                Assert.True(documentStream.CanRead);

                // Assert: rendering created the dedicated preview surface
                // inside the existing document content host.
                HtmlDocumentPreviewSurface surface =
                    Assert.IsType<HtmlDocumentPreviewSurface>(
                        Assert.Single(contentHost.Controls));

                WebView2 webView =
                    FindWebView(surface);

                Assert.NotNull(webView.CoreWebView2);

                string profileFolderPath =
                    Assert.IsType<string>(
                        profileSessionFactory.UserDataFolderPath);

                Assert.True(
                    Directory.Exists(profileFolderPath));

                // Assert: the runtime uses the exact isolated profile
                // created by the injected profile-session factory.
                Assert.Equal(
                    Path.GetFullPath(profileFolderPath),
                    Path.GetFullPath(
                        webView.CoreWebView2.Environment.UserDataFolder),
                    ignoreCase: true);

                // Assert: the HTML preview uses InPrivate mode.
                Assert.True(
                    webView.CoreWebView2.Profile.IsInPrivateModeEnabled);

                // Assert: the dedicated HTML preview profile explicitly
                // uses the light color scheme rather than inheriting
                // the operating-system preference.
                Assert.Equal(
                    CoreWebView2PreferredColorScheme.Light,
                    webView.CoreWebView2.Profile.PreferredColorScheme);

                // Assert: the #404 browser policy has disabled document scripting.
                Assert.False(
                    webView.CoreWebView2.Settings.IsScriptEnabled);

                // Assert: static structure and inline styling were rendered.
                string renderedText =
                    await webView.CoreWebView2.ExecuteScriptAsync(
                        "document.body.innerText");

                Assert.Contains(
                    marker,
                    renderedText,
                    StringComparison.Ordinal);

                // Assert: inspect the browser's effective document color settings.
                string pageColorDiagnostics =
                    await webView.CoreWebView2.ExecuteScriptAsync(
                        """
                        JSON.stringify({
                            preferredDarkScheme:
                                window.matchMedia("(prefers-color-scheme: dark)").matches,
                            declaredColorScheme:
                                document.querySelector("meta[name='color-scheme']")?.content ?? null,
                            computedColorScheme:
                                getComputedStyle(document.documentElement).colorScheme,
                            htmlBackground:
                                getComputedStyle(document.documentElement).backgroundColor,
                            bodyBackground:
                                getComputedStyle(document.body).backgroundColor,
                            htmlColor:
                                getComputedStyle(document.documentElement).color,
                            bodyColor:
                                getComputedStyle(document.body).color
                        })
                        """);

                // Assert: capture the actual rendered WebView2 surface.
                using var previewImageStream =
                    new MemoryStream();

                await webView.CoreWebView2.CapturePreviewAsync(
                    CoreWebView2CapturePreviewImageFormat.Png,
                    previewImageStream);

                previewImageStream.Position = 0;

                using var previewBitmap =
                    new Bitmap(previewImageStream);

                Assert.True(previewBitmap.Width > 10);
                Assert.True(previewBitmap.Height > 10);

                Color topLeftPixel =
                    previewBitmap.GetPixel(5, 5);

                Color centerPixel =
                    previewBitmap.GetPixel(
                        previewBitmap.Width / 2,
                        previewBitmap.Height / 2);

                Color canvasPixel =
                    previewBitmap.GetPixel(
                        previewBitmap.Width - 5,
                        previewBitmap.Height - 5);

                static string FormatColor(Color color) =>
                    $"#{color.R:X2}{color.G:X2}{color.B:X2}";

                string colorDiagnostics =
                    $"Page CSS: {pageColorDiagnostics}; "
                    + $"PreferredColorScheme: "
                    + $"{webView.CoreWebView2.Profile.PreferredColorScheme}; "
                    + $"WebView2 DefaultBackgroundColor: "
                    + $"{FormatColor(webView.DefaultBackgroundColor)}; "
                    + $"WebView BackColor: {FormatColor(webView.BackColor)}; "
                    + $"Surface BackColor: {FormatColor(surface.BackColor)}; "
                    + $"ContentHost BackColor: {FormatColor(contentHost.BackColor)}; "
                    + $"WebView2 Runtime: "
                    + $"{webView.CoreWebView2.Environment.BrowserVersionString}; "
                    + $"Screenshot pixels: "
                    + $"top-left={FormatColor(topLeftPixel)}, "
                    + $"center={FormatColor(centerPixel)}, "
                    + $"bottom-right={FormatColor(canvasPixel)}.";

                Assert.True(
                    canvasPixel.ToArgb() == Color.White.ToArgb(),
                    "Expected the HTML preview canvas to be white. "
                    + colorDiagnostics);

                string computedHeadingColor =
                    await webView.CoreWebView2.ExecuteScriptAsync(
                        "getComputedStyle(document.querySelector('h1')).color");

                Assert.Equal(
                    "\"rgb(1, 2, 3)\"",
                    computedHeadingColor);

                // Assert: the approved embedded PNG is decoded and rendered
                // by the real WebView2 runtime, not merely retained in markup.
                bool embeddedImageCompleted = false;
                int embeddedImageWidth = 0;
                int embeddedImageHeight = 0;

                for (int attempt = 0; attempt < 40; attempt++)
                {
                    string imageStateJson =
                        await webView.CoreWebView2.ExecuteScriptAsync(
                            """
                            (() => {
                                const image = document.querySelector("#embedded-image");
                                return {
                                    complete: image?.complete ?? false,
                                    naturalWidth: image?.naturalWidth ?? 0,
                                    naturalHeight: image?.naturalHeight ?? 0
                                };
                            })()
                            """);

                    using JsonDocument imageState =
                        JsonDocument.Parse(imageStateJson);

                    JsonElement imageStateRoot =
                        imageState.RootElement;

                    embeddedImageCompleted =
                        imageStateRoot.GetProperty("complete").GetBoolean();

                    embeddedImageWidth =
                        imageStateRoot.GetProperty("naturalWidth").GetInt32();

                    embeddedImageHeight =
                        imageStateRoot.GetProperty("naturalHeight").GetInt32();

                    if (embeddedImageCompleted
                        && embeddedImageWidth > 0
                        && embeddedImageHeight > 0)
                    {
                        break;
                    }

                    await Task.Delay(50);
                }

                Assert.True(embeddedImageCompleted);
                Assert.Equal(2, embeddedImageWidth);
                Assert.Equal(2, embeddedImageHeight);

                // Assert: NavigateToString did not create a separate
                // plaintext HTML document under the dedicated profile.
                Assert.Empty(
                    Directory.EnumerateFiles(
                        profileFolderPath,
                        "*.html",
                        SearchOption.AllDirectories));

                // Act: dispose through the renderer-owned preview surface.
                await surface.DisposeAsync();

                // Assert: cleanup completed physical profile-directory deletion.
                Assert.False(
                    Directory.Exists(profileFolderPath));
            });
    }

    /// <summary>
    /// Verifies that the synchronous document-host clear path used by
    /// DocumentViewForm starts renderer cleanup and that the in-flight
    /// profile cleanup can be awaited to its physical deletion postcondition.
    /// </summary>
    [Fact]
    public Task DocumentContentHostClear_DisposesNestedHtmlPreviewAndDeletesProfileDirectory()
    {
        return RunOnStaThreadAsync(
            async form =>
            {
                // Arrange
                var contentHost = new Panel
                {
                    Dock = DockStyle.Fill
                };

                var renderHost = new Panel
                {
                    Dock = DockStyle.Fill
                };

                form.Controls.Add(contentHost);
                contentHost.Controls.Add(renderHost);

                var profileSessionFactory =
                    new CapturingHtmlDocumentWebView2ProfileSessionFactory();

                HtmlDocumentContentRenderer renderer =
                    CreateRenderer(profileSessionFactory);

                const string html =
                    """
                    <!DOCTYPE html>
                    <html>
                    <head><meta charset="utf-8" /></head>
                    <body><p>Host-clear lifecycle marker.</p></body>
                    </html>
                    """;

                using var documentStream =
                    new MemoryStream(Encoding.UTF8.GetBytes(html));

                // Act: render through the nested control structure used by
                // DocumentViewForm, then use its synchronous host-clear path.
                await renderer.RenderAsync(
                    renderHost,
                    documentStream,
                    "host-clear-lifecycle.html");

                HtmlDocumentPreviewSurface surface =
                    Assert.IsType<HtmlDocumentPreviewSurface>(
                        Assert.Single(renderHost.Controls));

                string profileFolderPath =
                    Assert.IsType<string>(
                        profileSessionFactory.UserDataFolderPath);

                Assert.True(Directory.Exists(profileFolderPath));

                DocumentContentHost.Clear(contentHost);

                // The synchronous Control.Dispose path initiates cleanup.
                // Keep the WinForms message loop active while its asynchronous
                // browser-process and profile cleanup completes in the background.
                await WaitForProfileCleanupCompletionAsync(
                    surface,
                    profileFolderPath,
                    TimeSpan.FromSeconds(30));

                // Assert: the nested host and actual WebView2 surface are disposed,
                // and profile deletion completed physically without a direct await
                // from the production presentation-clear caller.
                Assert.True(renderHost.IsDisposed);
                Assert.True(surface.IsDisposed);
                Assert.Empty(contentHost.Controls);
                Assert.False(Directory.Exists(profileFolderPath));
                Assert.True(documentStream.CanRead);
            });
    }

    /// <summary>
    /// Verifies that cancellation immediately after dedicated profile
    /// initialization is propagated and the renderer awaits profile cleanup.
    /// </summary>
    [Fact]
    public Task RenderAsync_WhenCancelledAfterProfileInitialization_CleansProfileAndPreservesSourceStream()
    {
        return RunOnStaThreadAsync(
            async form =>
            {
                // Arrange
                var contentHost = new Panel
                {
                    Dock = DockStyle.Fill
                };

                form.Controls.Add(contentHost);

                using var cancellationSource =
                    new CancellationTokenSource();

                var profileSessionFactory =
                    new CapturingHtmlDocumentWebView2ProfileSessionFactory(
                        _ => cancellationSource.Cancel());

                HtmlDocumentContentRenderer renderer =
                    CreateRenderer(profileSessionFactory);

                const string html =
                    """
                    <!DOCTYPE html>
                    <html>
                    <head>
                        <meta charset="utf-8" />
                    </head>
                    <body>
                        <p>Cancellation during HTML preview initialization.</p>
                    </body>
                    </html>
                    """;

                using var documentStream =
                    new MemoryStream(
                        Encoding.UTF8.GetBytes(html));

                // Act
                await Assert.ThrowsAnyAsync<OperationCanceledException>(
                    () => renderer.RenderAsync(
                        contentHost,
                        documentStream,
                        "cancelled-after-initialization.html",
                        cancellationSource.Token));

                // Assert: the source remains caller-owned.
                Assert.True(documentStream.CanRead);

                // Assert: the factory created a real profile session before
                // cancellation was requested.
                string profileFolderPath =
                    Assert.IsType<string>(
                        profileSessionFactory.UserDataFolderPath);

                // Assert: the renderer awaited cleanup instead of returning
                // while its initialized profile remained on disk.
                Assert.False(
                    Directory.Exists(profileFolderPath));

                Assert.Empty(contentHost.Controls);
            });
    }

    private static async Task WaitForProfileCleanupCompletionAsync(
        HtmlDocumentPreviewSurface surface,
        string profileFolderPath,
        TimeSpan timeout)
    {
        DateTime deadline =
            DateTime.UtcNow + timeout;

        while ((Directory.Exists(profileFolderPath) || !surface.IsDisposed)
               && DateTime.UtcNow < deadline)
        {
            // Awaiting keeps the WinForms message loop responsive so the
            // surface's UI-thread cleanup continuation can complete.
            await Task.Delay(50);
        }

        if (Directory.Exists(profileFolderPath) || !surface.IsDisposed)
        {
            throw new TimeoutException(
                "The HTML preview profile cleanup did not complete after the document host was cleared.");
        }
    }

    private static string CreateEmbeddedPngDataUri()
    {
        using var bitmap =
            new Bitmap(2, 2);

        bitmap.SetPixel(0, 0, Color.Red);
        bitmap.SetPixel(1, 0, Color.Green);
        bitmap.SetPixel(0, 1, Color.Blue);
        bitmap.SetPixel(1, 1, Color.Black);

        using var imageStream =
            new MemoryStream();

        bitmap.Save(
            imageStream,
            System.Drawing.Imaging.ImageFormat.Png);

        return "data:image/png;base64,"
            + Convert.ToBase64String(imageStream.ToArray());
    }

    private static HtmlDocumentContentRenderer CreateRenderer(
        IHtmlDocumentWebView2ProfileSessionFactory? profileSessionFactory = null)
    {
        return new HtmlDocumentContentRenderer(
            profileSessionFactory
                ?? new HtmlDocumentWebView2ProfileSessionFactory(),
            NullLogger<HtmlDocumentPreviewSurface>.Instance);
    }

    private static WebView2 FindWebView(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            if (child is WebView2 webView)
            {
                return webView;
            }

            try
            {
                return FindWebView(child);
            }
            catch (InvalidOperationException)
            {
                // Continue searching sibling controls.
            }
        }

        throw new InvalidOperationException(
            "The HTML preview surface does not contain a WebView2 control.");
    }

    /// <summary>
    /// Runs a test body on an STA thread with a WinForms message loop.
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
    /// Captures the unique user-data folder created by a real HTML preview
    /// profile session and optionally executes a callback after initialization.
    /// </summary>
    private sealed class CapturingHtmlDocumentWebView2ProfileSessionFactory
        : IHtmlDocumentWebView2ProfileSessionFactory
    {
        private readonly Action<string>? _afterSessionCreated;

        internal CapturingHtmlDocumentWebView2ProfileSessionFactory(
            Action<string>? afterSessionCreated = null)
        {
            _afterSessionCreated = afterSessionCreated;
        }

        internal string? UserDataFolderPath { get; private set; }

        public async Task<HtmlDocumentWebView2ProfileSession> CreateAsync(
            WebView2 webView,
            CancellationToken cancellationToken)
        {
            HtmlDocumentWebView2ProfileSession profileSession =
                await HtmlDocumentWebView2ProfileSession.CreateAsync(
                    webView,
                    cancellationToken: cancellationToken);

            string userDataFolderPath =
                profileSession.UserDataFolderPath;

            UserDataFolderPath = userDataFolderPath;

            _afterSessionCreated?.Invoke(
                userDataFolderPath);

            return profileSession;
        }
    }
}
