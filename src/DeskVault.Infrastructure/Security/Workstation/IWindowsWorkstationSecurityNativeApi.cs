using System.Runtime.InteropServices;

namespace DeskVault.Infrastructure.Security.Workstation;

/// <summary>
/// Receives native Windows suspend/resume notifications.
/// The callback must not allow managed exceptions to cross the native boundary.
/// </summary>
[UnmanagedFunctionPointer(
    CallingConvention.Winapi)]
internal delegate uint WindowsWorkstationPowerNotificationCallback(
    IntPtr context,
    uint notificationType,
    IntPtr setting);

/// <summary>
/// Isolates the native window and WTS registration boundary so workstation
/// notification behavior can be tested without interacting with the host OS.
/// </summary>
internal interface IWindowsWorkstationSecurityNativeApi
{
    IWindowsWorkstationSecurityNativeWindow CreateHiddenTopLevelWindow(
        Action<uint, nuint, nint> messageReceived);

    bool RegisterSessionNotification(
        IntPtr windowHandle,
        uint flags,
        out int errorCode);

    bool UnregisterSessionNotification(
        IntPtr windowHandle,
        out int errorCode);

    /// <summary>
    /// Gets the Windows session identifier associated with the current
    /// DeskVault process.
    /// </summary>
    bool TryGetProcessSessionId(
        out uint processSessionId,
        out int errorCode);

    /// <summary>
    /// Retrieves the WTSSessionInfoEx snapshot for the supplied session.
    /// The implementation owns and releases the native WTS response buffer.
    /// </summary>
    bool TryQuerySessionInformation(
        uint sessionId,
        out WindowsWorkstationSecuritySessionStateSnapshot snapshot,
        out int errorCode);

    /// <summary>
    /// Registers an independent callback for Windows suspend/resume events.
    /// The implementation must retain the callback until successful
    /// unregistration.
    /// </summary>
    bool TryRegisterSuspendResumeNotification(
        WindowsWorkstationPowerNotificationCallback callback,
        out IntPtr registrationHandle,
        out int errorCode);

    /// <summary>
    /// Unregisters a previously registered suspend/resume callback.
    /// A failed unregistration must not release the callback delegate.
    /// </summary>
    bool TryUnregisterSuspendResumeNotification(
        IntPtr registrationHandle,
        out int errorCode);

}

/// <summary>
/// Represents a hidden top-level native window and its owning message loop.
/// Creation, message-loop execution, and disposal occur on the same thread.
/// RequestStop may be called from another thread.
/// </summary>
internal interface IWindowsWorkstationSecurityNativeWindow : IDisposable
{
    IntPtr Handle { get; }

    void RunMessageLoop();

    void RequestStop();
}
