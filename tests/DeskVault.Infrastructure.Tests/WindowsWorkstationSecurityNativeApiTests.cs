using DeskVault.Infrastructure.Security.Workstation;

namespace DeskVault.Infrastructure.Tests;

public sealed class WindowsWorkstationSecurityNativeApiTests
{
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
}
