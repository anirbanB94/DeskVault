using DeskVault.Application.Interfaces;
using DeskVault.Application.Security.Sessions;
using WinFormsApplication = global::System.Windows.Forms.Application;

namespace DeskVault.UI.Services.Security;

/// <summary>
/// Captures deliberate WinForms input and reports it against the
/// authenticated session revision observed by this message filter.
/// </summary>
/// <remarks>
/// The filter deliberately excludes passive pointer movement, paint,
/// timer, and other background messages.
///
/// Start and Stop must be called on the WinForms UI thread. Session
/// notifications may arrive on another thread; they only update the
/// cached session revision and never interact with UI controls.
/// </remarks>
public sealed class WinFormsUserActivityMessageFilter :
    IMessageFilter,
    IDisposable
{
    private const int WmSysKeyDown = 0x0104;
    private const int WmKeyDown = 0x0100;
    private const int WmImeKeyDown = 0x0290;

    private const int WmGesture = 0x0119;
    private const int WmTouch = 0x0240;
    private const int WmPointerDown = 0x0246;

    private const int WmLeftButtonDown = 0x0201;
    private const int WmRightButtonDown = 0x0204;
    private const int WmMiddleButtonDown = 0x0207;
    private const int WmMouseWheel = 0x020A;
    private const int WmXButtonDown = 0x020B;
    private const int WmHorizontalMouseWheel = 0x020E;

    private readonly object _syncRoot = new();

    private readonly IApplicationSecuritySession _session;
    private readonly IApplicationUserActivityService _activityService;

    private long _activeSessionRevision = -1;
    private bool _started;
    private bool _disposed;

    public WinFormsUserActivityMessageFilter(
        IApplicationSecuritySession session,
        IApplicationUserActivityService activityService)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(activityService);

        _session = session;
        _activityService = activityService;
    }

    /// <summary>
    /// Registers the message filter and begins tracking the current
    /// authenticated session revision.
    /// </summary>
    public void Start()
    {
        lock (_syncRoot)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_started)
            {
                return;
            }

            _session.StateChanged += OnSessionStateChanged;
            _started = true;

            ObserveCurrentSession();

            try
            {
                WinFormsApplication.AddMessageFilter(this);
            }
            catch
            {
                _session.StateChanged -= OnSessionStateChanged;
                _started = false;
                Interlocked.Exchange(
                    ref _activeSessionRevision,
                    -1);

                throw;
            }
        }
    }

    /// <summary>
    /// Removes the message filter and stops reporting activity.
    /// </summary>
    public void Stop()
    {
        lock (_syncRoot)
        {
            if (!_started)
            {
                return;
            }

            _started = false;

            Interlocked.Exchange(
                ref _activeSessionRevision,
                -1);

            _session.StateChanged -= OnSessionStateChanged;

            WinFormsApplication.RemoveMessageFilter(this);
        }
    }

    /// <inheritdoc />
    public bool PreFilterMessage(ref Message message)
    {
        if (!IsQualifyingInputMessage(message.Msg))
        {
            return false;
        }

        long observedRevision =
            Interlocked.Read(ref _activeSessionRevision);

        if (observedRevision >= 0)
        {
            // Do not consume the message. The normal WinForms control
            // processing must still receive the user's input.
            _activityService.TryReportQualifyingActivity(
                observedRevision);
        }

        return false;
    }

    /// <summary>
    /// Releases the message-filter registration and session subscription.
    /// </summary>
    public void Dispose()
    {
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            if (_started)
            {
                _started = false;

                Interlocked.Exchange(
                    ref _activeSessionRevision,
                    -1);

                _session.StateChanged -= OnSessionStateChanged;

                WinFormsApplication.RemoveMessageFilter(this);
            }

            _disposed = true;
        }
    }

    private void OnSessionStateChanged(
        ApplicationSecuritySessionSnapshot _)
    {
        lock (_syncRoot)
        {
            if (!_started || _disposed)
            {
                return;
            }

            // Notifications can be delayed behind another dispatcher.
            // Reconcile against the latest authoritative snapshot rather
            // than trusting the revision carried by this notification.
            ObserveCurrentSession();
        }
    }

    private void ObserveCurrentSession()
    {
        ApplicationSecuritySessionSnapshot current =
            _session.CurrentSnapshot;

        Interlocked.Exchange(
            ref _activeSessionRevision,
            current.IsProtectedAccessAvailable
                ? current.Revision
                : -1);
    }

    private static bool IsQualifyingInputMessage(int messageId)
    {
        return messageId switch
        {
            WmKeyDown or
            WmSysKeyDown or
            WmImeKeyDown or

            WmLeftButtonDown or
            WmRightButtonDown or
            WmMiddleButtonDown or
            WmXButtonDown or
            WmMouseWheel or
            WmHorizontalMouseWheel or

            WmTouch or
            WmPointerDown or
            WmGesture => true,

            _ => false
        };
    }
}
