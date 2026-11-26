using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace DeskVault.UI.Rendering.HtmlDocumentRendering;

/// <summary>
/// Hosts one HTML preview and owns its WebView2 security policy
/// and isolated profile session.
/// </summary>
/// <remarks>
/// The profile session owns the WebView2 control after successful
/// initialization. The security policy is disposed before the profile
/// session. Cleanup is asynchronous and is marshalled to the owning
/// WinForms UI thread when necessary.
/// </remarks>
internal sealed class HtmlDocumentPreviewSurface : Panel, IAsyncDisposable
{
    private readonly WebView2 _webView;

    private readonly ILogger<HtmlDocumentPreviewSurface> _logger;

    private readonly int _uiThreadId;

    private readonly SynchronizationContext _uiSynchronizationContext;

    private readonly Lock _disposalSyncRoot = new();

    private readonly CancellationTokenSource _surfaceLifetimeCancellation = new();

    private HtmlDocumentWebView2ProfileSession? _profileSession;

    private HtmlDocumentWebView2SecurityPolicy? _securityPolicy;

    private Task? _initializationTask;

    private Task? _navigationCleanupTask;

    private Task? _disposalTask;

    private TaskCompletionSource? _navigationCleanupCompletion;

    private bool _initializationStarted;

    private bool _surfaceDisposed;

    private bool _cleanupCompleted;

    /// <summary>
    /// Initializes a preview surface with an uninitialized WebView2 control.
    /// </summary>
    /// <param name="logger">The logger used to report asynchronous cleanup failures.</param>
    public HtmlDocumentPreviewSurface(
        ILogger<HtmlDocumentPreviewSurface> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
        _uiThreadId = Environment.CurrentManagedThreadId;

        _uiSynchronizationContext =
            SynchronizationContext.Current
            ?? new WindowsFormsSynchronizationContext();

        Dock = DockStyle.Fill;

        _webView = new WebView2
        {
            DefaultBackgroundColor = Color.White,
            BackColor = Color.White,
            Dock = DockStyle.Fill
        };

        Controls.Add(_webView);
    }

    /// <summary>
    /// Initializes the dedicated profile and applies the HTML security
    /// policy before any document navigation can occur.
    /// </summary>
    /// <param name="profileSessionFactory">The dedicated profile-session factory.</param>
    /// <param name="cancellationToken">The initialization cancellation token.</param>
    /// <returns>A task representing profile initialization.</returns>
    public Task InitializeAsync(
        IHtmlDocumentWebView2ProfileSessionFactory profileSessionFactory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profileSessionFactory);

        VerifyOwningUiThread();

        var completion =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        lock (_disposalSyncRoot)
        {
            ObjectDisposedException.ThrowIf(
                _disposalTask is not null,
                this);

            if (_initializationStarted)
            {
                throw new InvalidOperationException(
                    "The HTML preview surface has already been initialized.");
            }

            _initializationStarted = true;
            _initializationTask = completion.Task;
        }

        _ = InitializeCoreAsync(
            profileSessionFactory,
            completion,
            cancellationToken);

        return completion.Task;
    }

    /// <summary>
    /// Initializes the WebView2 profile and applies the dedicated security policy.
    /// </summary>
    private async Task InitializeCoreAsync(
        IHtmlDocumentWebView2ProfileSessionFactory profileSessionFactory,
        TaskCompletionSource completion,
        CancellationToken cancellationToken)
    {
        try
        {
            using var linkedCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    _surfaceLifetimeCancellation.Token);

            linkedCancellation.Token.ThrowIfCancellationRequested();

            HtmlDocumentWebView2ProfileSession profileSession =
                await profileSessionFactory.CreateAsync(
                    _webView,
                    linkedCancellation.Token);

            _profileSession = profileSession;

            ThrowIfDisposalStarted();

            linkedCancellation.Token.ThrowIfCancellationRequested();

            CoreWebView2 coreWebView2 =
                _webView.CoreWebView2
                ?? throw new InvalidOperationException(
                    "HTML preview profile initialization did not create CoreWebView2.");

            _securityPolicy =
                new HtmlDocumentWebView2SecurityPolicy(
                    coreWebView2);

            completion.TrySetResult();
        }
        catch (OperationCanceledException exception)
        {
            completion.TrySetCanceled(
                exception.CancellationToken);
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }
    }

    /// <summary>
    /// Loads the complete host-composed HTML document after authorizing
    /// the single initial navigation through the dedicated security policy.
    /// </summary>
    /// <param name="html">The complete bounded HTML document.</param>
    /// <param name="cancellationToken">The navigation cancellation token.</param>
    /// <returns>A task representing initial document navigation.</returns>
    public async Task NavigateAsync(
        string html,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(html);

        VerifyOwningUiThread();
        ThrowIfDisposalStarted();

        HtmlDocumentWebView2SecurityPolicy securityPolicy =
            _securityPolicy
            ?? throw new InvalidOperationException(
                "The HTML preview security policy has not been initialized.");

        CoreWebView2 coreWebView2 =
            _webView.CoreWebView2
            ?? throw new InvalidOperationException(
                "The HTML preview WebView2 control has not been initialized.");

        using var linkedCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _surfaceLifetimeCancellation.Token);

        CancellationToken operationToken =
            linkedCancellation.Token;

        operationToken.ThrowIfCancellationRequested();

        var navigationCompletion =
            new TaskCompletionSource<CoreWebView2NavigationCompletedEventArgs>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var navigationCleanupCompletion =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        _navigationCleanupCompletion =
            navigationCleanupCompletion;

        _navigationCleanupTask =
            navigationCleanupCompletion.Task;

        void OnNavigationCompleted(
            object? sender,
            CoreWebView2NavigationCompletedEventArgs args)
        {
            navigationCompletion.TrySetResult(args);
        }

        coreWebView2.NavigationCompleted +=
            OnNavigationCompleted;

        using CancellationTokenRegistration cancellationRegistration =
            operationToken.Register(
                () => navigationCompletion.TrySetCanceled(operationToken));

        try
        {
            operationToken.ThrowIfCancellationRequested();

            securityPolicy.AuthorizeInitialDocumentNavigation();

            _webView.NavigateToString(html);

            CoreWebView2NavigationCompletedEventArgs navigationResult =
                await navigationCompletion.Task;

            operationToken.ThrowIfCancellationRequested();

            if (!navigationResult.IsSuccess)
            {
                throw new InvalidOperationException(
                    "WebView2 could not complete the HTML preview navigation. "
                    + $"Web error: {navigationResult.WebErrorStatus}.");
            }
        }
        finally
        {
            coreWebView2.NavigationCompleted -=
                OnNavigationCompleted;

            _navigationCleanupCompletion = null;
            _navigationCleanupTask = null;

            navigationCleanupCompletion.TrySetResult();
        }
    }

    /// <summary>
    /// Releases the security policy, profile session and hosted controls.
    /// </summary>
    /// <returns>A task representing complete preview-resource cleanup.</returns>
    public ValueTask DisposeAsync()
    {
        return new ValueTask(
            StartDisposal());
    }

    /// <summary>
    /// Starts asynchronous resource cleanup when the containing WinForms
    /// hierarchy disposes the surface synchronously.
    /// </summary>
    protected override void Dispose(
        bool disposing)
    {
        if (!disposing)
        {
            base.Dispose(false);
            return;
        }

        _ = StartDisposal();
    }

    /// <summary>
    /// Starts or joins the active cleanup attempt.
    /// A failed profile cleanup can be retried through DisposeAsync.
    /// </summary>
    private Task StartDisposal()
    {
        TaskCompletionSource completion;
        Task disposalTask;
        bool executeOnOwningThread;

        lock (_disposalSyncRoot)
        {
            if (_cleanupCompleted)
            {
                return Task.CompletedTask;
            }

            if (_disposalTask is { IsCompleted: false })
            {
                return _disposalTask;
            }

            completion =
                new TaskCompletionSource(
                    TaskCreationOptions.RunContinuationsAsynchronously);

            disposalTask =
                completion.Task;

            _disposalTask =
                disposalTask;

            executeOnOwningThread =
                Environment.CurrentManagedThreadId == _uiThreadId;
        }

        _surfaceLifetimeCancellation.Cancel();

        if (executeOnOwningThread)
        {
            _ = DisposeCoreAsync(completion);
        }
        else
        {
            _uiSynchronizationContext.Post(
                _ => _ = DisposeCoreAsync(completion),
                null);
        }

        _ = disposalTask.ContinueWith(
            static task =>
            {
                _ = task.Exception;
            },
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted
            | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        return disposalTask;
    }

    /// <summary>
    /// Disposes the security policy before the profile session and
    /// attempts all remaining cleanup operations before reporting failures.
    /// </summary>
    private async Task DisposeCoreAsync(
        TaskCompletionSource completion)
    {
        var failures =
            new List<Exception>();

        Task? initializationTask;

        lock (_disposalSyncRoot)
        {
            initializationTask =
                _initializationTask;
        }

        if (initializationTask is not null)
        {
            try
            {
                await initializationTask;
            }
            catch
            {
                // Initialization reports its failure to its caller.
                // Disposal must still release any partially initialized resources.
            }
        }

        Task? navigationCleanupTask =
            _navigationCleanupTask;

        if (navigationCleanupTask is not null)
        {
            try
            {
                await navigationCleanupTask;
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        if (!_surfaceDisposed)
        {
            try
            {
                Controls.Remove(_webView);
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        if (_securityPolicy is not null)
        {
            try
            {
                _securityPolicy.Dispose();
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
            finally
            {
                _securityPolicy = null;
            }
        }

        if (_profileSession is not null)
        {
            try
            {
                await _profileSession.DisposeAsync();

                _profileSession = null;
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }
        else if (!_webView.IsDisposed)
        {
            try
            {
                _webView.Dispose();
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        if (!_surfaceDisposed)
        {
            try
            {
                base.Dispose(true);
                _surfaceDisposed = true;
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        if (failures.Count > 0)
        {
            var cleanupException =
                new AggregateException(
                    "HTML preview resource cleanup did not complete.",
                    failures);

            _logger.LogError(
                cleanupException,
                "HTML preview resource cleanup failed.");

            completion.TrySetException(
                cleanupException);

            return;
        }

        _surfaceLifetimeCancellation.Dispose();

        lock (_disposalSyncRoot)
        {
            _cleanupCompleted = true;
        }

        completion.TrySetResult();
    }

    /// <summary>
    /// Verifies that the operation is running on the surface's owning UI thread.
    /// </summary>
    private void VerifyOwningUiThread()
    {
        if (Environment.CurrentManagedThreadId != _uiThreadId)
        {
            throw new InvalidOperationException(
                "HTML preview operations must run on the owning WinForms UI thread.");
        }
    }

    /// <summary>
    /// Prevents initialization or navigation after resource cleanup has begun.
    /// </summary>
    private void ThrowIfDisposalStarted()
    {
        lock (_disposalSyncRoot)
        {
            ObjectDisposedException.ThrowIf(
                _disposalTask is not null,
                this);
        }
    }
}
