using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DeskVault.Infrastructure.Security.Workstation;

internal delegate bool WindowsWorkstationProcessSessionIdLookup(
    uint processId,
    out uint sessionId,
    out int errorCode);

internal delegate bool WindowsWorkstationWtsSessionInformationQuery(
    IntPtr serverHandle,
    uint sessionId,
    int informationClass,
    out IntPtr buffer,
    out uint bytesReturned);

internal delegate void WindowsWorkstationWtsMemoryFree(
    IntPtr memory);

/// <summary>
/// Implements the native Windows operations required by the workstation
/// security notification event source.
/// </summary>
internal sealed class WindowsWorkstationSecurityNativeApi
    : IWindowsWorkstationSecurityNativeApi
{
    private const uint WindowMessageQuit = 0x0012;
    private const uint WindowMessageClose = 0x0010;
    private const uint WindowMessageDestroy = 0x0002;
    private const uint WindowMessageNonClientDestroy = 0x0082;

    private const uint PeekMessageNoRemove = 0x0000;

    // WS_POPUP without WS_VISIBLE creates a hidden top-level window.
    private const uint WindowStylePopup = 0x80000000;

    private const int ErrorInvalidWindowHandle = 1400;

    private const int ErrorInvalidHandle = 6;

    private const int ErrorInvalidData = 13;

    private const int ErrorNotSupported = 50;

    private const int WtsSessionInfoExInformationClass = 25;

    private const uint DeviceNotifyCallback = 0x00000002;

    private readonly ConcurrentDictionary<
        IntPtr,
        WindowsWorkstationPowerNotificationCallback> _powerCallbacks = new();

    private readonly WindowsWorkstationProcessSessionIdLookup
        _processSessionIdLookup;

    private readonly WindowsWorkstationWtsSessionInformationQuery
        _wtsSessionInformationQuery;

    private readonly WindowsWorkstationWtsMemoryFree
        _wtsMemoryFree;

    private static long _windowClassSequence;

    internal WindowsWorkstationSecurityNativeApi()
        : this(
            TryGetProcessSessionIdNative,
            WtsQuerySessionInformationNative,
            WtsFreeMemoryNative)
    {
    }

    internal WindowsWorkstationSecurityNativeApi(
        WindowsWorkstationWtsSessionInformationQuery wtsSessionInformationQuery,
        WindowsWorkstationWtsMemoryFree wtsMemoryFree)
        : this(
            TryGetProcessSessionIdNative,
            wtsSessionInformationQuery,
            wtsMemoryFree)
    {
    }

    internal WindowsWorkstationSecurityNativeApi(
        WindowsWorkstationProcessSessionIdLookup processSessionIdLookup,
        WindowsWorkstationWtsSessionInformationQuery wtsSessionInformationQuery,
        WindowsWorkstationWtsMemoryFree wtsMemoryFree)
    {
        ArgumentNullException.ThrowIfNull(processSessionIdLookup);
        ArgumentNullException.ThrowIfNull(wtsSessionInformationQuery);
        ArgumentNullException.ThrowIfNull(wtsMemoryFree);

        _processSessionIdLookup = processSessionIdLookup;
        _wtsSessionInformationQuery = wtsSessionInformationQuery;
        _wtsMemoryFree = wtsMemoryFree;
    }

    /// <summary>
    /// Gets the native WTSINFOEXW structure size for the current process
    /// architecture and managed interop layout.
    /// </summary>
    internal static int WtsInfoExStructureSize =>
        Marshal.SizeOf<WtsInfoExNative>();

    /// <summary>
    /// Determines whether the current process architecture has a supported
    /// 64-bit Windows native ABI for workstation session reconciliation.
    /// </summary>
    /// <remarks>
    /// DeskVault supports x64 and ARM64 processes. An x64 process running
    /// under Windows ARM64 emulation still uses the x64 process ABI.
    /// Native ARM64 compatibility is independently verified by ARM64 CI.
    /// </remarks>
    internal static bool IsSupportedProcessArchitecture(
        Architecture processArchitecture,
        int pointerSize)
    {
        return pointerSize == sizeof(long) &&
            processArchitecture is Architecture.X64 or Architecture.Arm64;
    }

    /// <inheritdoc />
    public IWindowsWorkstationSecurityNativeWindow CreateHiddenTopLevelWindow(
        Action<uint, nuint, nint> messageReceived)
    {
        ArgumentNullException.ThrowIfNull(messageReceived);

        return new NativeWindow(messageReceived);
    }

    /// <inheritdoc />
    public bool RegisterSessionNotification(
        IntPtr windowHandle,
        uint flags,
        out int errorCode)
    {
        if (windowHandle == IntPtr.Zero)
        {
            errorCode = ErrorInvalidWindowHandle;
            return false;
        }

        bool succeeded =
            WtsRegisterSessionNotification(
                windowHandle,
                flags);

        errorCode =
            succeeded
                ? 0
                : Marshal.GetLastWin32Error();

        return succeeded;
    }

    /// <inheritdoc />
    public bool UnregisterSessionNotification(
        IntPtr windowHandle,
        out int errorCode)
    {
        if (windowHandle == IntPtr.Zero)
        {
            errorCode = ErrorInvalidWindowHandle;
            return false;
        }

        bool succeeded =
            WtsUnregisterSessionNotification(
                windowHandle);

        errorCode =
            succeeded
                ? 0
                : Marshal.GetLastWin32Error();

        return succeeded;
    }

    /// <inheritdoc />
    public bool TryGetProcessSessionId(
        out uint processSessionId,
        out int errorCode)
    {
        if (!IsSupportedProcessArchitecture(
                RuntimeInformation.ProcessArchitecture,
                IntPtr.Size))
        {
            processSessionId = 0;
            errorCode = ErrorNotSupported;
            return false;
        }

        uint processId =
            checked((uint)Environment.ProcessId);

        bool succeeded =
            _processSessionIdLookup(
                processId,
                out processSessionId,
                out errorCode);

        if (!succeeded)
        {
            processSessionId = 0;

            // A failed lookup without a native error is still an invalid
            // monitoring result. Do not surface Win32Exception(0).
            if (errorCode == 0)
            {
                errorCode = ErrorInvalidData;
            }

            return false;
        }

        errorCode = 0;
        return true;
    }

    /// <inheritdoc />
    public bool TryQuerySessionInformation(
        uint sessionId,
        out WindowsWorkstationSecuritySessionStateSnapshot snapshot,
        out int errorCode)
    {
        snapshot = default;

        // Initialize these before invoking the native boundary so that
        // a query delegate that throws after assigning a buffer still
        // receives the same unconditional cleanup.
        IntPtr buffer = IntPtr.Zero;
        uint bytesReturned = 0;

        try
        {
            bool succeeded =
                _wtsSessionInformationQuery(
                    IntPtr.Zero,
                    sessionId,
                    WtsSessionInfoExInformationClass,
                    out buffer,
                    out bytesReturned);

            if (!succeeded)
            {
                errorCode = Marshal.GetLastWin32Error();
                return false;
            }

            int expectedStructureSize =
                WtsInfoExStructureSize;

            // Never marshal a native structure from a buffer that is
            // smaller than the expected structure layout.
            if (buffer == IntPtr.Zero ||
                bytesReturned < (uint)expectedStructureSize)
            {
                errorCode = ErrorInvalidData;
                return false;
            }

            WtsInfoExNative nativeInformation =
                Marshal.PtrToStructure<WtsInfoExNative>(buffer);

            snapshot =
                new WindowsWorkstationSecuritySessionStateSnapshot(
                    Level: nativeInformation.Level,
                    StructureSize: bytesReturned,
                    SessionId: nativeInformation.Level1.SessionId,
                    ConnectionState: nativeInformation.Level1.SessionState,
                    SessionFlags: nativeInformation.Level1.SessionFlags);

            // A larger-than-expected response is deliberately returned
            // with its actual size. The validation parser rejects it.
            errorCode = 0;
            return true;
        }
        finally
        {
            // The WTS response belongs to this method. Release it on
            // success, native-query failure, malformed response, and
            // exceptions raised by the native query or marshalling.
            if (buffer != IntPtr.Zero)
            {
                _wtsMemoryFree(buffer);
            }
        }
    }

    /// <inheritdoc />
    public bool TryRegisterSuspendResumeNotification(
        WindowsWorkstationPowerNotificationCallback callback,
        out IntPtr registrationHandle,
        out int errorCode)
    {
        ArgumentNullException.ThrowIfNull(callback);

        var parameters =
            new DeviceNotifySubscribeParameters
            {
                Callback = callback,
                Context = IntPtr.Zero
            };

        uint nativeError =
            PowerRegisterSuspendResumeNotificationNative(
                DeviceNotifyCallback,
                ref parameters,
                out registrationHandle);

        if (nativeError != 0)
        {
            registrationHandle = IntPtr.Zero;
            errorCode = unchecked((int)nativeError);
            return false;
        }

        if (registrationHandle == IntPtr.Zero)
        {
            errorCode = ErrorInvalidData;
            return false;
        }

        // Keep the managed delegate rooted for as long as Windows owns
        // the registration. A failed unregister retains this reference.
        if (!_powerCallbacks.TryAdd(registrationHandle, callback))
        {
            _ = PowerUnregisterSuspendResumeNotificationNative(
                registrationHandle);

            registrationHandle = IntPtr.Zero;
            errorCode = ErrorInvalidData;
            return false;
        }

        errorCode = 0;
        return true;
    }

    /// <inheritdoc />
    public bool TryUnregisterSuspendResumeNotification(
        IntPtr registrationHandle,
        out int errorCode)
    {
        if (registrationHandle == IntPtr.Zero)
        {
            errorCode = ErrorInvalidHandle;
            return false;
        }

        uint nativeError =
            PowerUnregisterSuspendResumeNotificationNative(
                registrationHandle);

        if (nativeError != 0)
        {
            // Keep the callback rooted. Windows may still invoke it while
            // the registration remains active.
            errorCode = unchecked((int)nativeError);
            return false;
        }

        _powerCallbacks.TryRemove(
            registrationHandle,
            out _);

        errorCode = 0;
        return true;
    }

    /// <summary>
    /// Represents a hidden top-level native window and the message loop
    /// owned by the thread that created it.
    /// </summary>
    private sealed class NativeWindow
        : IWindowsWorkstationSecurityNativeWindow
    {
        private readonly Action<uint, nuint, nint> _messageReceived;

        // The callback delegate must remain rooted for the complete lifetime
        // of the registered native window class.
        private readonly WindowProcedure _windowProcedure;

        private readonly string _className;

        private readonly IntPtr _moduleHandle;

        private readonly uint _ownerThreadId;

        private IntPtr _windowHandle;

        private bool _classRegistered;

        private int _stopRequested;

        private int _disposed;

        public NativeWindow(
            Action<uint, nuint, nint> messageReceived)
        {
            ArgumentNullException.ThrowIfNull(messageReceived);

            _messageReceived = messageReceived;
            _windowProcedure = ProcessWindowMessage;
            _className = CreateUniqueClassName();

            _moduleHandle =
                GetModuleHandleNative(null);

            if (_moduleHandle == IntPtr.Zero)
            {
                throw CreateNativeException(
                    "Unable to obtain the current process module handle.");
            }

            _ownerThreadId =
                GetCurrentThreadIdNative();

            if (_ownerThreadId == 0)
            {
                throw CreateNativeException(
                    "Unable to obtain the native workstation-monitor thread ID.");
            }

            // Windows creates the message queue lazily. Ensure it exists
            // before another thread attempts to post WM_QUIT.
            EnsureMessageQueue();

            RegisterWindowClass();

            try
            {
                CreateWindow();
            }
            catch (Exception creationException)
            {
                try
                {
                    UnregisterWindowClass();
                }
                catch (Exception cleanupException)
                {
                    throw new AggregateException(
                        "Native window creation and window-class cleanup both failed.",
                        creationException,
                        cleanupException);
                }

                throw;
            }
        }

        /// <inheritdoc />
        public IntPtr Handle =>
            _windowHandle;

        /// <summary>
        /// Runs the message loop on the thread that created the native window.
        /// </summary>
        public void RunMessageLoop()
        {
            ThrowIfDisposed();
            EnsureOwnerThread();

            while (true)
            {
                int result =
                    GetMessageNative(
                        out NativeMessage message,
                        IntPtr.Zero,
                        0,
                        0);

                if (result == -1)
                {
                    throw CreateNativeException(
                        "The workstation notification message loop failed.");
                }

                if (result == 0)
                {
                    // WM_QUIT terminates the message loop.
                    return;
                }

                _ = TranslateMessageNative(in message);
                _ = DispatchMessageNative(in message);
            }
        }

        /// <summary>
        /// Requests termination of the owning thread's message loop.
        /// </summary>
        public void RequestStop()
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                return;
            }

            if (Interlocked.Exchange(ref _stopRequested, 1) != 0)
            {
                return;
            }

            // Preferred shutdown: post WM_QUIT to the owning thread.
            if (PostThreadMessageNative(
                    _ownerThreadId,
                    WindowMessageQuit,
                    UIntPtr.Zero,
                    IntPtr.Zero))
            {
                return;
            }

            int postThreadError =
                Marshal.GetLastWin32Error();

            IntPtr windowHandle =
                Interlocked.CompareExchange(
                    ref _windowHandle,
                    IntPtr.Zero,
                    IntPtr.Zero);

            // Fallback: WM_CLOSE is handled by our window procedure.
            // It terminates the message loop without destroying the HWND.
            if (windowHandle != IntPtr.Zero &&
                PostMessageNative(
                    windowHandle,
                    WindowMessageClose,
                    IntPtr.Zero,
                    IntPtr.Zero))
            {
                return;
            }

            int postWindowError =
                windowHandle == IntPtr.Zero
                    ? ErrorInvalidWindowHandle
                    : Marshal.GetLastWin32Error();

            // Allow a subsequent Stop request to retry.
            Volatile.Write(ref _stopRequested, 0);

            throw new AggregateException(
                "Unable to request shutdown of the workstation notification message loop.",
                new Win32Exception(
                    postThreadError,
                    "Posting WM_QUIT failed."),
                new Win32Exception(
                    postWindowError,
                    "Posting WM_CLOSE failed."));
        }

        /// <summary>
        /// Destroys the native window and unregisters its class.
        /// This method must execute on the window-creating thread.
        /// Cleanup remains retryable when either native operation fails.
        /// </summary>
        public void Dispose()
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                return;
            }

            // Validate ownership before changing any cleanup state.
            // A wrong-thread call must not prevent the owner thread from
            // completing native cleanup later.
            EnsureOwnerThread();

            List<Exception>? failures = null;

            IntPtr windowHandle =
                Interlocked.CompareExchange(
                    ref _windowHandle,
                    IntPtr.Zero,
                    IntPtr.Zero);

            if (windowHandle != IntPtr.Zero)
            {
                if (!DestroyWindowNative(windowHandle))
                {
                    failures ??= [];

                    failures.Add(
                        CreateNativeException(
                            "Destroying the workstation notification window failed."));
                }
                else
                {
                    // WM_NCDESTROY normally clears the handle. Clear it
                    // defensively after successful destruction.
                    Interlocked.Exchange(
                        ref _windowHandle,
                        IntPtr.Zero);
                }
            }

            // A window class must not be unregistered while its HWND
            // still exists. Retain the class registration for retry if
            // DestroyWindow failed.
            if (Interlocked.CompareExchange(
                    ref _windowHandle,
                    IntPtr.Zero,
                    IntPtr.Zero) == IntPtr.Zero)
            {
                try
                {
                    UnregisterWindowClass();
                }
                catch (Exception exception)
                {
                    failures ??= [];
                    failures.Add(exception);
                }
            }

            if (failures is { Count: 1 })
            {
                throw failures[0];
            }

            if (failures is { Count: > 1 })
            {
                throw new AggregateException(
                    "Native workstation notification cleanup was incomplete.",
                    failures);
            }

            // Commit the disposed state only after both native resources
            // have actually been released. If either operation failed,
            // a later call on the owner thread can retry it.
            Volatile.Write(ref _disposed, 1);
        }

        private void RegisterWindowClass()
        {
            var windowClass =
                new NativeWindowClass
                {
                    Size =
                        (uint)Marshal.SizeOf<NativeWindowClass>(),

                    Style = 0,

                    WindowProcedure =
                        _windowProcedure,

                    ClassExtraBytes = 0,

                    WindowExtraBytes = 0,

                    InstanceHandle =
                        _moduleHandle,

                    IconHandle =
                        IntPtr.Zero,

                    CursorHandle =
                        IntPtr.Zero,

                    BackgroundBrushHandle =
                        IntPtr.Zero,

                    MenuName = null,

                    ClassName =
                        _className,

                    SmallIconHandle =
                        IntPtr.Zero
                };

            ushort atom =
                RegisterClassExNative(
                    ref windowClass);

            if (atom == 0)
            {
                throw CreateNativeException(
                    "Registering the workstation notification window class failed.");
            }

            _classRegistered = true;
        }

        private void CreateWindow()
        {
            _windowHandle =
                CreateWindowExNative(
                    extendedStyle: 0,
                    className: _className,
                    windowName: _className,
                    style: WindowStylePopup,
                    x: 0,
                    y: 0,
                    width: 0,
                    height: 0,
                    parentWindowHandle: IntPtr.Zero,
                    menuHandle: IntPtr.Zero,
                    instanceHandle: _moduleHandle,
                    parameter: IntPtr.Zero);

            if (_windowHandle == IntPtr.Zero)
            {
                throw CreateNativeException(
                    "Creating the workstation notification window failed.");
            }
        }

        private static void EnsureMessageQueue()
        {
            // PeekMessage creates the current thread's message queue.
            // PM_NOREMOVE does not remove a message from that queue.
            _ = PeekMessageNative(
                out _,
                IntPtr.Zero,
                0,
                0,
                PeekMessageNoRemove);
        }

        private void UnregisterWindowClass()
        {
            if (!_classRegistered)
            {
                return;
            }

            if (!UnregisterClassNative(
                    _className,
                    _moduleHandle))
            {
                throw CreateNativeException(
                    "Unregistering the workstation notification window class failed.");
            }

            _classRegistered = false;
        }

        private IntPtr ProcessWindowMessage(
            IntPtr windowHandle,
            uint message,
            IntPtr wParam,
            IntPtr lParam)
        {
            if (message == WindowMessageClose)
            {
                // Do not call DefWindowProc here.
                // DefWindowProc handles WM_CLOSE by destroying the window.
                // We need the registered HWND to remain alive until WTS
                // unregistration completes.
                PostQuitMessageNative(0);

                return IntPtr.Zero;
            }

            if (message == WindowMessageDestroy)
            {
                PostQuitMessageNative(0);

                return IntPtr.Zero;
            }

            if (message == WindowMessageNonClientDestroy)
            {
                try
                {
                    return DefWindowProcNative(
                        windowHandle,
                        message,
                        wParam,
                        lParam);
                }
                finally
                {
                    Interlocked.Exchange(
                        ref _windowHandle,
                        IntPtr.Zero);
                }
            }

            try
            {
                _messageReceived(
                    message,
                    unchecked((nuint)wParam.ToInt64()),
                    lParam);
            }
            catch (Exception)
            {
                Trace.TraceError(
                    "A workstation native-window message callback failed.");
            }

            return DefWindowProcNative(
                windowHandle,
                message,
                wParam,
                lParam);
        }

        private static string CreateUniqueClassName()
        {
            long sequence =
                Interlocked.Increment(
                    ref _windowClassSequence);

            return $"DeskVault.WorkstationSecurity.{Environment.ProcessId}.{sequence}";
        }

        private void EnsureOwnerThread()
        {
            uint currentThreadId =
                GetCurrentThreadIdNative();

            if (currentThreadId != _ownerThreadId)
            {
                throw new InvalidOperationException(
                    "Native workstation window operations must execute on the creating thread.");
            }
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(
                Volatile.Read(ref _disposed) != 0,
                this);
        }

        private static Win32Exception CreateNativeException(
            string message)
        {
            int errorCode =
                Marshal.GetLastWin32Error();

            return new Win32Exception(
                errorCode,
                message);
        }
    }

    #region Native window procedure and structures

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate IntPtr WindowProcedure(
        IntPtr windowHandle,
        uint message,
        IntPtr wParam,
        IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeWindowClass
    {
        public uint Size;

        public uint Style;

        [MarshalAs(UnmanagedType.FunctionPtr)]
        public WindowProcedure WindowProcedure;

        public int ClassExtraBytes;

        public int WindowExtraBytes;

        public IntPtr InstanceHandle;

        public IntPtr IconHandle;

        public IntPtr CursorHandle;

        public IntPtr BackgroundBrushHandle;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string? MenuName;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string? ClassName;

        public IntPtr SmallIconHandle;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMessage
    {
        public IntPtr WindowHandle;

        public uint Message;

        public UIntPtr WParam;

        public IntPtr LParam;

        public uint Time;

        public NativePoint Point;

        public uint Private;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;

        public int Y;
    }

    [StructLayout(
        LayoutKind.Sequential,
        CharSet = CharSet.Unicode,
        Pack = 8)]
    internal struct WtsInfoExNative
    {
        public uint Level;

        // WTSINFOEXW contains a union with WTSINFOEX_LEVEL1_W as
        // its supported level-1 member. Sequential layout preserves
        // the native union alignment for this single supported member.
        public WtsInfoExLevel1Native Level1;
    }

    [StructLayout(
        LayoutKind.Sequential,
        CharSet = CharSet.Unicode,
        Pack = 8)]
    internal struct WtsInfoExLevel1Native
    {
        public uint SessionId;

        public int SessionState;

        public int SessionFlags;

        // The native WTS definitions use the respective maximum
        // lengths plus one character for the terminating null.
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)]
        public string WinStationName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 21)]
        public string UserName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 18)]
        public string DomainName;

        public long LogonTime;

        public long ConnectTime;

        public long DisconnectTime;

        public long LastInputTime;

        public long CurrentTime;

        public uint IncomingBytes;

        public uint OutgoingBytes;

        public uint IncomingFrames;

        public uint OutgoingFrames;

        public uint IncomingCompressedBytes;

        public uint OutgoingCompressedBytes;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DeviceNotifySubscribeParameters
    {
        public WindowsWorkstationPowerNotificationCallback Callback;

        public IntPtr Context;
    }

    #endregion

    #region Win32 imports

    [DllImport(
        "kernel32.dll",
        EntryPoint = "GetModuleHandleW",
        ExactSpelling = true,
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    private static extern IntPtr GetModuleHandleNative(
        string? moduleName);

    [DllImport(
        "kernel32.dll",
        EntryPoint = "GetCurrentThreadId",
        ExactSpelling = true,
        SetLastError = false)]
    private static extern uint GetCurrentThreadIdNative();

    [DllImport(
        "user32.dll",
        EntryPoint = "RegisterClassExW",
        ExactSpelling = true,
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    private static extern ushort RegisterClassExNative(
        ref NativeWindowClass windowClass);

    [DllImport(
        "user32.dll",
        EntryPoint = "UnregisterClassW",
        ExactSpelling = true,
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterClassNative(
        string className,
        IntPtr instanceHandle);

    [DllImport(
        "user32.dll",
        EntryPoint = "CreateWindowExW",
        ExactSpelling = true,
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    private static extern IntPtr CreateWindowExNative(
        uint extendedStyle,
        string className,
        string windowName,
        uint style,
        int x,
        int y,
        int width,
        int height,
        IntPtr parentWindowHandle,
        IntPtr menuHandle,
        IntPtr instanceHandle,
        IntPtr parameter);

    [DllImport(
        "user32.dll",
        EntryPoint = "DestroyWindow",
        ExactSpelling = true,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindowNative(
        IntPtr windowHandle);

    [DllImport(
        "user32.dll",
        EntryPoint = "DefWindowProcW",
        ExactSpelling = true,
        SetLastError = false)]
    private static extern IntPtr DefWindowProcNative(
        IntPtr windowHandle,
        uint message,
        IntPtr wParam,
        IntPtr lParam);

    [DllImport(
        "user32.dll",
        EntryPoint = "PeekMessageW",
        ExactSpelling = true,
        SetLastError = false)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PeekMessageNative(
        out NativeMessage message,
        IntPtr windowHandle,
        uint minimumMessage,
        uint maximumMessage,
        uint removeMessage);

    [DllImport(
        "user32.dll",
        EntryPoint = "GetMessageW",
        ExactSpelling = true,
        SetLastError = true)]
    private static extern int GetMessageNative(
        out NativeMessage message,
        IntPtr windowHandle,
        uint minimumMessage,
        uint maximumMessage);

    [DllImport(
        "user32.dll",
        EntryPoint = "TranslateMessage",
        ExactSpelling = true,
        SetLastError = false)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TranslateMessageNative(
        in NativeMessage message);

    [DllImport(
        "user32.dll",
        EntryPoint = "DispatchMessageW",
        ExactSpelling = true,
        SetLastError = false)]
    private static extern IntPtr DispatchMessageNative(
        in NativeMessage message);

    [DllImport(
        "user32.dll",
        EntryPoint = "PostThreadMessageW",
        ExactSpelling = true,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostThreadMessageNative(
        uint threadId,
        uint message,
        UIntPtr wParam,
        IntPtr lParam);

    [DllImport(
        "user32.dll",
        EntryPoint = "PostMessageW",
        ExactSpelling = true,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessageNative(
        IntPtr windowHandle,
        uint message,
        IntPtr wParam,
        IntPtr lParam);

    [DllImport(
        "user32.dll",
        EntryPoint = "PostQuitMessage",
        ExactSpelling = true,
        SetLastError = false)]
    private static extern void PostQuitMessageNative(
        int exitCode);

    [DllImport(
        "wtsapi32.dll",
        EntryPoint = "WTSRegisterSessionNotification",
        ExactSpelling = true,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WtsRegisterSessionNotification(
        IntPtr windowHandle,
        uint flags);

    [DllImport(
        "wtsapi32.dll",
        EntryPoint = "WTSUnRegisterSessionNotification",
        ExactSpelling = true,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WtsUnregisterSessionNotification(
        IntPtr windowHandle);

    private static bool TryGetProcessSessionIdNative(
        uint processId,
        out uint sessionId,
        out int errorCode)
    {
        bool succeeded =
            ProcessIdToSessionIdNative(
                processId,
                out sessionId);

        errorCode =
            succeeded
                ? 0
                : Marshal.GetLastWin32Error();

        return succeeded;
    }

    [DllImport(
        "kernel32.dll",
        EntryPoint = "ProcessIdToSessionId",
        ExactSpelling = true,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ProcessIdToSessionIdNative(
        uint processId,
        out uint sessionId);

    [DllImport(
        "wtsapi32.dll",
        EntryPoint = "WTSQuerySessionInformationW",
        ExactSpelling = true,
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WtsQuerySessionInformationNative(
        IntPtr serverHandle,
        uint sessionId,
        int informationClass,
        out IntPtr buffer,
        out uint bytesReturned);

    [DllImport(
        "wtsapi32.dll",
        EntryPoint = "WTSFreeMemory",
        ExactSpelling = true,
        SetLastError = false)]
    private static extern void WtsFreeMemoryNative(
        IntPtr memory);

    [DllImport(
        "powrprof.dll",
        EntryPoint = "PowerRegisterSuspendResumeNotification",
        ExactSpelling = true,
        SetLastError = false)]
    private static extern uint PowerRegisterSuspendResumeNotificationNative(
        uint flags,
        ref DeviceNotifySubscribeParameters recipient,
        out IntPtr registrationHandle);

    [DllImport(
        "powrprof.dll",
        EntryPoint = "PowerUnregisterSuspendResumeNotification",
        ExactSpelling = true,
        SetLastError = false)]
    private static extern uint PowerUnregisterSuspendResumeNotificationNative(
        IntPtr registrationHandle);

    #endregion
}
