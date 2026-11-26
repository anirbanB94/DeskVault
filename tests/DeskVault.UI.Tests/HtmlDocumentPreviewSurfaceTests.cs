using DeskVault.UI.Rendering.HtmlDocumentRendering;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.WinForms;
using Moq;
using WindowsForms = System.Windows.Forms;

namespace DeskVault.UI.Tests;

/// <summary>
/// Verifies HTML preview surface initialization and disposal coordination.
/// </summary>
public sealed class HtmlDocumentPreviewSurfaceTests
{
    /// <summary>
    /// Verifies that disposal waits for an in-progress profile initialization
    /// before releasing the WebView2 control.
    /// </summary>
    [Fact]
    public Task DisposeAsync_DuringInitialization_WaitsForInitialization()
    {
        return RunOnStaThreadAsync(
            async form =>
            {
                // Arrange
                var surface =
                    new HtmlDocumentPreviewSurface(
                        Mock.Of<ILogger<HtmlDocumentPreviewSurface>>());

                form.Controls.Add(surface);

                WebView2 webView =
                    Assert.IsType<WebView2>(
                        Assert.Single(surface.Controls));

                var profileSessionFactory =
                    new DelayedProfileSessionFactory();

                Task initializationTask =
                    surface.InitializeAsync(
                        profileSessionFactory,
                        CancellationToken.None);

                // Act
                Task disposalTask =
                    surface.DisposeAsync().AsTask();

                bool wasDisposedBeforeInitializationCompleted =
                    webView.IsDisposed;

                profileSessionFactory.Fail(
                    new InvalidOperationException(
                        "Simulated profile initialization failure."));

                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => initializationTask);

                await disposalTask;

                // Assert
                Assert.False(
                    wasDisposedBeforeInitializationCompleted);

                Assert.True(webView.IsDisposed);
                Assert.True(surface.IsDisposed);
            });
    }

    /// <summary>
    /// Defers profile creation so disposal can be requested while
    /// initialization is still pending.
    /// </summary>
    private sealed class DelayedProfileSessionFactory
        : IHtmlDocumentWebView2ProfileSessionFactory
    {
        private readonly TaskCompletionSource<HtmlDocumentWebView2ProfileSession>
            _completion = new(
                TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<HtmlDocumentWebView2ProfileSession> CreateAsync(
            WebView2 webView,
            CancellationToken cancellationToken)
        {
            return _completion.Task;
        }

        internal void Fail(
            Exception exception)
        {
            _completion.TrySetException(exception);
        }
    }

    /// <summary>
    /// Executes a test on a dedicated STA thread with a WinForms message loop.
    /// </summary>
    private static Task<bool> RunOnStaThreadAsync(
        Func<WindowsForms.Form, Task> testBody)
    {
        var completion =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var thread = new Thread(
            () =>
            {
                try
                {
                    using var form =
                        new WindowsForms.Form
                        {
                            Width = 900,
                            Height = 650,
                            ShowInTaskbar = false,
                            StartPosition =
                                WindowsForms.FormStartPosition.Manual,
                            Location = new System.Drawing.Point(-2000, -2000)
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

        thread.SetApartmentState(
            ApartmentState.STA);

        thread.Start();

        return completion.Task.WaitAsync(
            TimeSpan.FromSeconds(60));
    }
}
