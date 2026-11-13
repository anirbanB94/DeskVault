using DeskVault.Infrastructure.Security.Workstation;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DeskVault.Infrastructure.Tests;

public sealed class WindowsWorkstationSecurityNativeApiTests
{
    [Fact]
    public void WtsInfoExNativeLayout_WhenMarshalled_MatchesWindowsNativeAbi()
    {
        // This ABI test must execute in a native process on a supported
        // 64-bit Windows architecture. An emulated process is not evidence.
        Architecture processArchitecture =
            RuntimeInformation.ProcessArchitecture;

        Architecture osArchitecture =
            RuntimeInformation.OSArchitecture;

        Assert.True(
            processArchitecture is Architecture.X64 or Architecture.Arm64,
            $"Unsupported process architecture for workstation native ABI validation: {processArchitecture}.");

        Assert.Equal(processArchitecture, osArchitecture);
        Assert.Equal(8, IntPtr.Size);

        // Arrange
        Type outerType =
            typeof(WindowsWorkstationSecurityNativeApi.WtsInfoExNative);

        Type level1Type =
            typeof(WindowsWorkstationSecurityNativeApi.WtsInfoExLevel1Native);

        // Act / Assert: WTSINFOEXW consists of a DWORD followed by an
        // 8-byte-aligned WTSINFOEX_LEVEL_W union.
        Assert.Equal(232, Marshal.SizeOf(outerType));
        Assert.Equal(
            8,
            Marshal.OffsetOf(
                outerType,
                nameof(WindowsWorkstationSecurityNativeApi.WtsInfoExNative.Level1))
                .ToInt32());

        // WTSINFOEX_LEVEL1_W layout is validated on both supported x64 and ARM64 Windows process architectures.
        Assert.Equal(224, Marshal.SizeOf(level1Type));

        Assert.Equal(
            0,
            Marshal.OffsetOf(
                level1Type,
                nameof(WindowsWorkstationSecurityNativeApi.WtsInfoExLevel1Native.SessionId))
                .ToInt32());

        Assert.Equal(
            4,
            Marshal.OffsetOf(
                level1Type,
                nameof(WindowsWorkstationSecurityNativeApi.WtsInfoExLevel1Native.SessionState))
                .ToInt32());

        Assert.Equal(
            8,
            Marshal.OffsetOf(
                level1Type,
                nameof(WindowsWorkstationSecurityNativeApi.WtsInfoExLevel1Native.SessionFlags))
                .ToInt32());

        Assert.Equal(
            12,
            Marshal.OffsetOf(
                level1Type,
                nameof(WindowsWorkstationSecurityNativeApi.WtsInfoExLevel1Native.WinStationName))
                .ToInt32());

        Assert.Equal(
            78,
            Marshal.OffsetOf(
                level1Type,
                nameof(WindowsWorkstationSecurityNativeApi.WtsInfoExLevel1Native.UserName))
                .ToInt32());

        Assert.Equal(
            120,
            Marshal.OffsetOf(
                level1Type,
                nameof(WindowsWorkstationSecurityNativeApi.WtsInfoExLevel1Native.DomainName))
                .ToInt32());

        Assert.Equal(
            160,
            Marshal.OffsetOf(
                level1Type,
                nameof(WindowsWorkstationSecurityNativeApi.WtsInfoExLevel1Native.LogonTime))
                .ToInt32());

        Assert.Equal(
            168,
            Marshal.OffsetOf(
                level1Type,
                nameof(WindowsWorkstationSecurityNativeApi.WtsInfoExLevel1Native.ConnectTime))
                .ToInt32());

        Assert.Equal(
            176,
            Marshal.OffsetOf(
                level1Type,
                nameof(WindowsWorkstationSecurityNativeApi.WtsInfoExLevel1Native.DisconnectTime))
                .ToInt32());

        Assert.Equal(
            184,
            Marshal.OffsetOf(
                level1Type,
                nameof(WindowsWorkstationSecurityNativeApi.WtsInfoExLevel1Native.LastInputTime))
                .ToInt32());

        Assert.Equal(
            192,
            Marshal.OffsetOf(
                level1Type,
                nameof(WindowsWorkstationSecurityNativeApi.WtsInfoExLevel1Native.CurrentTime))
                .ToInt32());

        Assert.Equal(
            200,
            Marshal.OffsetOf(
                level1Type,
                nameof(WindowsWorkstationSecurityNativeApi.WtsInfoExLevel1Native.IncomingBytes))
                .ToInt32());

        Assert.Equal(
            204,
            Marshal.OffsetOf(
                level1Type,
                nameof(WindowsWorkstationSecurityNativeApi.WtsInfoExLevel1Native.OutgoingBytes))
                .ToInt32());

        Assert.Equal(
            208,
            Marshal.OffsetOf(
                level1Type,
                nameof(WindowsWorkstationSecurityNativeApi.WtsInfoExLevel1Native.IncomingFrames))
                .ToInt32());

        Assert.Equal(
            212,
            Marshal.OffsetOf(
                level1Type,
                nameof(WindowsWorkstationSecurityNativeApi.WtsInfoExLevel1Native.OutgoingFrames))
                .ToInt32());

        Assert.Equal(
            216,
            Marshal.OffsetOf(
                level1Type,
                nameof(WindowsWorkstationSecurityNativeApi.WtsInfoExLevel1Native.IncomingCompressedBytes))
                .ToInt32());

        Assert.Equal(
            220,
            Marshal.OffsetOf(
                level1Type,
                nameof(WindowsWorkstationSecurityNativeApi.WtsInfoExLevel1Native.OutgoingCompressedBytes))
                .ToInt32());
    }

    [Theory]
    [InlineData(Architecture.X64, 8, true)]
    [InlineData(Architecture.Arm64, 8, true)]
    [InlineData(Architecture.X86, 4, false)]
    [InlineData(Architecture.Arm, 4, false)]
    [InlineData(Architecture.X64, 4, false)]
    [InlineData(Architecture.Arm64, 4, false)]
    public void IsSupportedProcessArchitecture_WhenEvaluated_Enforces64BitX64AndArm64Policy(
        Architecture processArchitecture,
        int pointerSize,
        bool expected)
    {
        // Act
        bool actual =
            WindowsWorkstationSecurityNativeApi.IsSupportedProcessArchitecture(
                processArchitecture,
                pointerSize);

        // Assert
        Assert.Equal(expected, actual);
    }
    [Fact]
    public async Task NativeWindow_WhenStopIsRequested_StopsMessageLoopAndDisposesOnCreatingThread()
    {
        // Arrange
        var nativeApi =
            new WindowsWorkstationSecurityNativeApi();

        var windowCreated =
            new TaskCompletionSource<IWindowsWorkstationSecurityNativeWindow>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var workerCompleted =
            new TaskCompletionSource<Exception?>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        int ownerThreadId = 0;

        var worker =
            new Thread(
                () =>
                {
                    IWindowsWorkstationSecurityNativeWindow? window = null;

                    try
                    {
                        ownerThreadId =
                            Environment.CurrentManagedThreadId;

                        window =
                            nativeApi.CreateHiddenTopLevelWindow(
                                (_, _, _) => { });

                        windowCreated.TrySetResult(window);

                        window.RunMessageLoop();

                        window.Dispose();

                        workerCompleted.TrySetResult(null);
                    }
                    catch (Exception exception)
                    {
                        if (window is not null)
                        {
                            try
                            {
                                window.Dispose();
                            }
                            catch (Exception cleanupException)
                            {
                                exception =
                                    new AggregateException(
                                        exception,
                                        cleanupException);
                            }
                        }

                        windowCreated.TrySetException(exception);
                        workerCompleted.TrySetResult(exception);
                    }
                })
            {
                IsBackground = true,
                Name = "DeskVault.NativeWindowSmokeTest"
            };

        worker.Start();

        // Act
        IWindowsWorkstationSecurityNativeWindow nativeWindow =
            await windowCreated.Task.WaitAsync(
                TimeSpan.FromSeconds(10),
                TestContext.Current.CancellationToken);

        // Assert: the native HWND was created successfully.
        Assert.NotEqual(
            IntPtr.Zero,
            nativeWindow.Handle);

        // RequestStop runs from the test thread.
        // The actual window is still owned by the dedicated worker thread.
        nativeWindow.RequestStop();

        Exception? workerFailure =
            await workerCompleted.Task.WaitAsync(
                TimeSpan.FromSeconds(10),
                TestContext.Current.CancellationToken);

        Assert.True(
            worker.Join(TimeSpan.FromSeconds(5)),
            "The native workstation message-loop thread did not terminate.");

        Assert.Null(workerFailure);

        Assert.NotEqual(0, ownerThreadId);
    }

    [Fact]
    public void TryGetProcessSessionId_WhenNativeLookupFails_ReturnsFailureAndNativeError()
    {
        // Arrange
        const int expectedErrorCode = 5;
        bool lookupInvoked = false;

        WindowsWorkstationProcessSessionIdLookup processSessionLookup =
            (
                uint processId,
                out uint sessionId,
                out int errorCode) =>
            {
                lookupInvoked = true;
                Assert.Equal((uint)Environment.ProcessId, processId);

                sessionId = 99;
                errorCode = expectedErrorCode;
                return false;
            };

        WindowsWorkstationWtsSessionInformationQuery query =
            (
                IntPtr serverHandle,
                uint sessionId,
                int informationClass,
                out IntPtr buffer,
                out uint bytesReturned) =>
            {
                buffer = IntPtr.Zero;
                bytesReturned = 0;

                throw new InvalidOperationException(
                    "WTS query must not run when process-session lookup fails.");
            };

        WindowsWorkstationWtsMemoryFree free =
            static memory =>
                throw new InvalidOperationException(
                    "WTS memory cleanup must not run when no WTS buffer exists.");

        var nativeApi =
            new WindowsWorkstationSecurityNativeApi(
                processSessionLookup,
                query,
                free);

        // Act
        bool succeeded =
            nativeApi.TryGetProcessSessionId(
                out uint processSessionId,
                out int errorCode);

        // Assert
        Assert.True(lookupInvoked);
        Assert.False(succeeded);
        Assert.Equal(0u, processSessionId);
        Assert.Equal(expectedErrorCode, errorCode);
    }
    [Fact]
    public void TryGetProcessSessionId_WhenCalled_ReturnsCurrentProcessSession()
    {
        // Arrange
        var nativeApi =
            new WindowsWorkstationSecurityNativeApi();

        using Process currentProcess =
            Process.GetCurrentProcess();

        // Act
        bool succeeded =
            nativeApi.TryGetProcessSessionId(
                out uint processSessionId,
                out int errorCode);

        // Assert
        Assert.True(succeeded, $"ProcessIdToSessionId failed with {errorCode}.");
        Assert.Equal(0, errorCode);
        Assert.Equal((uint)currentProcess.SessionId, processSessionId);
    }

    [Fact]
    public void TryQuerySessionInformation_WhenCurrentProcessSessionIsQueried_ReturnsWtsInfoEx()
    {
        // Arrange
        var nativeApi =
            new WindowsWorkstationSecurityNativeApi();

        bool sessionLookupSucceeded =
            nativeApi.TryGetProcessSessionId(
                out uint processSessionId,
                out int sessionLookupError);

        Assert.True(
            sessionLookupSucceeded,
            $"ProcessIdToSessionId failed with {sessionLookupError}.");

        // Act
        bool querySucceeded =
            nativeApi.TryQuerySessionInformation(
                processSessionId,
                out WindowsWorkstationSecuritySessionStateSnapshot snapshot,
                out int queryError);

        // Assert
        Assert.True(
            querySucceeded,
            $"WTSQuerySessionInformationW failed with {queryError}.");

        Assert.Equal(0, queryError);
        Assert.Equal(1u, snapshot.Level);
        Assert.Equal(processSessionId, snapshot.SessionId);

        Assert.Equal(
            (uint)WindowsWorkstationSecurityNativeApi.WtsInfoExStructureSize,
            snapshot.StructureSize);
    }

    [Fact]
    public void TryQuerySessionInformation_WhenQuerySucceeds_FreesReturnedWtsBuffer()
    {
        // Arrange
        int expectedSize =
            WindowsWorkstationSecurityNativeApi.WtsInfoExStructureSize;

        IntPtr nativeBuffer =
            Marshal.AllocHGlobal(expectedSize);

        IntPtr allocatedBuffer = nativeBuffer;

        Marshal.Copy(
            new byte[expectedSize],
            0,
            nativeBuffer,
            expectedSize);

        // WTSINFOEXW contains a 4-byte Level followed by an 8-byte-aligned
        // union. Level1 starts at offset 8; SessionId, SessionState and
        // SessionFlags occupy offsets 8, 12 and 16 respectively.
        Marshal.WriteInt32(nativeBuffer, 0, 1);
        Marshal.WriteInt32(nativeBuffer, 8, 42);
        Marshal.WriteInt32(nativeBuffer, 12, 0);
        Marshal.WriteInt32(nativeBuffer, 16, 1);

        int freeCount = 0;
        IntPtr freedBuffer = IntPtr.Zero;

        WindowsWorkstationWtsSessionInformationQuery query =
            (
                IntPtr server,
                uint sessionId,
                int informationClass,
                out IntPtr buffer,
                out uint bytesReturned) =>
            {
                Assert.Equal(IntPtr.Zero, server);
                Assert.Equal(42u, sessionId);
                Assert.Equal(25, informationClass);

                buffer = nativeBuffer;
                bytesReturned = (uint)expectedSize;
                return true;
            };

        WindowsWorkstationWtsMemoryFree free =
            memory =>
            {
                freedBuffer = memory;
                Interlocked.Increment(ref freeCount);
                Marshal.FreeHGlobal(memory);
                nativeBuffer = IntPtr.Zero;
            };

        var nativeApi =
            new WindowsWorkstationSecurityNativeApi(query, free);

        try
        {
            // Act
            bool succeeded =
                nativeApi.TryQuerySessionInformation(
                    42,
                    out WindowsWorkstationSecuritySessionStateSnapshot snapshot,
                    out int errorCode);

            // Assert
            Assert.True(succeeded);
            Assert.Equal(0, errorCode);
            Assert.Equal(1u, snapshot.Level);
            Assert.Equal(42u, snapshot.SessionId);
            Assert.Equal((uint)expectedSize, snapshot.StructureSize);
            Assert.Equal(0, snapshot.ConnectionState);
            Assert.Equal(1, snapshot.SessionFlags);

            Assert.Equal(1, freeCount);
            Assert.Equal(allocatedBuffer, freedBuffer);
        }
        finally
        {
            if (nativeBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(nativeBuffer);
            }
        }
    }

    [Fact]
    public void TryQuerySessionInformation_WhenReturnedBufferIsTooSmall_FreesMalformedWtsBuffer()
    {
        // Arrange
        IntPtr nativeBuffer =
            Marshal.AllocHGlobal(sizeof(int));

        Marshal.WriteInt32(nativeBuffer, 1);

        int freeCount = 0;
        IntPtr freedBuffer = IntPtr.Zero;

        WindowsWorkstationWtsSessionInformationQuery query =
            (
                IntPtr server,
                uint sessionId,
                int informationClass,
                out IntPtr buffer,
                out uint bytesReturned) =>
            {
                buffer = nativeBuffer;
                bytesReturned = sizeof(uint);
                return true;
            };

        WindowsWorkstationWtsMemoryFree free =
            memory =>
            {
                freedBuffer = memory;
                Interlocked.Increment(ref freeCount);
                Marshal.FreeHGlobal(memory);
                nativeBuffer = IntPtr.Zero;
            };

        var nativeApi =
            new WindowsWorkstationSecurityNativeApi(query, free);

        try
        {
            // Act
            bool succeeded =
                nativeApi.TryQuerySessionInformation(
                    42,
                    out WindowsWorkstationSecuritySessionStateSnapshot snapshot,
                    out int errorCode);

            // Assert
            Assert.False(succeeded);
            Assert.Equal(13, errorCode);
            Assert.Equal(default, snapshot);
            Assert.Equal(1, freeCount);
            Assert.NotEqual(IntPtr.Zero, freedBuffer);
        }
        finally
        {
            if (nativeBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(nativeBuffer);
            }
        }
    }

    [Fact]
    public void TryQuerySessionInformation_WhenReturnedSnapshotFailsParserValidation_FreesBuffer()
    {
        // Arrange
        int expectedSize =
            WindowsWorkstationSecurityNativeApi.WtsInfoExStructureSize;

        IntPtr nativeBuffer =
            Marshal.AllocHGlobal(expectedSize);

        IntPtr allocatedBuffer = nativeBuffer;

        Marshal.Copy(
            new byte[expectedSize],
            0,
            nativeBuffer,
            expectedSize);

        // Return a structurally complete response with an unsupported
        // WTSINFOEX level. The native query succeeds, but the Application
        // session-state parser must reject the snapshot.
        Marshal.WriteInt32(nativeBuffer, 0, 2);
        Marshal.WriteInt32(nativeBuffer, 8, 42);
        Marshal.WriteInt32(nativeBuffer, 12, 0);
        Marshal.WriteInt32(nativeBuffer, 16, 1);

        int freeCount = 0;
        IntPtr freedBuffer = IntPtr.Zero;

        WindowsWorkstationWtsSessionInformationQuery query =
            (
                IntPtr server,
                uint sessionId,
                int informationClass,
                out IntPtr buffer,
                out uint bytesReturned) =>
            {
                buffer = nativeBuffer;
                bytesReturned = (uint)expectedSize;
                return true;
            };

        WindowsWorkstationWtsMemoryFree free =
            memory =>
            {
                freedBuffer = memory;
                Interlocked.Increment(ref freeCount);
                Marshal.FreeHGlobal(memory);
                nativeBuffer = IntPtr.Zero;
            };

        var nativeApi =
            new WindowsWorkstationSecurityNativeApi(query, free);

        try
        {
            // Act: native marshalling succeeds and the buffer is released.
            bool querySucceeded =
                nativeApi.TryQuerySessionInformation(
                    42,
                    out WindowsWorkstationSecuritySessionStateSnapshot snapshot,
                    out int errorCode);

            // Assert: the independent parser rejects the unsupported level.
            Assert.True(querySucceeded);
            Assert.Equal(0, errorCode);
            Assert.Equal(2u, snapshot.Level);

            Assert.Equal(1, freeCount);
            Assert.Equal(allocatedBuffer, freedBuffer);

            Assert.Throws<InvalidDataException>(
                () => WindowsWorkstationSecuritySessionStateParser.Parse(
                    snapshot,
                    expectedSize,
                    42));

            // Parsing takes place after the native query has released its
            // buffer. Parser failure must not cause a second release.
            Assert.Equal(1, freeCount);
        }
        finally
        {
            if (nativeBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(nativeBuffer);
            }
        }
    }
    [Fact]
    public void TryQuerySessionInformation_WhenNativeQueryFailsWithBuffer_FreesBuffer()
    {
        // Arrange
        IntPtr nativeBuffer =
            Marshal.AllocHGlobal(sizeof(int));

        int freeCount = 0;
        IntPtr freedBuffer = IntPtr.Zero;

        WindowsWorkstationWtsSessionInformationQuery query =
            (
                IntPtr server,
                uint sessionId,
                int informationClass,
                out IntPtr buffer,
                out uint bytesReturned) =>
            {
                buffer = nativeBuffer;
                bytesReturned = 0;
                return false;
            };

        WindowsWorkstationWtsMemoryFree free =
            memory =>
            {
                freedBuffer = memory;
                Interlocked.Increment(ref freeCount);
                Marshal.FreeHGlobal(memory);
                nativeBuffer = IntPtr.Zero;
            };

        var nativeApi =
            new WindowsWorkstationSecurityNativeApi(query, free);

        try
        {
            // Act
            bool succeeded =
                nativeApi.TryQuerySessionInformation(
                    42,
                    out WindowsWorkstationSecuritySessionStateSnapshot snapshot,
                    out _);

            // Assert
            Assert.False(succeeded);
            Assert.Equal(default, snapshot);
            Assert.Equal(1, freeCount);
            Assert.NotEqual(IntPtr.Zero, freedBuffer);
        }
        finally
        {
            if (nativeBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(nativeBuffer);
            }
        }
    }

    [Fact]
    public void TryQuerySessionInformation_WhenNativeQueryThrowsAfterAllocatingBuffer_FreesBuffer()
    {
        // Arrange
        IntPtr nativeBuffer =
            Marshal.AllocHGlobal(sizeof(int));

        int freeCount = 0;
        IntPtr freedBuffer = IntPtr.Zero;

        WindowsWorkstationWtsSessionInformationQuery query =
            (
                IntPtr server,
                uint sessionId,
                int informationClass,
                out IntPtr buffer,
                out uint bytesReturned) =>
            {
                buffer = nativeBuffer;
                bytesReturned = sizeof(uint);

                throw new InvalidOperationException(
                    "Synthetic native-query exception.");
            };

        WindowsWorkstationWtsMemoryFree free =
            memory =>
            {
                freedBuffer = memory;
                Interlocked.Increment(ref freeCount);
                Marshal.FreeHGlobal(memory);
                nativeBuffer = IntPtr.Zero;
            };

        var nativeApi =
            new WindowsWorkstationSecurityNativeApi(query, free);

        try
        {
            // Act
            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(
                    () => nativeApi.TryQuerySessionInformation(
                        42,
                        out _,
                        out _));

            // Assert
            Assert.Contains(
                "Synthetic native-query exception.",
                exception.Message);

            Assert.Equal(1, freeCount);
            Assert.NotEqual(IntPtr.Zero, freedBuffer);
        }
        finally
        {
            if (nativeBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(nativeBuffer);
            }
        }
    }

    [Fact]
    public void TryRegisterSuspendResumeNotification_WhenRegistered_UnregistersAndReleasesCallback()
    {
        // Arrange
        var nativeApi =
            new WindowsWorkstationSecurityNativeApi();

        WindowsWorkstationPowerNotificationCallback callback =
            static (_, _, _) => 0;

        // Act
        bool registrationSucceeded =
            nativeApi.TryRegisterSuspendResumeNotification(
                callback,
                out IntPtr registrationHandle,
                out int registrationError);

        // Assert
        Assert.True(
            registrationSucceeded,
            $"Power registration failed with {registrationError}.");

        Assert.Equal(0, registrationError);
        Assert.NotEqual(IntPtr.Zero, registrationHandle);

        try
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();

            Assert.True(
                nativeApi.TryUnregisterSuspendResumeNotification(
                    registrationHandle,
                    out int unregistrationError),
                $"Power unregistration failed with {unregistrationError}.");

            Assert.Equal(0, unregistrationError);
        }
        finally
        {
            GC.KeepAlive(callback);
        }
    }

    [Fact]
    public void TryUnregisterSuspendResumeNotification_WhenHandleIsZero_ReturnsInvalidHandle()
    {
        // Arrange
        var nativeApi =
            new WindowsWorkstationSecurityNativeApi();

        // Act
        bool succeeded =
            nativeApi.TryUnregisterSuspendResumeNotification(
                IntPtr.Zero,
                out int errorCode);

        // Assert
        Assert.False(succeeded);
        Assert.Equal(6, errorCode);
    }
}
