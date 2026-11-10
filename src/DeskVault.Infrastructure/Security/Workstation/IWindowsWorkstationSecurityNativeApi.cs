namespace DeskVault.Infrastructure.Security.Workstation;

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
