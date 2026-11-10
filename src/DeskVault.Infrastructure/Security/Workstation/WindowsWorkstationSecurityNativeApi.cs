using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DeskVault.Infrastructure.Security.Workstation;

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

    private static long _windowClassSequence;

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
        /// </summary>
        public void Dispose()
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                return;
            }

            // Validate ownership before committing the disposed state. A
            // wrong-thread call must not prevent the owner thread from later
            // completing native cleanup.
            EnsureOwnerThread();

            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

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
                    // WM_NCDESTROY normally clears the field during the
                    // native call. Clear it here as a defensive fallback.
                    Interlocked.Exchange(
                        ref _windowHandle,
                        IntPtr.Zero);
                }
            }

            try
            {
                UnregisterWindowClass();
            }
            catch (Exception exception)
            {
                failures ??= [];
                failures.Add(exception);
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

    #endregion
}
