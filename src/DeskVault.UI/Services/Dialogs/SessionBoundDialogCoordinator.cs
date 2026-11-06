using DeskVault.Application.Interfaces;
using DeskVault.Application.Security.Sessions;
using DeskVault.UI.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace DeskVault.UI.Services.Dialogs;

/// <summary>
/// Owns the lifetime of modal UI forms that may display protected information.
/// </summary>
public sealed class SessionBoundDialogCoordinator :
    ISessionBoundDialogCoordinator,
    IDisposable
{
    private readonly object _syncRoot = new();
    private readonly IApplicationSecuritySession _applicationSecuritySession;
    private readonly ILogger<SessionBoundDialogCoordinator> _logger;
    private readonly List<DialogRegistration> _activeDialogs = [];

    private SynchronizationContext? _uiSynchronizationContext;
    private int _uiThreadId;
    private bool _isSubscribed;
    private bool _closePostScheduled;
    private int _disposed;

    public SessionBoundDialogCoordinator(
        IApplicationSecuritySession applicationSecuritySession,
        ILogger<SessionBoundDialogCoordinator> logger)
    {
        ArgumentNullException.ThrowIfNull(applicationSecuritySession);
        ArgumentNullException.ThrowIfNull(logger);

        _applicationSecuritySession = applicationSecuritySession;
        _logger = logger;
    }

    /// <inheritdoc />
    public DialogResult ShowDialog(
        Form dialog,
        long expectedSessionRevision,
        IWin32Window? owner = null)
    {
        ArgumentNullException.ThrowIfNull(dialog);

        EnsureOwningUiThread();

        ObjectDisposedException.ThrowIf(
            dialog.IsDisposed || dialog.Disposing,
            dialog);

        if (!TryRegister(dialog, expectedSessionRevision, out DialogRegistration registration))
        {
            return DialogResult.Cancel;
        }

        EventHandler shownHandler = (_, _) =>
            OnDialogShown(registration);

        dialog.Shown += shownHandler;

        try
        {
            // Close the registration/subscription race before entering the
            // modal message loop. OnDialogShown performs the same check after
            // WinForms has created and displayed the dialog handle.
            if (!IsRegistrationCurrent(registration))
            {
                registration.MarkRevoked();
                CloseDialog(registration);
                return DialogResult.Cancel;
            }

            DialogResult result = owner is null
                ? dialog.ShowDialog()
                : dialog.ShowDialog(owner);

            // The user may have accepted the dialog immediately before a lock
            // committed. Never return approval from an obsolete revision.
            return IsRegistrationCurrent(registration)
                ? result
                : DialogResult.Cancel;
        }
        finally
        {
            dialog.Shown -= shownHandler;
            Unregister(registration);
        }
    }

    public void Dispose()
    {
        DialogRegistration[] registrations;
        SynchronizationContext? synchronizationContext;
        bool closeOnCurrentThread;

        lock (_syncRoot)
        {
            if (_disposed != 0)
            {
                return;
            }

            _disposed = 1;

            if (_isSubscribed)
            {
                _applicationSecuritySession.StateChanged -=
                    OnApplicationSecuritySessionStateChanged;

                _isSubscribed = false;
            }

            registrations = _activeDialogs.ToArray();

            foreach (DialogRegistration registration in registrations)
            {
                registration.MarkRevoked();
            }

            synchronizationContext = _uiSynchronizationContext;
            closeOnCurrentThread =
                _uiThreadId == Environment.CurrentManagedThreadId;
        }

        if (registrations.Length == 0)
        {
            return;
        }

        if (closeOnCurrentThread)
        {
            CloseRegistrations(registrations);
            return;
        }

        if (synchronizationContext is null)
        {
            _logger.LogError(
                "Session-bound dialogs could not be scheduled for closure during coordinator disposal because the UI synchronization context was unavailable.");

            return;
        }

        try
        {
            synchronizationContext.Post(
                _ => CloseRegistrations(registrations),
                null);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                "Session-bound dialog closure during coordinator disposal could not be scheduled. FailureType={FailureType}",
                exception.GetType().Name);
        }
    }

    private void EnsureOwningUiThread()
    {
        lock (_syncRoot)
        {
            ObjectDisposedException.ThrowIf(
                _disposed != 0,
                this);

            int currentThreadId =
                Environment.CurrentManagedThreadId;

            if (_uiThreadId == 0)
            {
                if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
                {
                    throw new InvalidOperationException(
                        "Session-bound dialogs must be displayed on a WinForms STA thread.");
                }

                _uiThreadId = currentThreadId;

                _uiSynchronizationContext =
                    SynchronizationContext.Current is WindowsFormsSynchronizationContext windowsFormsContext
                        ? windowsFormsContext
                        : new WindowsFormsSynchronizationContext();

                return;
            }

            if (_uiThreadId != currentThreadId)
            {
                throw new InvalidOperationException(
                    "All session-bound dialogs must be displayed on the coordinator's owning WinForms UI thread.");
            }
        }
    }

    private bool TryRegister(
        Form dialog,
        long expectedSessionRevision,
        out DialogRegistration registration)
    {
        registration = new DialogRegistration(
            dialog,
            expectedSessionRevision);

        lock (_syncRoot)
        {
            ObjectDisposedException.ThrowIf(
                _disposed != 0,
                this);

            ApplicationSecuritySessionSnapshot snapshot =
                _applicationSecuritySession.CurrentSnapshot;

            if (!IsAuthorizedRevision(
                    snapshot,
                    expectedSessionRevision))
            {
                return false;
            }

            if (_activeDialogs.Any(active =>
                    ReferenceEquals(active.Dialog, dialog)))
            {
                throw new InvalidOperationException(
                    "The same form cannot be registered as more than one active session-bound dialog.");
            }

            _activeDialogs.Add(registration);

            if (!_isSubscribed)
            {
                try
                {
                    _applicationSecuritySession.StateChanged +=
                        OnApplicationSecuritySessionStateChanged;

                    _isSubscribed = true;
                }
                catch
                {
                    _activeDialogs.Remove(registration);
                    throw;
                }
            }
        }

        // The session may have transitioned between the first snapshot read
        // and event subscription. A fresh authoritative read closes that gap.
        if (!IsSessionRevisionCurrent(expectedSessionRevision))
        {
            registration.MarkRevoked();
        }

        return true;
    }

    private void Unregister(
        DialogRegistration registration)
    {
        lock (_syncRoot)
        {
            _activeDialogs.Remove(registration);

            if (_activeDialogs.Count == 0 && _isSubscribed)
            {
                _applicationSecuritySession.StateChanged -=
                    OnApplicationSecuritySessionStateChanged;

                _isSubscribed = false;
                _closePostScheduled = false;
            }
        }
    }

    private void OnDialogShown(
        DialogRegistration registration)
    {
        if (IsRegistrationCurrent(registration))
        {
            return;
        }

        registration.MarkRevoked();
        CloseDialog(registration);
    }

    private void OnApplicationSecuritySessionStateChanged(
        ApplicationSecuritySessionSnapshot notification)
    {
        SynchronizationContext? synchronizationContext = null;
        bool closeOnCurrentThread = false;

        lock (_syncRoot)
        {
            if (_disposed != 0 || _activeDialogs.Count == 0)
            {
                return;
            }

            // Notifications can be older than the authoritative snapshot by
            // the time this consumer receives them. The payload is therefore
            // considered alongside a fresh CurrentSnapshot read.
            ApplicationSecuritySessionSnapshot currentSnapshot =
                _applicationSecuritySession.CurrentSnapshot;

            bool invalidatedAnyDialog = false;

            foreach (DialogRegistration registration in _activeDialogs)
            {
                if (!IsAuthorizedRevision(
                        currentSnapshot,
                        registration.ExpectedSessionRevision) ||
                    IsInvalidatingNotification(
                        notification,
                        registration.ExpectedSessionRevision))
                {
                    registration.MarkRevoked();
                    invalidatedAnyDialog = true;
                }
            }

            if (!invalidatedAnyDialog)
            {
                return;
            }

            if (_uiThreadId == Environment.CurrentManagedThreadId)
            {
                closeOnCurrentThread = true;
            }
            else if (!_closePostScheduled)
            {
                _closePostScheduled = true;
                synchronizationContext = _uiSynchronizationContext;
            }
        }

        if (closeOnCurrentThread)
        {
            CloseRevokedDialogs();
            return;
        }

        if (synchronizationContext is null)
        {
            return;
        }

        try
        {
            synchronizationContext.Post(
                _ => CloseRevokedDialogs(),
                null);
        }
        catch (Exception exception)
        {
            lock (_syncRoot)
            {
                _closePostScheduled = false;
            }

            _logger.LogError(
                "Session-bound dialog closure after session revocation could not be scheduled. FailureType={FailureType}",
                exception.GetType().Name);
        }
    }

    private void CloseRevokedDialogs()
    {
        DialogRegistration[] registrations;

        lock (_syncRoot)
        {
            _closePostScheduled = false;
            registrations = _activeDialogs.ToArray();
        }

        CloseInvalidatedRegistrations(registrations);
    }

    private void CloseRegistrations(
        IEnumerable<DialogRegistration> registrations)
    {
        foreach (DialogRegistration registration in registrations.Reverse())
        {
            CloseDialog(registration);
        }
    }

    private void CloseInvalidatedRegistrations(
        IEnumerable<DialogRegistration> registrations)
    {
        foreach (DialogRegistration registration in registrations.Reverse())
        {
            if (IsRegistrationCurrent(registration))
            {
                continue;
            }

            registration.MarkRevoked();
            CloseDialog(registration);
        }
    }

    private void CloseDialog(
        DialogRegistration registration)
    {
        if (!registration.TryRequestClose())
        {
            return;
        }

        Form dialog = registration.Dialog;

        if (dialog.IsDisposed || dialog.Disposing)
        {
            return;
        }

        try
        {
            // Assigning DialogResult closes a modal form and guarantees that
            // the caller receives a non-affirmative result.
            dialog.DialogResult = DialogResult.Cancel;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                "A session-bound dialog could not be closed after access was withdrawn. FailureType={FailureType}",
                exception.GetType().Name);
        }
    }

    private bool IsRegistrationCurrent(
        DialogRegistration registration)
    {
        if (Volatile.Read(ref _disposed) != 0 ||
            registration.IsRevoked)
        {
            return false;
        }

        if (IsSessionRevisionCurrent(registration.ExpectedSessionRevision))
        {
            return true;
        }

        registration.MarkRevoked();
        return false;
    }

    private bool IsSessionRevisionCurrent(
        long expectedSessionRevision)
    {
        ApplicationSecuritySessionSnapshot snapshot =
            _applicationSecuritySession.CurrentSnapshot;

        return IsAuthorizedRevision(
            snapshot,
            expectedSessionRevision);
    }

    private static bool IsAuthorizedRevision(
        ApplicationSecuritySessionSnapshot snapshot,
        long expectedSessionRevision)
    {
        return snapshot.IsProtectedAccessAvailable &&
               snapshot.Revision == expectedSessionRevision;
    }

    private static bool IsInvalidatingNotification(
        ApplicationSecuritySessionSnapshot notification,
        long expectedSessionRevision)
    {
        return notification.Revision > expectedSessionRevision ||
               (notification.Revision == expectedSessionRevision &&
                !notification.IsProtectedAccessAvailable);
    }

    private sealed class DialogRegistration
    {
        private int _revoked;
        private int _closeRequested;

        public DialogRegistration(
            Form dialog,
            long expectedSessionRevision)
        {
            Dialog = dialog;
            ExpectedSessionRevision = expectedSessionRevision;
        }

        public Form Dialog { get; }

        public long ExpectedSessionRevision { get; }

        public bool IsRevoked =>
            Volatile.Read(ref _revoked) != 0;

        public void MarkRevoked()
        {
            Interlocked.Exchange(
                ref _revoked,
                1);
        }

        public bool TryRequestClose()
        {
            return Interlocked.Exchange(
                ref _closeRequested,
                1) == 0;
        }
    }
}
