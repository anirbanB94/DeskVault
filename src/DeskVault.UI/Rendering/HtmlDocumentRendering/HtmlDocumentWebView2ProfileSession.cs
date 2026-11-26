using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace DeskVault.UI.Rendering.HtmlDocumentRendering;

/// <summary>
/// Owns one isolated WebView2 profile and its user-data folder for an HTML
/// preview session.
/// </summary>
/// <remarks>
/// Each instance receives a unique user-data folder and an InPrivate profile.
/// InPrivate mode does not eliminate the on-disk user-data folder; cleanup
/// therefore clears browsing data, closes the WebView2 control, waits for
/// browser processes to exit, and removes the unique folder.
///
/// This type owns the supplied WebView2 control. Creation and disposal must
/// run on the control's owning WinForms UI thread. The HTML security policy
/// must be disposed before this session.
/// </remarks>
internal sealed class HtmlDocumentWebView2ProfileSession : IAsyncDisposable
{
    private const string HtmlPreviewProfileName =
        "DeskVaultHtmlPreview";

    private static readonly TimeSpan BrowserProcessExitTimeout =
        TimeSpan.FromSeconds(20);

    private readonly WebView2 _webView;

    private readonly string _userDataFolderPath;

    private CoreWebView2Environment? _environment;

    private bool _browsingDataCleanupAttempted;

    private bool _webViewDisposed;

    private bool _browserProcessesExited;

    private bool _userDataFolderDeleted;

    private bool _cleanupCompleted;

    /// <summary>
    /// Signals that WebView2 has exited the complete browser-process collection
    /// and released the resources associated with this user-data folder.
    /// </summary>
    private readonly TaskCompletionSource<bool> _browserProcessExitedSignal =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private bool _browserProcessExitHandlerAttached;

    private bool _webViewInitialized;

    /// <summary>
    /// Gets the WebView2 control owned by this preview session.
    /// </summary>
    internal WebView2 WebView => _webView;

    /// <summary>
    /// Gets the unique user-data folder assigned to this preview session.
    /// </summary>
    /// <remarks>
    /// This path is exposed internally for profile verification and runtime
    /// tests. It must not be presented to document content or included in
    /// user-facing diagnostics.
    /// </remarks>
    internal string UserDataFolderPath => _userDataFolderPath;

    private HtmlDocumentWebView2ProfileSession(
        WebView2 webView,
        string userDataFolderPath)
    {
        _webView = webView;
        _userDataFolderPath = userDataFolderPath;
    }

    /// <summary>
    /// Creates and initializes a WebView2 control with a unique InPrivate
    /// profile and dedicated user-data folder.
    /// </summary>
    /// <param name="webView">
    /// An uninitialized WebView2 control transferred to this session.
    /// </param>
    /// <param name="userDataRoot">
    /// Optional parent folder used to contain unique preview user-data
    /// folders. The production default is a DeskVault-specific folder under
    /// the current user's temporary directory.
    /// </param>
    /// <param name="cancellationToken">
    /// A token used to stop the creation operation before or after WebView2
    /// initialization. WebView2 initialization itself is not cancellable.
    /// </param>
    /// <returns>
    /// An initialized profile session that owns the supplied WebView2 control.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="webView"/> is null.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the supplied WebView2 control has already been initialized
    /// or the runtime does not establish the requested profile configuration.
    /// </exception>
    /// <exception cref="AggregateException">
    /// Thrown when initialization fails and cleanup also fails.
    /// </exception>
    internal static async Task<HtmlDocumentWebView2ProfileSession> CreateAsync(
        WebView2 webView,
        string? userDataRoot = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(webView);

        if (webView.CoreWebView2 is not null)
        {
            throw new InvalidOperationException(
                "The HTML preview WebView2 control must be uninitialized.");
        }

        string resolvedUserDataRoot =
            Path.GetFullPath(
                userDataRoot
                ?? Path.Combine(
                    Path.GetTempPath(),
                    "DeskVault",
                    "HtmlPreviewProfiles"));

        string userDataFolderPath =
            Path.Combine(
                resolvedUserDataRoot,
                Guid.NewGuid().ToString("N"));

        var session =
            new HtmlDocumentWebView2ProfileSession(
                webView,
                userDataFolderPath);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            Directory.CreateDirectory(
                resolvedUserDataRoot);

            // Each preview gets a distinct environment and UDF. This prevents
            // the preview from sharing the default or Markdown WebView2 profile.
            session._environment =
                await CoreWebView2Environment.CreateAsync(
                    null,
                    session._userDataFolderPath);

            // Subscribe before initializing the control so the browser-exit event
            // cannot be missed during the preview session's lifetime.
            session._environment.BrowserProcessExited +=
                session.OnBrowserProcessExited;

            session._browserProcessExitHandlerAttached = true;

            cancellationToken.ThrowIfCancellationRequested();

            CoreWebView2ControllerOptions controllerOptions =
                session._environment.CreateCoreWebView2ControllerOptions();

            controllerOptions.ProfileName =
                HtmlPreviewProfileName;

            controllerOptions.IsInPrivateModeEnabled = true;

            await webView.EnsureCoreWebView2Async(
                session._environment,
                controllerOptions);

            session._webViewInitialized = true;

            cancellationToken.ThrowIfCancellationRequested();

            CoreWebView2 coreWebView2 =
                webView.CoreWebView2
                ?? throw new InvalidOperationException(
                    "WebView2 initialization completed without a CoreWebView2 instance.");

            coreWebView2.Profile.PreferredColorScheme =
                CoreWebView2PreferredColorScheme.Light;

            // Verify the actual runtime configuration rather than assuming
            // the requested controller options were applied.
            if (!coreWebView2.Profile.IsInPrivateModeEnabled)
            {
                throw new InvalidOperationException(
                    "The HTML preview profile is not operating in InPrivate mode.");
            }

            if (!PathsEqual(
                    session._userDataFolderPath,
                    session._environment.UserDataFolder))
            {
                throw new InvalidOperationException(
                    "WebView2 initialized with an unexpected user-data folder.");
            }

            if (!Directory.Exists(session._userDataFolderPath))
            {
                throw new InvalidOperationException(
                    "The HTML preview user-data folder was not created.");
            }

            return session;
        }
        catch (Exception initializationException)
        {
            try
            {
                await session.DisposeAsync();
            }
            catch (Exception cleanupException)
            {
                throw new AggregateException(
                    "HTML preview initialization and profile cleanup both failed.",
                    initializationException,
                    cleanupException);
            }

            throw;
        }
    }

    /// <summary>
    /// Clears browsing data, releases the WebView2 control, waits for all
    /// associated browser processes, and deletes the session's unique
    /// user-data folder.
    /// </summary>
    /// <remarks>
    /// The operation is retryable when browser-process shutdown or physical
    /// folder deletion has not completed. A cleanup failure is surfaced to
    /// the caller and is never silently treated as successful disposal.
    ///
    /// Invoke this method on the owning WebView2 UI thread. Dispose the
    /// HTML-specific WebView2 security policy before disposing this session.
    /// </remarks>
    /// <exception cref="HtmlDocumentWebView2ProfileCleanupException">
    /// Thrown when any required cleanup operation fails.
    /// </exception>
    public async ValueTask DisposeAsync()
    {
        if (_cleanupCompleted)
        {
            return;
        }

        var failures =
            new List<(
                HtmlDocumentWebView2ProfileCleanupStage Stage,
                Exception Exception)>();

        await ClearBrowsingDataAsync(
            failures);

        DisposeWebView(
            failures);

        await WaitForBrowserProcessesToExitAsync(
            failures);

        DeleteUserDataFolder(
            failures);

        _cleanupCompleted =
            _webViewDisposed
            && _browserProcessesExited
            && _userDataFolderDeleted;

        if (failures.Count > 0)
        {
            HtmlDocumentWebView2ProfileCleanupStage stage =
                failures.Count == 1
                    ? failures[0].Stage
                    : HtmlDocumentWebView2ProfileCleanupStage.Multiple;

            throw new HtmlDocumentWebView2ProfileCleanupException(
                stage,
                new AggregateException(
                    failures.Select(failure => failure.Exception)));
        }

        if (!_cleanupCompleted)
        {
            throw new HtmlDocumentWebView2ProfileCleanupException(
                HtmlDocumentWebView2ProfileCleanupStage.IncompleteCleanup,
                new InvalidOperationException(
                    "The profile cleanup postconditions were not satisfied."));
        }
    }

    /// <summary>
    /// Clears all browsing data associated with the preview profile.
    /// </summary>
    /// <param name="failures">
    /// Receives cleanup errors without preventing later teardown attempts.
    /// </param>
    private async Task ClearBrowsingDataAsync(
        List<(
            HtmlDocumentWebView2ProfileCleanupStage Stage,
            Exception Exception)> failures)
    {
        if (_browsingDataCleanupAttempted)
        {
            return;
        }

        _browsingDataCleanupAttempted = true;

        CoreWebView2? coreWebView2 =
            _webView.CoreWebView2;

        if (coreWebView2 is null)
        {
            return;
        }

        try
        {
            // Stop any remaining navigation or resource activity before
            // asking the profile to clear its browser-managed data.
            coreWebView2.Stop();
        }
        catch (Exception exception)
        {
            failures.Add(
                (
                    HtmlDocumentWebView2ProfileCleanupStage.StopWebView,
                    exception
                ));
        }

        try
        {
            await coreWebView2.Profile.ClearBrowsingDataAsync();
        }
        catch (Exception exception)
        {
            failures.Add(
                (
                    HtmlDocumentWebView2ProfileCleanupStage.ClearBrowsingData,
                    exception
                ));
        }
    }

    /// <summary>
    /// Disposes the owned WebView2 control.
    /// </summary>
    /// <param name="failures">
    /// Receives control-disposal errors for reporting after the remaining
    /// safe cleanup operations have been attempted.
    /// </param>
    private void DisposeWebView(
        List<(
            HtmlDocumentWebView2ProfileCleanupStage Stage,
            Exception Exception)> failures)
    {
        if (_webViewDisposed)
        {
            return;
        }

        try
        {
            _webView.Dispose();
            _webViewDisposed = true;
        }
        catch (Exception exception)
        {
            failures.Add(
                (
                    HtmlDocumentWebView2ProfileCleanupStage.DisposeWebView,
                    exception
                ));
        }
    }

    /// <summary>
    /// Waits for WebView2 to report that the complete browser-process collection
    /// has exited and released its resources for the preview user-data folder.
    /// </summary>
    /// <param name="failures">
    /// Receives process-shutdown failures for reporting after other safe
    /// cleanup operations have been attempted.
    /// </param>
    private async Task WaitForBrowserProcessesToExitAsync(
        List<(
            HtmlDocumentWebView2ProfileCleanupStage Stage,
            Exception Exception)> failures)
    {
        if (_browserProcessesExited)
        {
            return;
        }

        if (_environment is null)
        {
            // No environment was created, so no WebView2 process can own
            // this session's user-data folder.
            _browserProcessesExited = true;
            return;
        }

        try
        {
            if (!_webViewInitialized
                && _environment.GetProcessInfos().Count == 0)
            {
                // Initialization did not complete and the environment
                // never started a browser process. There is no process
                // collection whose termination must be awaited.
                _browserProcessesExited = true;
            }
            else
            {
                // An empty process-info snapshot is not sufficient evidence
                // of complete resource release. BrowserProcessExited is the
                // authoritative WebView2 lifecycle signal.
                await _browserProcessExitedSignal.Task.WaitAsync(
                    BrowserProcessExitTimeout);

                _browserProcessesExited = true;
            }

            if (_browserProcessExitHandlerAttached)
            {
                _environment.BrowserProcessExited -=
                    OnBrowserProcessExited;

                _browserProcessExitHandlerAttached = false;
            }
        }
        catch (Exception exception)
        {
            failures.Add(
                (
                    HtmlDocumentWebView2ProfileCleanupStage.WaitForBrowserProcesses,
                    exception
                ));
        }
    }

    /// <summary>
    /// Deletes the unique preview user-data folder after its WebView2
    /// processes have exited.
    /// </summary>
    /// <param name="failures">
    /// Receives physical deletion and postcondition-verification failures.
    /// </param>
    private void DeleteUserDataFolder(
        List<(
            HtmlDocumentWebView2ProfileCleanupStage Stage,
            Exception Exception)> failures)
    {
        if (_userDataFolderDeleted || !_browserProcessesExited)
        {
            return;
        }

        try
        {
            if (Directory.Exists(_userDataFolderPath))
            {
                Directory.Delete(
                    _userDataFolderPath,
                    recursive: true);
            }

            // Directory.Delete is not considered successful until the
            // physical directory is confirmed absent.
            _userDataFolderDeleted =
                !Directory.Exists(_userDataFolderPath);

            if (!_userDataFolderDeleted)
            {
                throw new IOException(
                    "The HTML preview user-data folder still exists after deletion.");
            }
        }
        catch (Exception exception)
        {
            failures.Add(
                (
                    HtmlDocumentWebView2ProfileCleanupStage.DeleteUserDataFolder,
                    exception
                ));
        }
    }

    /// <summary>
    /// Compares two normalized user-data folder paths using Windows path
    /// comparison semantics.
    /// </summary>
    private static bool PathsEqual(
        string left,
        string right)
    {
        return string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Signals completion of the WebView2 browser-process lifecycle after
    /// the runtime has released the profile's associated resources.
    /// </summary>
    /// <param name="sender">The WebView2 environment raising the event.</param>
    /// <param name="args">The browser-process exit details.</param>
    private void OnBrowserProcessExited(
        object? sender,
        CoreWebView2BrowserProcessExitedEventArgs args)
    {
        _browserProcessExitedSignal.TrySetResult(true);
    }
}

/// <summary>
/// Identifies the cleanup operation that failed for an HTML preview profile.
/// </summary>
internal enum HtmlDocumentWebView2ProfileCleanupStage
{
    /// <summary>
    /// Stopping the WebView2 instance failed.
    /// </summary>
    StopWebView,

    /// <summary>
    /// Clearing profile browsing data failed.
    /// </summary>
    ClearBrowsingData,

    /// <summary>
    /// Disposing the WebView2 control failed.
    /// </summary>
    DisposeWebView,

    /// <summary>
    /// Waiting for the environment's browser processes failed.
    /// </summary>
    WaitForBrowserProcesses,

    /// <summary>
    /// Deleting or verifying deletion of the user-data folder failed.
    /// </summary>
    DeleteUserDataFolder,

    /// <summary>
    /// More than one cleanup operation failed.
    /// </summary>
    Multiple,

    /// <summary>
    /// One or more cleanup postconditions were not satisfied.
    /// </summary>
    IncompleteCleanup
}

/// <summary>
/// Represents an observable failure to complete HTML preview profile cleanup.
/// </summary>
internal sealed class HtmlDocumentWebView2ProfileCleanupException
    : InvalidOperationException
{
    /// <summary>
    /// Initializes a cleanup exception for the specified failure stage.
    /// </summary>
    /// <param name="stage">The cleanup operation that failed.</param>
    /// <param name="innerException">The underlying cleanup failure.</param>
    internal HtmlDocumentWebView2ProfileCleanupException(
        HtmlDocumentWebView2ProfileCleanupStage stage,
        Exception innerException)
        : base(
            $"HTML preview profile cleanup did not complete at stage '{stage}'.",
            innerException)
    {
        Stage = stage;
    }

    /// <summary>
    /// Gets the cleanup operation that failed.
    /// </summary>
    internal HtmlDocumentWebView2ProfileCleanupStage Stage { get; }
}
